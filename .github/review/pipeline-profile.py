#!/usr/bin/env python3
"""Driver for `dafny verify --pipeline-profile <file>` (#241, milestone D0).

    pipeline-profile.py run --dafny DAFNY --out DIR [--repeat R] [--sample S] -- VERIFY-ARGS...
    pipeline-profile.py summary PROFILE.jsonl [--title T]
    pipeline-profile.py check PROFILE.jsonl

`run` runs `DAFNY verify VERIFY-ARGS` R times without the option and R times with it,
alternating (off, on, off, on, ...), and writes to DIR:

    off-<i>.out, on-<i>.out     each run's standard output and error, merged
    on-<i>.jsonl                each profiled run's profile
    ps-<mode>-<i>.tsv           the process tree sampled every S seconds (default 0.5) with
                                `ps -axo pid=,ppid=,%cpu=,rss=,comm=`: time, root/child CPU
                                percent and RSS, summed over the root's descendants
    runs.tsv                    per run: mode, exit code, wall seconds, user and system CPU
                                seconds of the process and its reaped descendants (Z3
                                included), peak RSS of the largest one
    summary.md                  output identity, overhead and the last profile's summary

It fails (exit 1) only if a profiled run's output differs from the unprofiled runs' output
or a run's exit code differs; timing is reported, never judged.  Run counters come from
getrusage(RUSAGE_CHILDREN), which covers descendants that were waited for; peak RSS is
the largest single process's, not a simultaneous sum (the ps samples give that).

`summary` prints Markdown tables of a profile: front-end stages; per-module translation and
Boogie preparation; tasks' queue wait and solve; and the stretches after resolution in
which no solver task was running, apportioned to the translation stages and GC that
overlap them.  `check` prints a deterministic digest of a profile's structure, for tests.

Times in a profile are milliseconds from when the profile was opened (see the manifest).
"""
import argparse
import json
import os
import platform
import resource
import statistics
import subprocess
import sys
import threading
import time

FRONT = ['roots', 'parse', 'parse.clone', 'resolve']
MODULE_STAGES = ['translate.wait', 'translate.prelude', 'translate', 'prepare']


def load(path):
    with open(path) as f:
        return [json.loads(line) for line in f if line.strip()]


def by_kind(events, kind):
    return [e for e in events if e.get('ev') == kind]


def fmt(x, digits=0):
    if x is None:
        return ''
    return f'{x:,.{digits}f}'


def mb(n):
    return None if n is None else n / 1e6


# ---------------------------------------------------------------------------- summary

def union_length(intervals):
    total, end = 0.0, None
    for s, e in sorted(intervals):
        if end is None or s > end:
            total += e - s
            end = e
        elif e > end:
            total += e - end
            end = e
    return total


def overlap(a, b):
    return max(0.0, min(a[1], b[1]) - max(a[0], b[0]))


def idle_intervals(tasks, start, end):
    """Intervals in [start, end] in which no task was between Running and Completed."""
    points = []
    for t in tasks:
        if t.get('running') is not None:
            points.append((t['running'], 1))
            points.append((t['completed'], -1))
    points.sort(key=lambda p: (p[0], -p[1]))
    idle, active, last = [], 0, start
    for time_, delta in points:
        if time_ < start:
            active += delta
            continue
        if time_ > end:
            break
        if active == 0 and time_ > last:
            idle.append((last, time_))
        active += delta
        if active == 0:
            last = time_
    if active == 0 and end > last:
        idle.append((last, end))
    return idle


def busy_profile(tasks, start, end):
    """Time-weighted mean number of running tasks over [start, end], and the time at each level."""
    points = []
    for t in tasks:
        if t.get('running') is not None:
            points.append((max(t['running'], start), 1))
            points.append((min(t['completed'], end), -1))
    points.sort(key=lambda p: (p[0], p[1]))
    level, last, area, at = 0, start, 0.0, {}
    for time_, delta in points:
        if time_ > last:
            area += level * (time_ - last)
            at[level] = at.get(level, 0.0) + time_ - last
            last = time_
        level += delta
    if end > last:
        at[level] = at.get(level, 0.0) + end - last
    span = end - start
    return (area / span if span > 0 else 0.0), at


