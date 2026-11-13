#!/usr/bin/env python3
"""Does a persistent per-module resolution cache pay for itself?  For
https://github.com/erniecohen/dafny/issues/261 (spec: https://github.com/erniecohen/dafny/pull/282).

    cache-payoff.py run     --dafny D --out DIR --name NAME [--reps 3] [--warmup 1] [--cwd DIR] -- DAFNY-ARGS...
    cache-payoff.py analyze DIR... --out REPORT.md [--json REPORT.json]

`run` runs `D DAFNY-ARGS --pipeline-profile ...` --warmup times (discarded) and then --reps
times, and records each run's wall time (process start to exit) in DIR/runs.tsv and its
profile in DIR/NAME-<i>.jsonl.  The exit code is recorded, not judged: a front-end-only
`dafny verify` with Boogie's no-op solver reports every task as inconclusive.

`analyze` reads every profile and reports, per run name, medians over the measured runs of:

    wall        the process's wall time
    parse       the `roots`, `parse` and `parse.clone` spans
    resolve     the `resolve` span
    modules     the union of the per-module resolution intervals (`fe` resolve.module); with
                --resolution-threads they overlap, so `module work` (their sum) is also given
    library     the part of `module work` spent on modules from library (.doo) files
    global      resolve - modules: system module, bind, global passes, verifiables
    translate   the union of `translate.prelude` and `translate` spans
    prepare     the union of `prepare` spans (Boogie's preparation of a module's tasks)
    vcgen       the union of the tasks' running-to-completed intervals; under the no-op solver
                this is Boogie's VC generation and SMT emission, with no solver time
    gc pause    the process's total GC pause time (it overlaps the stages above)

and the savings a warm persistent per-module cache could give, from the module graph of the
profile (`moduleGraph`: edge [a, b] means a depends on b).  An edit to module m invalidates
m and every module that depends on it, transitively: D(m).  Every other module's resolution
is assumed to be a free cache hit (an upper bound: the lookup, the snapshot's
materialization and the rebinding of https://github.com/erniecohen/dafny/pull/282 cost
nothing).  Translation reuse is bounded the same way, for the modules outside D(m) (an upper
bound: the global projection G of https://github.com/erniecohen/dafny/pull/272 can
invalidate more).  Edits considered: the median over all user modules (a random edit), the
median over the leaves (user modules no other user module depends on) and the root (the
user module with the most dependents).  Library modules are never edited, so they are always
hits.  A saving's share of wall time is the saved module work divided by the module work,
times the modules' wall time (the modules interval union), divided by the wall time.
"""
import argparse
import json
import os
import statistics
import subprocess
import sys
import time
from collections import defaultdict
from pathlib import Path


def load(path):
    with open(path) as f:
        return [json.loads(line) for line in f if line.strip()]


def union(intervals):
    total, end = 0.0, None
    for s, e in sorted(intervals):
        if end is None or s > end:
            total += e - s
            end = e
        elif e > end:
            total += e - end
            end = e
    return total


