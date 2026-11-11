#!/usr/bin/env python3
"""Front-end measurements for https://github.com/erniecohen/dafny/issues/241 (milestone D1).

    frontend-profile.py run     --dafny D --out DIR --corpus NAME --configs C1,C2,... [--reps 10] [--seed 1]
                                (--project FILE [--cwd DIR] | --plan PLAN.tsv --litdir DIR [--shard I/N])
                                [--command resolve|verify]
                                [--cores N]
    frontend-profile.py heap    --dafny D --out DIR --corpus NAME --project FILE [--cwd DIR] [--windows 1,4] [--reps 3]
    frontend-profile.py gcdump  --dafny D --out DIR --corpus NAME --project FILE [--cwd DIR] [--windows 1,4]
                                [--points parsed,resolved,1,20,43,80]
    frontend-profile.py analyze DIR... --out REPORT.md [--seed 1]
    frontend-profile.py check   PROFILE.jsonl     a digest that does not depend on timing, for tests

`run` is the timing protocol.  Every configuration of --configs runs once as a warm-up, which
is discarded, then --reps rounds follow; in each round every configuration runs once, in an
order shuffled with --seed and the round number.  All runs of one invocation are on one
machine, so two configurations' runs of the same round form a pair.  A configuration is
`<gc>-<resolver>-<profile>`:

    gc        ws (DOTNET_gcServer=0) or srv (DOTNET_gcServer=1)
    resolver  default (the project's own choice), legacy or refresh (--type-system-refresh)
    profile   off, on (--pipeline-profile) or gcev (on, plus --pipeline-profile-gc-events)

--command resolve runs `dafny resolve`; --command verify runs `dafny verify` with Boogie's no-op
solver (/proverOpt:SOLVER=noop), so that translation and Boogie's preparation run but no
solver does: front-end quantities never include solver time.  With --plan (a plan of
lit-verdicts.py), the corpus is every program of the plan that declares two or more modules,
and a run of a configuration runs each of them once, in plan order; programs for which any
configuration's warm-up reports a parse or resolution error are dropped from every round.

Outputs in DIR (CSV, one row per run, program, stage, module, collection or heap point):
runs.csv, stages.csv, modules.csv, gc.csv, heap.csv, graphs/*.json (a program's module graph
with each module's median weight comes from `analyze`), profiles/*.jsonl.gz (raw profiles),
outputs/ (each configuration's first output), machine.json.

`heap` runs `dafny verify` (no-op solver) with --pipeline-profile-heap at each module window K
(--verification-module-window), --reps times: a full compacting collection after parsing,
after resolution and after each module's results are reported, and the live heap after it.
`gcdump` runs the same with --pipeline-profile-hold and takes `dotnet-gcdump` heap dumps at the
named points (parsed, resolved, or the n-th reported module), with their type tables.

`analyze` prints Markdown tables: per configuration the median, interquartile range and 95%
bootstrap confidence interval (percentile, 10,000 resamples of the rounds) of each stage's wall
time, thread CPU time and allocation; paired ratios between configurations with their
bootstrap CI; the module graph's resolution weight W_R, weighted critical path L_R, width
profile and the ideal parallel bound R_global + max(W_R/N, L_R); the library share; retention;
and the GC.  Every derived quantity is given with its formula.

The driver only measures; it exits 0 unless a run could not be started.  It fails (exit 1) if
the output of a configuration with the profile on differs from the same configuration with it
off, since the profile must not change Dafny's output.
"""
import argparse
import csv
import gzip
import hashlib
import json
import os
import platform
import random
import re
import shlex
import shutil
import statistics
import subprocess
import sys
import tempfile
import threading
import time
from collections import defaultdict
from pathlib import Path

MODULE = re.compile(r'^\s*(?:abstract\s+|replaceable\s+)?module\s', re.M)
RESOLUTION_ERROR = re.compile(rb'(resolution/type errors|parse errors) detected')
NOOP = ['--solver-option', 'SOLVER=noop']
DETERMINISTIC = ['--boogie', '/deterministicLiteralHashes:1', '--boogie', '/restartProver']

# ---------------------------------------------------------------------------- machine


def machine():
    info = {'platform': platform.platform(), 'machine': platform.machine(), 'python': platform.python_version(),
            'cpus': os.cpu_count()}
    try:
        if sys.platform == 'darwin':
            info['cpu'] = subprocess.run(['sysctl', '-n', 'machdep.cpu.brand_string'], capture_output=True,
                                         text=True).stdout.strip()
            info['memoryBytes'] = int(subprocess.run(['sysctl', '-n', 'hw.memsize'], capture_output=True,
                                                     text=True).stdout.strip())
        else:
            with open('/proc/cpuinfo') as f:
                names = [l.split(':', 1)[1].strip() for l in f if l.startswith('model name')]
            info['cpu'] = names[0] if names else ''
            with open('/proc/meminfo') as f:
                info['memoryBytes'] = int(f.readline().split()[1]) * 1024
    except (OSError, ValueError, IndexError):
        pass
    try:
        info['dotnet'] = subprocess.run(['dotnet', '--list-runtimes'], capture_output=True, text=True).stdout.strip()
    except OSError:
        pass
    return info


# ---------------------------------------------------------------------------- configurations


def parse_config(name):
    gc, resolver, prof = name.split('-')
    assert gc in ('ws', 'srv') and resolver in ('default', 'legacy', 'refresh') and prof in ('off', 'on', 'gcev'), name
    return gc, resolver, prof