def summary(events, title=None):
    out = []
    manifest = (by_kind(events, 'manifest') or [{}])[0]
    spans = by_kind(events, 'span')
    tasks = by_kind(events, 'task')
    modules = by_kind(events, 'module')
    final = (by_kind(events, 'summary') or [{}])[-1]
    if title:
        out.append(f'### {title}\n')
    out.append(f"Dafny {manifest.get('dafnyVersion')}, {manifest.get('runtime')}, {manifest.get('os')} "
               f"({manifest.get('arch')}), {manifest.get('processorCount')} processors, "
               f"--cores {manifest.get('cores')}, server GC {manifest.get('serverGC')}, "
               f"type-system-refresh {manifest.get('typeSystemRefresh')}.  The profile opened "
               f"{fmt(manifest.get('sinceProcessStartMs'))} ms after the process started.\n")

    total_ms = final.get('t')
    out.append('| whole run | value |')
    out.append('|---|---:|')
    out.append(f"| wall, profile open to summary (s) | {fmt((total_ms or 0) / 1000, 2)} |")
    out.append(f"| Dafny process CPU (s) | {fmt((final.get('cpuMs') or 0) / 1000, 2)} |")
    out.append(f"| allocated (MB) | {fmt(mb(final.get('allocBytes')))} |")
    out.append(f"| GC count gen0/gen1/gen2 | {'/'.join(str(x) for x in final.get('gc', []))} |")
    out.append(f"| GC pause (s) | {fmt((final.get('gcPauseMs') or 0) / 1000, 2)} |")
    out.append(f"| peak working set (MB) | {fmt(mb(final.get('peakWorkingSetBytes')))} |")
    out.append(f"| modules translated / prepared | {final.get('translations')} / {final.get('preparations')} |")
    out.append(f"| tasks completed (cached) | {final.get('tasksCompleted')} ({final.get('tasksCached')}) |")
    out.append('')

    out.append('Front end, and module stages summed over modules.  CPU, allocation and GC are '
               'process-wide deltas over each span, so where spans overlap other work they include it.\n')
    out.append('| stage | count | wall (s) | wall, union of spans (s) | CPU (s) | allocated (MB) | GC gen0/1/2 | GC pause (s) |')
    out.append('|---|---:|---:|---:|---:|---:|---|---:|')
    for stage in FRONT + MODULE_STAGES:
        ss = [s for s in spans if s['stage'] == stage]
        if not ss:
            continue
        wall = sum(s['end'] - s['start'] for s in ss)
        union = union_length([(s['start'], s['end']) for s in ss])
        gcs = [sum(s['gc'][i] for s in ss) for i in range(3)]
        out.append(f"| {stage} | {len(ss)} | {fmt(wall / 1000, 2)} | {fmt(union / 1000, 2)} | "
                   f"{fmt(sum(s['cpuMs'] for s in ss) / 1000, 2)} | {fmt(mb(sum(s['allocBytes'] for s in ss)))} | "
                   f"{'/'.join(map(str, gcs))} | {fmt(sum(s['gcPauseMs'] for s in ss) / 1000, 2)} |")
    out.append('')

    resolve_end = max((s['end'] for s in spans if s['stage'] == 'resolve'), default=0.0)
    last_done = max((t['completed'] for t in tasks), default=resolve_end)
    if tasks:
        mean_busy, at = busy_profile(tasks, resolve_end, last_done)
        idle = idle_intervals(tasks, resolve_end, last_done)
        idle_total = sum(e - s for s, e in idle)
        waits = [t['queueWaitMs'] for t in tasks if t.get('queueWaitMs') is not None]
        solves = [t['solveMs'] for t in tasks if t.get('solveMs') is not None]
        out.append('After resolution (from the end of `resolve` to the last task completed):\n')
        out.append('| quantity | value |')
        out.append('|---|---:|')
        out.append(f'| duration (s) | {fmt((last_done - resolve_end) / 1000, 2)} |')
        out.append(f'| mean running tasks | {fmt(mean_busy, 2)} |')
        out.append(f'| time with no task running (s) | {fmt(idle_total / 1000, 2)} |')
        out.append(f'| tasks | {len(tasks)} |')
        if waits:
            out.append(f'| queue wait, submitted to running: median / max (s) | '
                       f'{fmt(statistics.median(waits) / 1000, 3)} / {fmt(max(waits) / 1000, 3)} |')
        if solves:
            out.append(f'| solve, running to completed: sum / median / max (s) | {fmt(sum(solves) / 1000, 2)} / '
                       f'{fmt(statistics.median(solves) / 1000, 3)} / {fmt(max(solves) / 1000, 3)} |')
        out.append('')
        levels = sorted(at)
        out.append('| running tasks | ' + ' | '.join(str(l) for l in levels) + ' |')
        out.append('|---|' + '---:|' * len(levels))
        out.append('| time (s) | ' + ' | '.join(fmt(at[l] / 1000, 2) for l in levels) + ' |')
        out.append('')

        # Apportion the idle stretches: the share of each idle interval overlapped by each module
        # stage (union over modules), the GC pause within the stage spans pro rata, and the rest.
        out.append('Stretches after resolution with no task running, by what overlaps them.  A span\'s GC '
                   'pause is counted pro rata to its overlap; "other" is idle time no stage covers.\n')
        out.append('| stage | idle time overlapped (s) | of which GC pause, pro rata (s) |')
        out.append('|---|---:|---:|')
        covered = []
        for stage in MODULE_STAGES:
            ss = [s for s in spans if s['stage'] == stage]
            ov, gc = 0.0, 0.0
            for s in ss:
                width = s['end'] - s['start']
                for iv in idle:
                    o = overlap((s['start'], s['end']), iv)
                    if o > 0:
                        ov += o
                        gc += s['gcPauseMs'] * (o / width if width > 0 else 0)
                        covered.append((max(s['start'], iv[0]), min(s['end'], iv[1])))
            out.append(f'| {stage} | {fmt(ov / 1000, 2)} | {fmt(gc / 1000, 2)} |')
        out.append(f'| other | {fmt((idle_total - union_length(covered)) / 1000, 2)} | |')
        out.append('')

    if modules or any(s['stage'] in MODULE_STAGES for s in spans):
        per = {}
        for s in spans:
            if s['stage'] in MODULE_STAGES:
                per.setdefault(s['module'], {})[s['stage']] = s
        for m in modules:
            per.setdefault(m['module'], {})['module'] = m
        rows = []
        for name, d in per.items():
            tr = sum(d[k]['end'] - d[k]['start'] for k in ('translate.prelude', 'translate', 'prepare') if k in d)
            rows.append((tr, name, d))
        rows.sort(key=lambda r: -r[0])
        shown = rows[:25]
        out.append(f'Modules by translation and preparation time ({len(shown)} of {len(rows)}); times in seconds.\n')
        out.append('| module | lock wait | prelude | translate | prepare | tasks | GC pause | admitted | released |')
        out.append('|---|---:|---:|---:|---:|---:|---:|---:|---:|')
        for _, name, d in shown:
            def w(k):
                return fmt((d[k]['end'] - d[k]['start']) / 1000, 3) if k in d else ''
            gc = sum(d[k]['gcPauseMs'] for k in MODULE_STAGES if k in d)
            m = d.get('module', {})
            out.append(f"| `{name}` | {w('translate.wait')} | {w('translate.prelude')} | {w('translate')} | {w('prepare')} | "
                       f"{d.get('prepare', {}).get('tasks', '')} | {fmt(gc / 1000, 3)} | "
                       f"{fmt((m.get('admitted') or 0) / 1000, 2) if m else ''} | "
                       f"{fmt((m.get('released') or 0) / 1000, 2) if m else ''} |")
        out.append('')
    return '\n'.join(out)