def run(a):
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    args = a.args[1:] if a.args and a.args[0] == '--' else a.args
    tsv = out / 'runs.tsv'
    if not tsv.exists():
        tsv.write_text('name\trep\texit\twall_s\n')
    for rep in range(1 - a.warmup, a.reps + 1):
        profile = out / f'{a.name}-{rep}.jsonl'
        cmd = [a.dafny, *args, '--pipeline-profile', str(profile.resolve())]
        t0 = time.monotonic()
        p = subprocess.run(cmd, cwd=a.cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        wall = time.monotonic() - t0
        if rep == 1 - a.warmup:
            (out / f'{a.name}.out').write_bytes(p.stdout)
        if b'resolution/type errors detected' in p.stdout or b'parse errors detected' in p.stdout:
            sys.exit(f'{a.name}: the program does not resolve; see {a.name}.out')
        if rep <= 0:
            profile.rename(out / f'{a.name}-warmup{-rep}.jsonl')
            print(f'{a.name}: warm-up exit {p.returncode}, {wall:.1f} s', flush=True)
            continue
        with open(tsv, 'a') as f:
            f.write(f'{a.name}\t{rep}\t{p.returncode}\t{wall:.3f}\n')
        print(f'{a.name}: run {rep} exit {p.returncode}, {wall:.1f} s', flush=True)
        events = load(profile)
        if not any(e['ev'] == 'summary' for e in events):
            sys.exit(f'{a.name}: profile {profile} has no summary')


def closure_dependents(n, edges):
    rev = defaultdict(set)
    for a, b in edges:
        rev[b].add(a)
    deps = []
    for m in range(n):
        seen, stack = {m}, [m]
        while stack:
            x = stack.pop()
            for y in rev[x]:
                if y not in seen:
                    seen.add(y)
                    stack.append(y)
        deps.append(seen)
    return deps


def analyze_profile(path, wall_s):
    events = load(path)
    manifest = events[0]
    spans = [e for e in events if e['ev'] == 'span']
    fe = [e for e in events if e['ev'] == 'fe']
    graph = next(e for e in events if e['ev'] == 'moduleGraph')
    nodes = graph['nodes']
    mods = [e for e in fe if e['stage'] == 'resolve.module']
    cost = defaultdict(float)
    for e in mods:
        cost[e['id']] += e['end'] - e['start']
    resolve = sum(s['end'] - s['start'] for s in spans if s['stage'] == 'resolve')
    modules_wall = union([(e['start'], e['end']) for e in mods])
    work = sum(cost.values())
    library = sum(c for i, c in cost.items() if nodes[i]['library'])
    tr = defaultdict(float)
    for s in spans:
        if s['stage'] in ('translate', 'translate.prelude'):
            tr[s['module']] += s['end'] - s['start']
    r = {
        'wall': wall_s * 1000,
        'parse': union([(s['start'], s['end']) for s in spans if s['stage'] in ('roots', 'parse', 'parse.clone')]),
        'resolve': resolve,
        'modules': modules_wall,
        'module work': work,
        'library': library,
        'global': resolve - modules_wall,
        'translate': union([(s['start'], s['end']) for s in spans if s['stage'] in ('translate', 'translate.prelude')]),
        'prepare': union([(s['start'], s['end']) for s in spans if s['stage'] == 'prepare']),
        'vcgen': union([(t['running'], t['completed']) for t in events
                        if t['ev'] == 'task' and t.get('running') is not None and t.get('completed') is not None]),
        'vcgen work': sum(t.get('solveMs') or 0 for t in events if t['ev'] == 'task'),
        'tasks': sum(1 for t in events if t['ev'] == 'task'),
        'gc pause': next((e.get('gcPauseMs') or 0 for e in reversed(events) if e['ev'] == 'summary'), 0),
        'threads': manifest.get('resolutionThreads'),
        'refresh': manifest.get('typeSystemRefresh'),
        'cores': manifest.get('processorCount'),
    }
    user = [i for i, n in enumerate(nodes) if n['kind'] == 'module' and not n['library'] and n['module'] != '_module']
    r['modules (user)'] = len(user)
    r['modules (library)'] = sum(1 for n in nodes if n['kind'] == 'module' and n['library'])
    r['graph nodes'] = len(nodes)
    r['graph edges'] = len(graph['edges'])
    deps = closure_dependents(len(nodes), graph['edges'])
    userset = set(user)
    # Translation is reported per module by its name; graph names of nested modules are dotted.
    name_of = {i: nodes[i]['module'] for i in range(len(nodes))}
    tr_total = sum(tr.values())

    def saving(m):
        d = deps[m]
        res = sum(c for i, c in cost.items() if i not in d)
        inval_names = {name_of[i] for i in d if nodes[i]['kind'] == 'module'}
        trs = sum(t for name, t in tr.items() if name not in inval_names)
        return res, trs, len(d & userset)

    per = {m: saving(m) for m in user}
    leaves = [m for m in user if len(deps[m] & userset) == 1]
    root = max(user, key=lambda m: (len(deps[m] & userset), cost[m])) if user else None
    r['leaves'] = len(leaves)
    r['root'] = name_of[root] if root is not None else None
    r['root dependents'] = per[root][2] - 1 if root is not None else 0
    r['dependents (median)'] = statistics.median(per[m][2] - 1 for m in user) if user else 0
    r['translate work'] = tr_total
    for label, ms in (('random', user), ('leaf', leaves), ('root', [root] if root is not None else [])):
        if not ms:
            continue
        res = statistics.median(per[m][0] for m in ms)
        trs = statistics.median(per[m][1] for m in ms)
        r[f'saved resolve work ({label})'] = res
        r[f'saved resolve wall ({label})'] = res / work * modules_wall if work else 0
        r[f'saved translate ({label})'] = trs
    return r


def analyze(a):
    rows = defaultdict(list)
    for d in a.dirs:
        for tsv in Path(d).rglob('runs.tsv'):
            for line in tsv.read_text().splitlines()[1:]:
                name, rep, _, wall = line.split('\t')
                rows[name].append(analyze_profile(tsv.parent / f'{name}-{rep}.jsonl', float(wall)))
    report = {}
    for name in sorted(rows):
        rs = rows[name]
        med = {}
        for k in rs[0]:
            vals = [r[k] for r in rs if k in r]
            if vals and isinstance(vals[0], (int, float)) and not isinstance(vals[0], bool):
                med[k] = statistics.median(vals)
            else:
                med[k] = vals[0] if vals else None
        med['runs'] = len(rs)
        report[name] = med
    lines = ['# Persistent per-module cache: what could it save?', '',
             'Medians over the measured runs; times in seconds; a share is of the process wall time.', '']
    lines += ['| run | runs | wall | parse | resolve | modules (wall) | module work | library | global | translate | prepare | vcgen (tasks) | gc pause |',
              '|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|']

    def s(x):
        return f'{x / 1000:.2f}'

    def pct(x, w):
        return f'{100 * x / w:.0f}%'
    for name, r in report.items():
        w = r['wall']
        lines.append(f"| {name} | {r['runs']} | {s(w)} | {s(r['parse'])} ({pct(r['parse'], w)}) | "
                     f"{s(r['resolve'])} ({pct(r['resolve'], w)}) | {s(r['modules'])} ({pct(r['modules'], w)}) | "
                     f"{s(r['module work'])} | {s(r['library'])} | {s(r['global'])} ({pct(r['global'], w)}) | "
                     f"{s(r['translate'])} ({pct(r['translate'], w)}) | {s(r['prepare'])} ({pct(r['prepare'], w)}) | "
                     f"{s(r['vcgen'])} ({pct(r['vcgen'], w)}, {r['tasks']}) | {s(r['gc pause'])} ({pct(r['gc pause'], w)}) |")
    lines += ['', '| run | resolver, threads | user modules | library modules | graph nodes / edges | leaves | root (dependents) | median dependents |',
              '|---|---|---:|---:|---:|---:|---|---:|']
    for name, r in report.items():
        lines.append(f"| {name} | {'refresh' if r['refresh'] else 'legacy'}, {r['threads']} | {r['modules (user)']} | "
                     f"{r['modules (library)']} | {r['graph nodes']} / {r['graph edges']} | {r['leaves']} | "
                     f"{r['root']} ({r['root dependents']}) | {r['dependents (median)']} |")
    lines += ['', 'Upper bound on what a warm cache saves, per edit (resolution wall saved; translation saved where translated):', '',
              '| run | edit leaf | edit random | edit root |', '|---|---:|---:|---:|']
    for name, r in report.items():
        w = r['wall']
        cells = []
        for label in ('leaf', 'random', 'root'):
            k = f'saved resolve wall ({label})'
            if k not in r:
                cells.append('')
                continue
            c = f"{s(r[k])} ({pct(r[k], w)})"
            if r['translate']:
                t = r[f'saved translate ({label})']
                c += f" + tr {s(t)} ({pct(t, w)})"
            cells.append(c)
        lines.append(f"| {name} | {' | '.join(cells)} |")
    Path(a.out).write_text('\n'.join(lines) + '\n')
    if a.json:
        Path(a.json).write_text(json.dumps(report, indent=1))
    print('\n'.join(lines))


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest='cmd', required=True)
    r = sub.add_parser('run')
    r.add_argument('--dafny', required=True)
    r.add_argument('--out', required=True)
    r.add_argument('--name', required=True)
    r.add_argument('--reps', type=int, default=3)
    r.add_argument('--warmup', type=int, default=1)
    r.add_argument('--cwd')
    r.add_argument('args', nargs=argparse.REMAINDER)
    an = sub.add_parser('analyze')
    an.add_argument('dirs', nargs='+')
    an.add_argument('--out', required=True)
    an.add_argument('--json')
    a = p.parse_args()
    run(a) if a.cmd == 'run' else analyze(a)


if __name__ == '__main__':
    main()