def command_line(dafny, command, config, target, own, profile_path, cores, extra=()):
    gc, resolver, prof = parse_config(config)
    args = [dafny, command]
    if command == 'verify':
        args += NOOP + ['--cores', str(cores)]
        if not uses_standard_libraries(target, own):
            args += DETERMINISTIC
    if resolver != 'default':
        args += ['--type-system-refresh:' + ('true' if resolver == 'refresh' else 'false')]
    if prof != 'off':
        args += ['--pipeline-profile', str(profile_path)]
    if prof == 'gcev':
        args += ['--pipeline-profile-gc-events']
    args += list(extra) + list(own) + [str(target)]
    env = dict(os.environ)
    env['DOTNET_gcServer'] = '1' if gc == 'srv' else '0'
    env['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    return args, env


def uses_standard_libraries(target, own):
    """A program built on the standard library's .doo refuses --boogie options other than its own."""
    if any(t.startswith('--standard-libraries') for t in own):
        return True
    try:
        return str(target).endswith('.toml') and re.search(r'^\s*standard-libraries\s*=\s*true', Path(target).read_text(),
                                                           re.M) is not None
    except OSError:
        return False


def run_one(args, env, cwd):
    """Run once; wall seconds, rusage of the child and output bytes."""
    t0 = time.perf_counter()
    with tempfile.TemporaryFile() as out:
        proc = subprocess.Popen(args, cwd=cwd, env=env, stdout=out, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL)
        _, status, ru = os.wait4(proc.pid, 0)
        wall = time.perf_counter() - t0
        proc.returncode = os.waitstatus_to_exitcode(status)
        out.seek(0)
        data = out.read()
    rss_mb = ru.ru_maxrss / (1024 * 1024) if sys.platform == 'darwin' else ru.ru_maxrss / 1024
    return {'exit': proc.returncode, 'wall_s': wall, 'user_s': ru.ru_utime, 'sys_s': ru.ru_stime,
            'maxrss_mb': rss_mb}, data


# ---------------------------------------------------------------------------- profiles


def load_events(path):
    opener = gzip.open if str(path).endswith('.gz') else open
    with opener(path, 'rt') as f:
        return [json.loads(l) for l in f if l.strip()]


def reduce_profile(events):
    """Stage sums, module rows, the module graph, GC and heap rows of one profile."""
    stages = defaultdict(lambda: [0, 0.0, 0.0, 0, 0.0])  # n, wall, cpu, alloc, gc pause
    graph = next((e for e in events if e['ev'] == 'moduleGraph'), None)
    library = {n['id']: n['library'] for n in graph['nodes']} if graph else {}
    modules = []
    for e in events:
        if e['ev'] == 'span':
            s = stages['span:' + e['stage']]
            s[0] += 1
            s[1] += e['end'] - e['start']
            s[2] += e.get('cpuMs') or 0
            s[3] += e.get('allocBytes') or 0
            s[4] += e.get('gcPauseMs') or 0
        elif e['ev'] == 'fe':
            names = [e['stage']]
            if e['stage'] == 'resolve.module':
                names.append('resolve.module.' + ('library' if library.get(e['id']) else 'user'))
                modules.append({'id': e['id'], 'module': e['module'], 'kind': e['kind'],
                                'library': int(bool(library.get(e['id']))), 'wall_ms': e['end'] - e['start'],
                                'cpu_ms': e.get('threadCpuMs'), 'alloc_bytes': e['threadAllocBytes'],
                                'gc_pause_ms': e['gcPauseMs'], 'refresh': int(bool(e.get('typeSystemRefresh')))})
                for p in e.get('phases') or []:
                    names.append('phase:' + p['phase'])
                    s = stages['phase:' + p['phase']]
                    s[0] += 1
                    s[1] += p['ms']
                    s[2] += p.get('threadCpuMs') or 0
                    s[3] += p['threadAllocBytes']
                names = names[:2]
            if e['stage'].startswith('resolve.global'):
                names.append('resolve.global')
            for name in names:
                s = stages[name]
                s[0] += 1
                s[1] += e['end'] - e['start']
                s[2] += e.get('threadCpuMs') or 0
                s[3] += e['threadAllocBytes']
                s[4] += e['gcPauseMs']
    summary = next((e for e in reversed(events) if e['ev'] == 'summary'), None)
    resolve_end = max((e['end'] for e in events if e['ev'] == 'span' and e['stage'] == 'resolve'), default=None)
    pauses = [e for e in events if e['ev'] == 'gcpause']
    gcs = [e for e in events if e['ev'] == 'gc']
    gc = None
    if pauses or gcs:
        gens = [sum(1 for g in gcs if g['gen'] == k) for k in range(3)]
        fe_pauses = [p['ms'] for p in pauses if resolve_end is not None and p['end'] <= resolve_end]
        gc = {'gcs': len(gcs), 'gen0': gens[0], 'gen1': gens[1], 'gen2': gens[2],
              'background': sum(1 for g in gcs if g['type'] == 1),
              'pauses': len(pauses), 'pause_ms_total': sum(p['ms'] for p in pauses),
              'pause_ms_max': max((p['ms'] for p in pauses), default=0),
              'pause_ms_p95': quantile(sorted(p['ms'] for p in pauses), 0.95) if pauses else 0,
              'pause_ms_frontend': sum(fe_pauses), 'pauses_frontend': len(fe_pauses),
              'promoted_bytes': sum(g.get('promotedBytes') or 0 for g in gcs)}
    heap = [e for e in events if e['ev'] == 'heap']
    manifest = events[0] if events and events[0]['ev'] == 'manifest' else {}
    totals = {'alloc_bytes': summary['allocBytes'] if summary else None,
              'gc_pause_ms': summary['gcPauseMs'] if summary else None,
              'cpu_ms': summary['cpuMs'] if summary else None,
              'resolve_end_ms': resolve_end}
    return stages, modules, graph, gc, heap, manifest, totals


# ---------------------------------------------------------------------------- run


class Writer:
    def __init__(self, out):
        self.out = out
        self.files = {}

    def row(self, name, row):
        if name not in self.files:
            path = self.out / (name + '.csv')
            new = not path.exists()
            f = open(path, 'a', newline='')
            w = csv.DictWriter(f, fieldnames=list(row))
            if new:
                w.writeheader()
            self.files[name] = (f, w)
        f, w = self.files[name]
        w.writerow(row)
        f.flush()


def corpus_programs(a):
    if a.plan:
        rows = [l.rstrip('\n').split('\t') for l in open(a.plan) if l.strip()]
        chosen = []
        for r in rows:
            path, opts = r[0], (r[1] if len(r) > 1 else '')
            text = (Path(a.litdir) / path).read_text(errors='replace')
            if len(MODULE.findall(text)) >= 2:
                own = shlex.split(opts)
                # The suite's programs are verified elsewhere; here the resolver choice is the configuration's.
                own = [t for t in own if not t.startswith('--type-system-refresh') and not t.startswith('--cores')
                       and not t.startswith('--resource-limit') and not t.startswith('--verification-time-limit')]
                chosen.append((path, own, Path(a.litdir)))
        i, n = (int(x) for x in a.shard.split('/'))
        return [c for k, c in enumerate(chosen) if k % n == i]
    return [(a.project, [], Path(a.cwd or '.'))]


def slug(s):
    return re.sub(r'[^A-Za-z0-9_.-]+', '_', s)


def cmd_run(a):
    out = Path(a.out).resolve()
    (out / 'profiles').mkdir(parents=True, exist_ok=True)
    (out / 'outputs').mkdir(exist_ok=True)
    (out / 'graphs').mkdir(exist_ok=True)
    (out / 'machine.json').write_text(json.dumps(machine(), indent=1))
    configs = a.configs.split(',')
    programs = corpus_programs(a)
    w = Writer(out)
    outputs = {}
    mismatches = []
    dropped = set()

    def go(config, rep, order, warmup):
        for k, (target, own, cwd) in enumerate(programs):
            if target in dropped:
                continue
            prof_path = out / 'profiles' / f'{a.corpus}__{slug(target)}__{config}__{rep}.jsonl'
            args, env = command_line(a.dafny, a.command, config, target, own, prof_path, a.cores)
            res, data = run_one(args, env, cwd)
            sha = hashlib.sha256(data).hexdigest()[:16]
            key = (target, config)
            if key not in outputs:
                outputs[key] = sha
                (out / 'outputs' / f'{a.corpus}__{slug(target)}__{config}.txt').write_bytes(data)
            if warmup and RESOLUTION_ERROR.search(data):
                dropped.add(target)
            w.row('runs', {'corpus': a.corpus, 'program': target, 'config': config, 'rep': rep, 'order': order,
                           'warmup': int(warmup), **{k2: round(v, 4) if isinstance(v, float) else v
                                                     for k2, v in res.items()}, 'out_sha': sha,
                           'args': ' '.join(shlex.quote(x) for x in args[1:])})
            if prof_path.exists():
                events = load_events(prof_path)
                with open(prof_path, 'rb') as src, gzip.open(str(prof_path) + '.gz', 'wb') as dst:
                    shutil.copyfileobj(src, dst)
                prof_path.unlink()
                if warmup:
                    continue
                stages, modules, graph, gc, heap, manifest, totals = reduce_profile(events)
                base = {'corpus': a.corpus, 'program': target, 'config': config, 'rep': rep}
                for stage, (n, wall, cpu, alloc, pause) in sorted(stages.items()):
                    w.row('stages', {**base, 'stage': stage, 'n': n, 'wall_ms': round(wall, 3),
                                     'cpu_ms': round(cpu, 3), 'alloc_bytes': alloc, 'gc_pause_ms': round(pause, 3)})
                w.row('stages', {**base, 'stage': 'process', 'n': 1, 'wall_ms': round(res['wall_s'] * 1000, 3),
                                 'cpu_ms': round((res['user_s'] + res['sys_s']) * 1000, 3),
                                 'alloc_bytes': totals['alloc_bytes'], 'gc_pause_ms': totals['gc_pause_ms']})
                for m in modules:
                    w.row('modules', {**base, **m})
                if gc:
                    w.row('gc', {**base, **gc, 'alloc_bytes': totals['alloc_bytes'],
                                 'server_gc': int(bool(manifest.get('serverGC')))})
                gpath = out / 'graphs' / f'{a.corpus}__{slug(target)}.json'
                if graph and not gpath.exists():
                    gpath.write_text(json.dumps({'corpus': a.corpus, 'program': target, 'nodes': graph['nodes'],
                                                 'edges': graph['edges'], 'manifest': manifest}))
            print(f'{a.corpus} {config} rep {rep}: {target} exit {res["exit"]} {res["wall_s"]:.2f}s', flush=True)

    for order, config in enumerate(configs):
        go(config, 0, order, True)
    if dropped:
        print(f'dropped {len(dropped)} of {len(programs)} programs with parse or resolution errors', flush=True)
        (out / f'dropped-{a.corpus}.txt').write_text('\n'.join(sorted(dropped)) + '\n')
    for rep in range(1, a.reps + 1):
        order = configs[:]
        random.Random(a.seed * 1000 + rep).shuffle(order)
        for k, config in enumerate(order):
            go(config, rep, k, False)

    # The profile must not change the output.
    for (target, config), sha in outputs.items():
        gc, resolver, prof = parse_config(config)
        if prof != 'off':
            off = outputs.get((target, f'{gc}-{resolver}-off'))
            if off is not None and off != sha:
                mismatches.append(f'{target}: {config} output differs from {gc}-{resolver}-off')
    (out / f'identity-{a.corpus}.txt').write_text(
        ('\n'.join(mismatches) if mismatches else 'identical: every profiled configuration\'s output equals its '
                                                    'unprofiled one\'s, where both ran') + '\n')
    return 1 if mismatches else 0


# ---------------------------------------------------------------------------- heap and gcdump


def cmd_heap(a):
    out = Path(a.out).resolve()
    (out / 'profiles').mkdir(parents=True, exist_ok=True)
    (out / 'machine.json').write_text(json.dumps(machine(), indent=1))
    w = Writer(out)
    for rep in range(1, a.reps + 1):
        for k in [int(x) for x in a.windows.split(',')]:
            prof = out / 'profiles' / f'{a.corpus}__heap__k{k}__{rep}.jsonl'
            args, env = command_line(a.dafny, 'verify', 'ws-default-on', a.project, [], prof, a.cores,
                                     ['--pipeline-profile-heap', '--verification-module-window', str(k)])
            res, _ = run_one(args, env, a.cwd or '.')
            events = load_events(prof)
            ordinal = 0
            for e in events:
                if e['ev'] != 'heap':
                    continue
                if e['where'] == 'module':
                    ordinal += 1
                w.row('heap', {'corpus': a.corpus, 'window': k, 'rep': rep, 'index': e['index'], 'where': e['where'],
                               'ordinal': ordinal if e['where'] == 'module' else 0, 'module': e.get('module', ''),
                               't_ms': e['t'], 'live_bytes': e['liveBytes'], 'heap_size_bytes': e['heapSizeBytes'],
                               'fragmented_bytes': e['fragmentedBytes'], 'committed_bytes': e['committedBytes'],
                               'heap_before_bytes': e['heapBytesBefore'], 'collect_ms': e['collectMs']})
            with open(prof, 'rb') as src, gzip.open(str(prof) + '.gz', 'wb') as dst:
                shutil.copyfileobj(src, dst)
            prof.unlink()
            print(f'heap K={k} rep {rep}: exit {res["exit"]} {res["wall_s"]:.1f}s peak RSS {res["maxrss_mb"]:.0f} MB',
                  flush=True)
    return 0


def gcdump_report(path):
    """(type, count, bytes) of `dotnet-gcdump report`, largest first: a type's rows (the report splits
    large objects into "(Bytes > 10K)" and "(Bytes > 1K)" rows) are summed, and assemblies dropped."""
    text = subprocess.run(['dotnet-gcdump', 'report', str(path)], capture_output=True, text=True).stdout
    agg = defaultdict(lambda: [0, 0])
    for line in text.splitlines():
        m = re.match(r'\s*([\d,]+)\s+([\d,]+)\s+(.+?)\s*$', line)
        if m:
            typ = re.sub(r'\s*\[[^\]]*\]$', '', m.group(3))
            typ = re.sub(r'\s*\(Bytes > [^)]*\)', '', typ)
            agg[typ][0] += int(m.group(2).replace(',', ''))
            agg[typ][1] += int(m.group(1).replace(',', ''))
    rows = sorted(((t, c, b) for t, (c, b) in agg.items()), key=lambda r: -r[2])
    m = re.search(r'^\s*([\d,]+)\s+GC Heap bytes', text, re.M)
    total = int(m.group(1).replace(',', '')) if m else sum(r[2] for r in rows)
    return text, rows, total


def cmd_gcdump(a):
    out = Path(a.out).resolve()
    dumps = out / 'gcdumps'
    dumps.mkdir(parents=True, exist_ok=True)
    w = Writer(out)
    wanted = a.points.split(',')
    for k in [int(x) for x in a.windows.split(',')]:
        hold = Path(tempfile.mkdtemp(prefix='hold'))
        prof = out / f'{a.corpus}__gcdump__k{k}.jsonl'
        args, env = command_line(a.dafny, 'verify', 'ws-default-on', a.project, [], prof, a.cores,
                                 ['--pipeline-profile-hold', str(hold), '--verification-module-window', str(k)])
        proc = subprocess.Popen(args, cwd=a.cwd or '.', env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        index, modules = 1, 0
        while proc.poll() is None:
            ready = hold / f'{index}.ready'
            if not ready.exists():
                time.sleep(0.05)
                continue
            pid, where = ready.read_text().strip().split('\t')
            if where == 'module':
                modules += 1
            name = where if where != 'module' else str(modules)
            if name in wanted:
                path = dumps / f'{a.corpus}__k{k}__{name}.gcdump'
                subprocess.run(['dotnet-gcdump', 'collect', '-p', pid, '-o', str(path)], capture_output=True)
                text, rows, total = gcdump_report(path)
                (dumps / f'{a.corpus}__k{k}__{name}.txt').write_text(text)
                for rank, (typ, count, size) in enumerate(rows[:a.top], 1):
                    w.row('gcdump', {'corpus': a.corpus, 'window': k, 'point': name, 'rank': rank, 'type': typ,
                                     'count': count, 'bytes': size, 'dump_total_bytes': total})
                print(f'gcdump K={k} at {name}: {total / 1e6:.0f} MB in {len(rows)} types', flush=True)
            (hold / f'{index}.go').write_text('')
            index += 1
        shutil.rmtree(hold, ignore_errors=True)
        if prof.exists():
            prof.unlink()
    return 0


# ---------------------------------------------------------------------------- statistics


def quantile(xs, q):
    """Linear-interpolation quantile (type 7) of sorted xs."""
    if not xs:
        return float('nan')
    pos = (len(xs) - 1) * q
    lo = int(pos)
    hi = min(lo + 1, len(xs) - 1)
    return xs[lo] + (xs[hi] - xs[lo]) * (pos - lo)


def describe(xs, rng, b=10000):
    """Median, IQR and a 95% percentile-bootstrap CI of the median."""
    xs = sorted(x for x in xs if x is not None)
    if not xs:
        return None
    meds = sorted(statistics.median(rng.choices(xs, k=len(xs))) for _ in range(b))
    return {'n': len(xs), 'median': statistics.median(xs), 'q1': quantile(xs, 0.25), 'q3': quantile(xs, 0.75),
            'lo': quantile(meds, 0.025), 'hi': quantile(meds, 0.975)}


def fmt(d, scale=1.0, digits=0):
    if d is None:
        return '–'
    f = lambda x: f'{x * scale:,.{digits}f}'
    return f"{f(d['median'])} [{f(d['q1'])}–{f(d['q3'])}] ({f(d['lo'])}–{f(d['hi'])})"


def read_csv(dirs, name):
    rows = []
    for d in dirs:
        for p in sorted(Path(d).rglob(name + '.csv')):
            with open(p) as f:
                rows.extend(csv.DictReader(f))
    return rows


def num(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


def critical_path(nodes, edges, weight):
    """Longest weighted path (sum of node weights) in the DAG; edge [a, b]: a after b."""
    deps = defaultdict(list)
    for a_, b_ in edges:
        if a_ != b_:
            deps[a_].append(b_)
    finish, level = {}, {}
    for n in sorted(nodes):  # ids are a topological order: dependencies first
        before = [finish[d] for d in deps[n] if d in finish]
        finish[n] = weight.get(n, 0.0) + max(before, default=0.0)
        level[n] = 1 + max((level[d] for d in deps[n] if d in level), default=0)
    return max(finish.values(), default=0.0), level


def cmd_analyze(a):
    rng = random.Random(a.seed)
    dirs = a.dirs
    runs = [r for r in read_csv(dirs, 'runs') if r['warmup'] == '0']
    stages = read_csv(dirs, 'stages')
    modules = read_csv(dirs, 'modules')
    gcs = read_csv(dirs, 'gc')
    heap = read_csv(dirs, 'heap')
    dumps = read_csv(dirs, 'gcdump')
    machines = {str(p.parent.name): json.loads(p.read_text()) for d in dirs for p in Path(d).rglob('machine.json')}
    L = []
    p = L.append
    p('## Machines\n')
    for name, m in sorted(machines.items()):
        gb = m.get('memoryBytes', 0) / 2 ** 30
        rt = ', '.join(l.split(' [')[0] for l in m.get('dotnet', '').splitlines() if 'NETCore' in l)
        p(f"- `{name}`: {m.get('cpu', '?')}, {m.get('cpus')} logical CPUs, {gb:.0f} GiB, {m.get('platform')}; "
          f"runtimes: {rt}")
    p('')
    p('Cells are **median [Q1–Q3] (95% bootstrap CI of the median)** over the measured rounds; '
      'the warm-up round is excluded. Times in ms, allocation in MB (10^6 bytes).\n')

    # --- per-run totals per corpus/config/rep (summed over the corpus's programs)
    per = defaultdict(lambda: defaultdict(lambda: [0.0, 0.0, 0.0, 0.0]))
    for r in stages:
        k = (r['corpus'], r['config'], r['rep'])
        s = per[k][r['stage']]
        s[0] += num(r['wall_ms']) or 0
        s[1] += num(r['cpu_ms']) or 0
        s[2] += num(r['alloc_bytes']) or 0
        s[3] += num(r['gc_pause_ms']) or 0
    wall = defaultdict(lambda: defaultdict(float))
    for r in runs:
        wall[(r['corpus'], r['config'])][r['rep']] += float(r['wall_s']) * 1000
    corpora = sorted({r['corpus'] for r in runs})
    order = ['process', 'span:roots', 'span:parse', 'parse.file', 'parse.doo', 'span:parse.clone', 'span:resolve',
             'resolve.prefixNamed', 'resolve.bind', 'resolve.system', 'resolve.global.preResolve', 'resolve.module',
             'resolve.module.user', 'resolve.module.library', 'resolve.global.replaceable',
             'resolve.global.postResolve', 'resolve.global.cardinality', 'resolve.verifiables', 'translate.module',
             'span:translate.prelude', 'span:prepare']
    for corpus in corpora:
        configs = sorted({r['config'] for r in runs if r['corpus'] == corpus})
        nprog = len({r['program'] for r in runs if r['corpus'] == corpus})
        p(f'## {corpus}\n')
        p(f'{nprog} program(s). Runs: ' + ', '.join(
            f"`{c}` {len(wall[(corpus, c)])}" for c in configs) + '.\n')
        p('### Process wall time (ms), all configurations\n')
        p('| configuration | wall |')
        p('|---|---|')
        for c in configs:
            p(f'| `{c}` | {fmt(describe(list(wall[(corpus, c)].values()), rng))} |')
        p('')
        # paired ratios
        pairs = [(x, y) for x in configs for y in configs if x != y and paired_interest(x, y)]
        if pairs:
            p('### Paired ratios of process wall time\n')
            p('Ratio r_i = wall(B, round i) / wall(A, round i), for rounds where both ran; median and 95% bootstrap CI '
              'of the median ratio.\n')
            p('| A | B | n | median ratio B/A | 95% CI |')
            p('|---|---|---|---|---|')
            for x, y in pairs:
                reps = sorted(set(wall[(corpus, x)]) & set(wall[(corpus, y)]))
                ratios = [wall[(corpus, y)][i] / wall[(corpus, x)][i] for i in reps]
                d = describe(ratios, rng)
                if d:
                    p(f"| `{x}` | `{y}` | {d['n']} | {d['median']:.4f} | {d['lo']:.4f}–{d['hi']:.4f} |")
            p('')
        prof_configs = [c for c in configs if any(k[0] == corpus and k[1] == c for k in per)]
        for c in prof_configs:
            reps = [k for k in per if k[0] == corpus and k[1] == c]
            p(f'### Front-end breakdown, `{c}`\n')
            p('Wall and thread CPU of each stage, summed over its events (and programs) per run. `span:` rows are '
              'the existing stage spans (process-wide CPU); the others are front-end events with the CPU and '
              'allocation of the thread that ran them.\n')
            p('| stage | events/run | wall ms | thread CPU ms | alloc MB | GC pause ms |')
            p('|---|---|---|---|---|---|')
            names = order + sorted({s for k in reps for s in per[k] if s not in order and not s.startswith('phase:')})
            for s in names:
                vals = [per[k][s] for k in reps if s in per[k]]
                if not vals:
                    continue
                n = statistics.median([cnt for cnt in count_events(stages, corpus, c, s)] or [0])
                p(f'| {s} | {n:,.0f} | {fmt(describe([v[0] for v in vals], rng))} | '
                  f'{fmt(describe([v[1] for v in vals], rng))} | '
                  f'{fmt(describe([v[2] for v in vals], rng), 1e-6)} | {fmt(describe([v[3] for v in vals], rng))} |')
            p('')
            ph = sorted({s for k in reps for s in per[k] if s.startswith('phase:')},
                        key=lambda s: -statistics.median([per[k][s][0] for k in reps if s in per[k]]))
            if ph:
                p(f'Phases inside `resolve.module` (summed over modules), `{c}`:\n')
                p('| phase | wall ms | thread CPU ms | alloc MB |')
                p('|---|---|---|---|')
                for s in ph[:12]:
                    vals = [per[k][s] for k in reps if s in per[k]]
                    p(f'| {s[6:]} | {fmt(describe([v[0] for v in vals], rng))} | '
                      f'{fmt(describe([v[1] for v in vals], rng))} | {fmt(describe([v[2] for v in vals], rng), 1e-6)} |')
                p('')
        analyze_graphs(a, dirs, corpus, modules, per, p, rng)
        analyze_gc(corpus, gcs, p, rng)
    analyze_heap(heap, dumps, p, rng)
    Path(a.out).write_text('\n'.join(L) + '\n')
    print('\n'.join(L))
    return 0


def paired_interest(x, y):
    gx, rx, px = parse_config(x)
    gy, ry, py = parse_config(y)
    same = [gx == gy, rx == ry, px == py]
    if sum(same) != 2:
        return False
    if px != py:  # profile overhead: B is the profiled one
        return px == 'off' or (px == 'on' and py == 'gcev')
    if gx != gy:
        return gx == 'ws'
    return rx in ('legacy', 'default') and ry == 'refresh'


def count_events(stages, corpus, config, stage):
    per = defaultdict(int)
    for r in stages:
        if r['corpus'] == corpus and r['config'] == config and r['stage'] == stage:
            per[r['rep']] += int(r['n'])
    return list(per.values())


def analyze_graphs(a, dirs, corpus, modules, per, p, rng):
    graphs = []
    for d in dirs:
        graphs += [json.loads(g.read_text()) for g in Path(d).rglob(f'graphs/{corpus}__*.json')]
    if not graphs:
        return
    for config in sorted({m['config'] for m in modules if m['corpus'] == corpus}):
        _, _, prof = parse_config(config)
        if prof == 'gcev':
            continue
        p(f'### Module graph and the ideal parallel bound, `{config}`\n')
        p('Measured: r_m, the median over rounds of module m\'s `resolve.module` wall time (it includes the GC '
          'pauses that happened on that thread). Derived:\n'
          '- W_R = Σ_m r_m;\n'
          '- L_R = max over paths of Σ r_m along the path, over the graph of imports, refinement, exports and '
          'nesting (a parent depends on its submodules);\n'
          '- R_global = median(`span:resolve`) − W_R: everything in resolution that is not one module '
          '(binding, the system module, global rewriter passes, cardinality, finding the verifiable symbols);\n'
          '- T(N) = R_global + max(W_R / N, L_R), the ideal resolution time with N workers (no contention, no GC '
          'interference, free scheduling); S(N) = median(`span:resolve`) / T(N).\n')
        rows = []
        for g in graphs:
            prog = g['program']
            ms = [m for m in modules if m['corpus'] == corpus and m['config'] == config and m['program'] == prog]
            if not ms:
                continue
            byid = defaultdict(list)
            for m in ms:
                byid[int(m['id'])].append(float(m['wall_ms']))
            weight = {i: statistics.median(v) for i, v in byid.items()}
            nodes = [n['id'] for n in g['nodes']]
            lib = {n['id']: n['library'] for n in g['nodes']}
            W = sum(weight.values())
            Lr, level = critical_path(nodes, g['edges'], weight)
            reps = [k for k in per if k[0] == corpus and k[1] == config]
            resolve_spans = []
            for r in sorted({m['rep'] for m in ms}):
                tot = sum(float(m['wall_ms']) for m in ms if m['rep'] == r)
                resolve_spans.append(tot)
            res = [float(row) for row in program_stage(a, corpus, config, prog, 'span:resolve')]
            R = statistics.median(res) if res else W
            rows.append({'program': prog, 'nodes': len(nodes), 'edges': len(g['edges']), 'W': W, 'L': Lr,
                         'R': R, 'Rg': R - W, 'level': level, 'weight': weight, 'lib': lib,
                         'Wlib': sum(w for i, w in weight.items() if lib.get(i))})
        if not rows:
            continue
        if len(rows) == 1:
            r = rows[0]
            p(f"Graph: {r['nodes']} module declarations, {r['edges']} edges, "
              f"{max(r['level'].values())} levels. Library modules' share of W_R: {r['Wlib']:,.0f} ms "
              f"({100 * r['Wlib'] / r['W'] if r['W'] else 0:.1f}%).\n")
            p('| quantity | value (ms) |')
            p('|---|---|')
            p(f"| median span:resolve | {r['R']:,.0f} |")
            p(f"| W_R | {r['W']:,.0f} |")
            p(f"| L_R | {r['L']:,.0f} |")
            p(f"| R_global | {r['Rg']:,.0f} |")
            p(f"| W_R / L_R (average parallelism) | {r['W'] / r['L'] if r['L'] else 0:.2f} |")
            p('')
            p('| N | T(N) ms | S(N) |')
            p('|---|---|---|')
            for n in (1, 2, 4, 8, 18):
                t = r['Rg'] + max(r['W'] / n, r['L'])
                p(f"| {n} | {t:,.0f} | {r['R'] / t:.2f} |")
            p('')
            levels = defaultdict(lambda: [0, 0.0])
            for i, lv in r['level'].items():
                levels[lv][0] += 1
                levels[lv][1] += r['weight'].get(i, 0.0)
            p('Width profile (level = longest chain of dependencies below a module, by count): modules and '
              'their summed r_m per level.\n')
            p('| level | modules | Σ r_m (ms) |')
            p('|---|---|---|')
            for lv in sorted(levels):
                p(f'| {lv} | {levels[lv][0]} | {levels[lv][1]:,.0f} |')
            p('')
            top = sorted(r['weight'].items(), key=lambda kv: -kv[1])[:8]
            names = {}
            for m in modules:
                if m['corpus'] == corpus and m['program'] == r['program']:
                    names[int(m['id'])] = m['module']
            p('Heaviest modules: ' + '; '.join(f'`{names.get(i, i)}` {w:,.0f} ms' for i, w in top) + '.\n')
        else:
            p(f'{len(rows)} programs. Distribution over programs (median [Q1–Q3], max):\n')
            p('| quantity | median | Q1–Q3 | max |')
            p('|---|---|---|---|')
            for name, f in [('module declarations', lambda r: r['nodes']), ('W_R ms', lambda r: r['W']),
                            ('L_R ms', lambda r: r['L']), ('R_global ms', lambda r: r['Rg']),
                            ('W_R / L_R', lambda r: r['W'] / r['L'] if r['L'] else 1.0),
                            ('S(4)', lambda r: r['R'] / (r['Rg'] + max(r['W'] / 4, r['L']))),
                            ('S(18)', lambda r: r['R'] / (r['Rg'] + max(r['W'] / 18, r['L'])))]:
                xs = sorted(f(r) for r in rows)
                p(f'| {name} | {statistics.median(xs):,.2f} | {quantile(xs, .25):,.2f}–{quantile(xs, .75):,.2f} '
                  f'| {xs[-1]:,.2f} |')
            tw = sum(r['W'] for r in rows)
            tr = sum(r['R'] for r in rows)
            p(f'\nOver the corpus: Σ W_R = {tw:,.0f} ms of Σ span:resolve = {tr:,.0f} ms.\n')


_stage_cache = {}


def program_stage(a, corpus, config, program, stage):
    key = tuple(a.dirs)
    if key not in _stage_cache:
        _stage_cache[key] = read_csv(a.dirs, 'stages')
    return [r['wall_ms'] for r in _stage_cache[key] if r['corpus'] == corpus and r['config'] == config
            and r['program'] == program and r['stage'] == stage]


def analyze_gc(corpus, gcs, p, rng):
    rows = [g for g in gcs if g['corpus'] == corpus]
    if not rows:
        return
    p('### GC (runtime GC events, `gcev` configurations)\n')
    p('Per run, summed over the corpus\'s programs. "Front end" counts the pauses that ended before the end of '
      'resolution. Pause = one execution-engine suspension (GCSuspendEEBegin to GCRestartEEEnd).\n')
    p('| configuration | collections | gen2 | pause total ms | pause max ms | front-end pause ms | allocated MB |')
    p('|---|---|---|---|---|---|---|')
    for c in sorted({g['config'] for g in rows}):
        per = defaultdict(lambda: defaultdict(float))
        for g in rows:
            if g['config'] != c:
                continue
            for k in ('gcs', 'gen2', 'pause_ms_total', 'pause_ms_frontend', 'alloc_bytes'):
                per[g['rep']][k] += num(g[k]) or 0
            per[g['rep']]['pause_ms_max'] = max(per[g['rep']]['pause_ms_max'], num(g['pause_ms_max']) or 0)
        col = lambda k, s=1.0: fmt(describe([v[k] for v in per.values()], rng), s)
        p(f"| `{c}` | {col('gcs')} | {col('gen2')} | {col('pause_ms_total')} | {col('pause_ms_max')} | "
          f"{col('pause_ms_frontend')} | {col('alloc_bytes', 1e-6)} |")
    p('')


def analyze_heap(heap, dumps, p, rng):
    if heap:
        p('## Retention: live heap after a forced full compacting GC\n')
        p('`dafny verify` with the no-op solver and `--pipeline-profile-heap`, at module window K. "module n" is the '
          'n-th module whose results were reported and released. Median over runs (min–max), MB.\n')
        for corpus in sorted({h['corpus'] for h in heap}):
            ks = sorted({int(h['window']) for h in heap if h['corpus'] == corpus})
            pts = []
            for h in heap:
                if h['corpus'] != corpus:
                    continue
                key = (h['where'], int(h['ordinal']))
                if key not in pts:
                    pts.append(key)
            p(f'### {corpus}\n')
            p('| point | ' + ' | '.join(f'K={k} live MB' for k in ks) + ' |')
            p('|---|' + '---|' * len(ks))
            nmod = max((o for w, o in pts if w == 'module'), default=0)
            show = [pt for pt in pts if pt[0] != 'module' or pt[1] in
                    {1, 2, 5, 10, 20, nmod // 2, nmod - 1, nmod}]
            for where, o in show:
                cells = []
                for k in ks:
                    xs = sorted(float(h['live_bytes']) / 1e6 for h in heap if h['corpus'] == corpus
                                and int(h['window']) == k and h['where'] == where and int(h['ordinal']) == o)
                    cells.append(f'{statistics.median(xs):,.0f} ({xs[0]:,.0f}–{xs[-1]:,.0f})' if xs else '–')
                p(f"| {where}{' ' + str(o) if where == 'module' else ''} | " + ' | '.join(cells) + ' |')
            p('')
            for k in ks:
                xs = [float(h['live_bytes']) / 1e6 for h in heap if h['corpus'] == corpus and int(h['window']) == k
                      and h['where'] == 'module']
                if xs:
                    p(f'K={k}: over all module points, live heap median {statistics.median(xs):,.0f} MB, '
                      f'max {max(xs):,.0f} MB.')
            p('')
    if dumps:
        p('## Dominant types in heap dumps (dotnet-gcdump)\n')
        p('Top types by bytes in each dump (`dotnet-gcdump report`, a type\'s size classes summed); a gcdump holds '
          'the live objects only. GC root paths are not in the CLI report; the .gcdump files are in the artifact '
          'for PerfView or Visual Studio.\n')
        for key in sorted({(d['corpus'], d['window'], d['point']) for d in dumps},
                          key=lambda t: (t[0], int(t[1]), t[2])):
            rows = [d for d in dumps if (d['corpus'], d['window'], d['point']) == key]
            total = float(rows[0]['dump_total_bytes'])
            p(f"**{key[0]}, K={key[1]}, at {key[2]}** ({total / 1e6:,.0f} MB in all): " + '; '.join(
                f"`{d['type']}` {float(d['bytes']) / 1e6:,.1f} MB ({int(d['count']):,})" for d in rows[:10]) + '.\n')


# ---------------------------------------------------------------------------- check


def cmd_check(a):
    events = load_events(a.profile)
    out = []
    kinds = sorted({e['ev'] for e in events})
    out.append('kinds: ' + ', '.join(kinds))
    fe = [e for e in events if e['ev'] == 'fe']
    counts = defaultdict(int)
    for e in fe:
        counts[e['stage']] += 1
    for stage in sorted(counts):
        out.append(f'{stage}: {counts[stage]}')
    ok = all(e['start'] <= e['end'] and e['threadAllocBytes'] >= 0 and e['gcPauseMs'] >= 0
             and (e['threadCpuMs'] is None or e['threadCpuMs'] >= 0) for e in fe)
    out.append(f'front-end events well-formed: {ok}')
    out.append('thread CPU recorded: ' + str(all(e['threadCpuMs'] is not None for e in fe)))
    graph = next((e for e in events if e['ev'] == 'moduleGraph'), None)
    if graph:
        out.append(f"module graph: {len(graph['nodes'])} nodes, {len(graph['edges'])} edges")
        names = {n['id']: n['module'] for n in graph['nodes']}
        for n in graph['nodes']:
            out.append(f"  {n['id']} {n['kind']} {n['module']}{' (library)' if n['library'] else ''}")
        for x, y in graph['edges']:
            out.append(f'  {names[x]} -> {names[y]}')
        mods = [e for e in fe if e['stage'] == 'resolve.module']
        same = [(e['id'], e['module']) for e in mods] == [(n['id'], n['module']) for n in graph['nodes']]
        out.append(f'resolve.module events follow the graph: {same}')
        literal = next((e for e in mods if e['kind'] == 'module' and e.get('phases')), None)
        if literal:
            out.append('phases of a module: ' + ', '.join(p['phase'] for p in literal['phases']))
    heap = [e for e in events if e['ev'] == 'heap']
    if heap:
        out.append('heap points: ' + ', '.join(e['where'] + (':' + e['module'] if e.get('module') else '')
                                               for e in heap))
        out.append('heap points measured: ' + str(all(e.get('liveBytes', 0) > 0 for e in heap)))
    gcs = [e for e in events if e['ev'] in ('gc', 'gcpause')]
    if 'gcEvents' in events[0] and events[0]['gcEvents']:
        out.append('gc events well-formed: ' + str(all(
            (e['ev'] == 'gcpause' and e['ms'] >= 0) or (e['ev'] == 'gc' and e['end'] >= e['start']) for e in gcs)))
    summary = [e for e in events if e['ev'] == 'summary']
    out.append(f"summary: {summary[-1]['reason'] if summary else None}")
    print('\n'.join(out))
    return 0


# ---------------------------------------------------------------------------- main


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    for name in ('run', 'heap', 'gcdump'):
        s = sub.add_parser(name)
        s.add_argument('--dafny', required=True)
        s.add_argument('--out', required=True)
        s.add_argument('--corpus', required=True)
        s.add_argument('--project')
        s.add_argument('--cwd')
        s.add_argument('--cores', type=int, default=os.cpu_count())
        s.add_argument('--seed', type=int, default=1)
        if name == 'run':
            s.add_argument('--configs', required=True)
            s.add_argument('--reps', type=int, default=10)
            s.add_argument('--plan')
            s.add_argument('--litdir')
            s.add_argument('--command', default='resolve', choices=['resolve', 'verify'])
            s.add_argument('--shard', default='0/1', help='I/N: every N-th program of the plan, from the I-th')
        else:
            s.add_argument('--windows', default='1,4')
            s.add_argument('--reps', type=int, default=3)
            s.add_argument('--points', default='parsed,resolved,1,20,43,80')
            s.add_argument('--top', type=int, default=25)
    s = sub.add_parser('check')
    s.add_argument('profile')
    s = sub.add_parser('analyze')
    s.add_argument('dirs', nargs='+')
    s.add_argument('--out', required=True)
    s.add_argument('--seed', type=int, default=1)
    a = ap.parse_args(argv)
    if a.cmd == 'run':
        if a.project:
            a.project = str(Path(a.project).resolve()) if not a.cwd else a.project
        return cmd_run(a)
    if a.cmd == 'heap':
        return cmd_heap(a)
    if a.cmd == 'gcdump':
        return cmd_gcdump(a)
    if a.cmd == 'check':
        return cmd_check(a)
    return cmd_analyze(a)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