# ---------------------------------------------------------------------------- check

def check(events):
    """A digest that does not depend on timing: event kinds, stages, modules and task counts."""
    lines = []
    kinds = {}
    for e in events:
        kinds[e['ev']] = kinds.get(e['ev'], 0) + 1
    lines.append('first event: ' + events[0]['ev'])
    lines.append('last event: ' + events[-1]['ev'])
    lines.append('kinds: ' + ', '.join(sorted(kinds)))
    spans = by_kind(events, 'span')
    for stage in FRONT + MODULE_STAGES:
        n = sum(1 for s in spans if s['stage'] == stage)
        lines.append(f'{stage}: {n}')
    monotone = all(s['start'] <= s['end'] for s in spans)
    lines.append(f'spans well-formed: {monotone}')
    tasks = by_kind(events, 'task')
    ordered = all(t['submitted'] <= (t['running'] if t['running'] is not None else t['completed']) <= t['completed']
                  for t in tasks)
    lines.append(f'task times ordered: {ordered}')
    for m in sorted({t['module'] for t in tasks}):
        mt = [t for t in tasks if t['module'] == m]
        outcomes = {}
        for t in mt:
            outcomes[t['outcome']] = outcomes.get(t['outcome'], 0) + 1
        lines.append(f'module {m}: {len(mt)} tasks, ' + ', '.join(f'{k} {v}' for k, v in sorted(outcomes.items())))
    for m in by_kind(events, 'module'):
        ok = m['admitted'] <= m['scheduled'] <= m['completed'] <= m['released']
        lines.append(f"module event {m['module']}: {m['symbols']} symbols, ordered {ok}")
    final = by_kind(events, 'summary')[-1]
    lines.append(f"summary: reason {final['reason']}, {final['tasksCompleted']} completed, "
                 f"{final['tasksUnfinished']} unfinished, events counted {final['events'] == len(events)}")
    return '\n'.join(lines)


# ---------------------------------------------------------------------------- run

def ps_tree():
    """{pid: (ppid, cpu%, rss KB, comm)} from ps; works on Linux and macOS."""
    try:
        text = subprocess.run(['ps', '-axo', 'pid=,ppid=,%cpu=,rss=,comm='], capture_output=True, text=True,
                              check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return {}
    procs = {}
    for line in text.splitlines():
        parts = line.split(None, 4)
        if len(parts) < 4:
            continue
        try:
            procs[int(parts[0])] = (int(parts[1]), float(parts[2]), int(parts[3]), parts[4] if len(parts) > 4 else '')
        except ValueError:
            continue
    return procs


def sample(root, path, interval, stop):
    t0 = time.monotonic()
    with open(path, 'w') as f:
        f.write('seconds\troot_cpu_pct\troot_rss_kb\tchildren\tchildren_cpu_pct\tchildren_rss_kb\n')
        while not stop.is_set():
            procs = ps_tree()
            if root in procs:
                kids, frontier = set(), {root}
                while frontier:
                    nxt = {p for p, v in procs.items() if v[0] in frontier and p not in kids and p != root}
                    kids |= nxt
                    frontier = nxt
                r = procs[root]
                f.write(f'{time.monotonic() - t0:.3f}\t{r[1]}\t{r[2]}\t{len(kids)}\t'
                        f'{sum(procs[k][1] for k in kids):.1f}\t{sum(procs[k][2] for k in kids)}\n')
            stop.wait(interval)


def run_once(cmd, out_path, ps_path, interval):
    before = resource.getrusage(resource.RUSAGE_CHILDREN)
    start = time.monotonic()
    with open(out_path, 'wb') as out:
        proc = subprocess.Popen(cmd, stdout=out, stderr=subprocess.STDOUT)
        stop = threading.Event()
        sampler = threading.Thread(target=sample, args=(proc.pid, ps_path, interval, stop), daemon=True)
        sampler.start()
        code = proc.wait()
        stop.set()
        sampler.join()
    wall = time.monotonic() - start
    after = resource.getrusage(resource.RUSAGE_CHILDREN)
    rss = after.ru_maxrss * (1 if platform.system() == 'Darwin' else 1024)
    return code, wall, after.ru_utime - before.ru_utime, after.ru_stime - before.ru_stime, rss


def run(args):
    os.makedirs(args.out, exist_ok=True)
    verify = args.verify_args
    if verify and verify[0] == '--':
        verify = verify[1:]
    rows = []
    for i in range(args.repeat):
        for mode in ('off', 'on'):
            cmd = [args.dafny, 'verify'] + verify
            if mode == 'on':
                cmd += ['--pipeline-profile', os.path.join(args.out, f'on-{i}.jsonl')]
            code, wall, user, system, rss = run_once(cmd, os.path.join(args.out, f'{mode}-{i}.out'),
                                                     os.path.join(args.out, f'ps-{mode}-{i}.tsv'), args.sample)
            rows.append((mode, i, code, wall, user, system, rss))
            print(f'{mode} {i}: exit {code}, {wall:.2f} s wall, {user + system:.2f} s CPU', flush=True)
    with open(os.path.join(args.out, 'runs.tsv'), 'w') as f:
        f.write('mode\trun\texit\twall_s\tuser_s\tsystem_s\tpeak_rss_bytes_largest_process\n')
        for r in rows:
            f.write('\t'.join(str(x) if not isinstance(x, float) else f'{x:.3f}' for x in r) + '\n')

    def read(mode, i):
        with open(os.path.join(args.out, f'{mode}-{i}.out'), 'rb') as f:
            return f.read()

    reference = read('off', 0)
    identical = all(read(m, i) == reference for m in ('off', 'on') for i in range(args.repeat))
    codes = {r[2] for r in rows}
    md = ['## `--pipeline-profile`: output identity and overhead\n',
          f"`dafny verify {' '.join(verify)}`, {args.repeat} runs without the option and {args.repeat} with it, "
          'alternating.\n',
          f'- Output of every run byte-identical to the first unprofiled run: **{identical}**.',
          f'- Exit codes: {sorted(codes)}.', '']
    md.append('| mode | runs | wall median (s) | wall min–max (s) | CPU median, with Z3 (s) | peak RSS, largest process (MB) |')
    md.append('|---|---:|---:|---:|---:|---:|')
    med = {}
    for mode in ('off', 'on'):
        rs = [r for r in rows if r[0] == mode]
        walls = [r[3] for r in rs]
        cpus = [r[4] + r[5] for r in rs]
        med[mode] = (statistics.median(walls), statistics.median(cpus))
        md.append(f'| {mode} | {len(rs)} | {walls and fmt(med[mode][0], 2)} | {fmt(min(walls), 2)}–{fmt(max(walls), 2)} | '
                  f'{fmt(med[mode][1], 2)} | {fmt(max(r[6] for r in rs) / 1e6)} |')
    md.append('')
    md.append(f"Overhead, ratio of medians on/off: wall {med['on'][0] / med['off'][0]:.3f}, "
              f"CPU {med['on'][1] / med['off'][1]:.3f}.  With {args.repeat} runs each, a ratio within the "
              f"spread of the unprofiled runs (min–max above) is not distinguishable from 1.\n")
    last = os.path.join(args.out, f'on-{args.repeat - 1}.jsonl')
    if os.path.exists(last):
        md.append(summary(load(last), title=f'Profile of the last profiled run (`{os.path.basename(last)}`)'))
    text = '\n'.join(md)
    with open(os.path.join(args.out, 'summary.md'), 'w') as f:
        f.write(text + '\n')
    print(text)
    return 0 if identical and len(codes) == 1 else 1


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest='cmd', required=True)
    r = sub.add_parser('run')
    r.add_argument('--dafny', required=True)
    r.add_argument('--out', required=True)
    r.add_argument('--repeat', type=int, default=3)
    r.add_argument('--sample', type=float, default=0.5)
    r.add_argument('verify_args', nargs=argparse.REMAINDER)
    s = sub.add_parser('summary')
    s.add_argument('profile')
    s.add_argument('--title')
    c = sub.add_parser('check')
    c.add_argument('profile')
    a = p.parse_args()
    if a.cmd == 'run':
        sys.exit(run(a))
    if a.cmd == 'summary':
        print(summary(load(a.profile), a.title))
    else:
        print(check(load(a.profile)))


if __name__ == '__main__':
    main()
