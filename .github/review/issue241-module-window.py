#!/usr/bin/env python3
"""Evidence for --verification-module-window (issue 241, cause 1).

    issue241-module-window.py run      <out dir> <dafny> <z3> <cwd> <K> [--gc 0|1] [--cores N]
                                       [--smt] [--] <dafny verify arguments>...
    issue241-module-window.py corpus   <plan.tsv> <LitTest dir> <dafny> <z3> <out dir> [--ks 1,4,16]
                                       [--shard I/N] [--cores N]
    issue241-module-window.py compare  <baseline run dir> <run dir>...   identity of each run with the baseline
    issue241-module-window.py table    <run dir>...                      wall time, CPU time, busy cores, peak RSS
    issue241-module-window.py report   <artifacts dir> <report.md>       the workflow's report

`run` verifies once, under /usr/bin/time -v, with

    --verification-module-window K --verification-time-limit 0 --cores N
    --boogie /deterministicLiteralHashes:1 --boogie /restartProver
    --log-format csv  and  --log-format json

and DOTNET_gcServer set to --gc.  It records stdout and stderr separately, the
exit code, the wall time, the user and system CPU time of the process and the
solvers it reaped, and its peak resident set.  With --smt it also writes each
task's SMT command stream (--solver-log <dir>/@PROC@.smt2), keeps only the
SHA-256 of each stream in smt.tsv, and deletes the streams.  A stream is the
file up to and including the solver's answer to its last check: Boogie appends
the commands that reset the solver for its next task, or for its exit, to the
file of the task it last checked, and those depend on when the process exits
(they differ between two runs at K=1), so they are not the task's.

`compare` checks, for each run against the baseline (normally K=1):

    stdout, stderr     byte for byte, and as sorted lines, after the run's own directory,
                       which names its log files, is replaced by <run>
    exit code          equal
    obligations        the set of (symbol, batch number, assertions) of the JSON log
    outcomes, RU       per obligation, equal
    logs               the rows of the JSON and CSV logs, as sets.  Their order is not
                       compared: the JSON log lists a symbol's results in the order its
                       batches finish, and the CSV log orders rows by duration, so both
                       differ between two runs at K = 1.  The reported order is that of
                       stdout, compared byte for byte.
    SMT                per task, the hash of its stream, when both runs have smt.tsv

and exits 1 when any of them differs.

`corpus` runs every program of a lit-verdicts plan that declares two or more
modules (by a `module` keyword at the start of a line), at each K, through `run`
with --smt, and compares each K with K=1, program by program.
"""
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys

TIME = '/usr/bin/time'
TRUNCATED = re.compile(r'-n\d+\.smt2$')
ANSWER = re.compile(rb'^; [A-Z][A-Za-z ]*$')  # ; Valid, ; Invalid, ; Out of resource, ...
MODULE = re.compile(r'^\s*(?:abstract\s+|replaceable\s+)?module\s', re.M)


def stream_hash(path):
    data = path.read_bytes()
    lines = data.split(b'\n')
    last = None
    for i, line in enumerate(lines):
        if line.strip() == b'(check-sat)':
            last = i
    if last is not None:
        # The answer is logged as a comment, such as `; Valid`, after the queries that follow (check-sat).
        for j in range(last + 1, len(lines)):
            if ANSWER.match(lines[j]):
                lines = lines[:j + 1]
                break
    return hashlib.sha256(b'\n'.join(lines)).hexdigest(), len(lines)


def run(out, dafny, z3, cwd, k, args, gc='0', cores=None, smt=False, timeout=5400):
    out = Path(out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    cores = cores or os.cpu_count()
    # An option the program sets itself is left to the program, as in lit-verdicts.py.
    own = {t.split('=')[0].split(':')[0] for t in args if t.startswith('--')}
    fixed = [('--cores', str(cores)), ('--verification-time-limit', '0')]
    cmd = [dafny, 'verify'] + args + ['--solver-path', z3]
    for name, value in fixed:
        if name not in own:
            cmd += [name, value]
    cmd += ['--verification-module-window', str(k),
            '--boogie', '/deterministicLiteralHashes:1', '--boogie', '/restartProver',
            '--log-format', 'csv;LogFileName=' + str(out / 'log.csv'),
            '--log-format', 'json;LogFileName=' + str(out / 'log.json')]
    if smt:
        (out / 'smt').mkdir()
        cmd += ['--solver-log', str(out / 'smt' / '@PROC@.smt2')]
    env = dict(os.environ, DOTNET_gcServer=str(gc))
    (out / 'command.json').write_text(json.dumps({'cmd': cmd, 'cwd': str(cwd), 'gcServer': gc, 'k': k,
                                                  'out': str(out)}))
    with open(out / 'stdout.txt', 'wb') as so, open(out / 'stderr.txt', 'wb') as se:
        # timeout(1) stops Dafny and its solvers with it; exit 124 means it ran out of time.
        p = subprocess.run(['timeout', '-k', '30', str(timeout), TIME, '-v', '-o', str(out / 'time.txt')] + cmd,
                           cwd=cwd, stdout=so, stderr=se, env=env)
        code = 'TIMEOUT' if p.returncode == 124 else str(p.returncode)
    (out / 'exit.txt').write_text(code + '\n')
    if smt:
        with open(out / 'smt.tsv', 'w') as f:
            for path in sorted((out / 'smt').iterdir()):
                digest, n = stream_hash(path)
                f.write('%s\t%s\t%d\n' % (path.name, digest, n))
        shutil.rmtree(out / 'smt')
    return code


def measures(d):
    d = Path(d)
    text = (d / 'time.txt').read_text()

    def field(name):
        m = re.search(re.escape(name) + r': (.*)', text)
        return m.group(1).strip() if m else ''
    wall = field('Elapsed (wall clock) time (h:mm:ss or m:ss)')
    parts = [float(x) for x in wall.split(':')]
    secs = sum(v * 60 ** i for i, v in enumerate(reversed(parts)))
    cpu = float(field('User time (seconds)')) + float(field('System time (seconds)'))
    rss = int(field('Maximum resident set size (kbytes)'))
    cmd = json.loads((d / 'command.json').read_text())
    return {'k': cmd['k'], 'gc': cmd['gcServer'], 'wall': secs, 'cpu': cpu, 'busy': cpu / secs if secs else 0,
            'rss_mib': rss / 1024, 'exit': (d / 'exit.txt').read_text().strip()}


def obligations(d):
    data = json.loads((Path(d) / 'log.json').read_text())
    rows = []
    for r in data['verificationResults']:
        for vc in r['vcResults']:
            asserts = tuple((a['filename'], a['line'], a['col'], a['description']) for a in vc['assertions'])
            rows.append(((r['name'], vc['vcNum'], asserts), (vc['outcome'], vc['resourceCount'])))
    return rows


def csv_rows(d):
    with open(Path(d) / 'log.csv', newline='') as f:
        rows = list(csv.reader(f))
    if not rows:
        return []
    keep = [i for i, h in enumerate(rows[0]) if 'Duration' not in h]
    return [tuple(r[i] for i in keep) for r in rows]


def smt(d):
    """Hashes of the SMT streams, by task file name.  Boogie truncates a long name and appends
    -n<i>, from a counter of the process, in the order in which solvers start: so for such names,
    the hashes of all the files with that truncated name, as a sorted list."""
    f = Path(d) / 'smt.tsv'
    if not f.exists():
        return None
    streams = {}
    for name, digest, _ in (l.split('\t') for l in f.read_text().splitlines()):
        streams.setdefault(TRUNCATED.sub('', name), []).append(digest)
    return {k: sorted(v) for k, v in streams.items()}


def run_dir(d):
    """The directory the run was made in, which its output names (it may have been moved since)."""
    cmd = json.loads((Path(d) / 'command.json').read_text())
    return cmd.get('out', str(Path(d).resolve())).encode()


def compare_one(base, other):
    """The list of differences between two runs."""
    diffs = []
    for name in ['stdout.txt', 'stderr.txt']:
        # The output names the run's own log files.
        a = (Path(base) / name).read_bytes().replace(run_dir(base), b'<run>')
        b = (Path(other) / name).read_bytes().replace(run_dir(other), b'<run>')
        if a != b:
            same_sorted = sorted(a.splitlines()) == sorted(b.splitlines())
            diffs.append('%s differs (%s as sorted lines)' % (name, 'equal' if same_sorted else 'also differs'))
    if (Path(base) / 'exit.txt').read_text() != (Path(other) / 'exit.txt').read_text():
        diffs.append('exit code differs')
    oa, ob = obligations(base), obligations(other)
    da, db = dict(oa), dict(ob)
    if set(da) != set(db):
        diffs.append('obligation sets differ: %d only in base, %d only in run' %
                     (len(set(da) - set(db)), len(set(db) - set(da))))
    moved = [k for k in set(da) & set(db) if da[k][0] != db[k][0]]
    ru = [k for k in set(da) & set(db) if da[k][1] != db[k][1]]
    if moved:
        diffs.append('%d outcomes differ, e.g. %s' % (len(moved), moved[0][:2]))
    if ru:
        diffs.append('%d resource counts differ, e.g. %s' % (len(ru), ru[0][:2]))
    # The JSON log lists a symbol's results in the order in which its batches finish (the
    # CompletedParts queue), so its order differs between two runs at K = 1 as well; the order
    # in which results are reported is that of stdout, compared byte for byte above.
    # The CSV logger orders its rows by duration, so only its rows are compared, not their order.
    if sorted(csv_rows(base)) != sorted(csv_rows(other)):
        diffs.append('CSV log rows differ (durations dropped)')
    sa, sb = smt(base), smt(other)
    if sa is not None and sb is not None:
        if set(sa) != set(sb):
            diffs.append('SMT task sets differ')
        bad = [t for t in set(sa) & set(sb) if sa[t] != sb[t]]
        if bad:
            diffs.append('%d SMT streams differ, e.g. %s' % (len(bad), sorted(bad)[0]))
    return diffs, len(oa), (sum(len(v) for v in sa.values()) if sa else 0)


def compare(base, others):
    failed = False
    print('| run | obligations | SMT streams | identical to %s |' % Path(base).name)
    print('|---|---|---|---|')
    for o in others:
        diffs, n, s = compare_one(base, o)
        failed |= bool(diffs)
        print('| %s | %d | %d | %s |' % (Path(o).name, n, s, 'yes' if not diffs else '; '.join(diffs)))
    return 1 if failed else 0


def table(dirs):
    print('| K | gcServer | exit | wall s | CPU s | busy cores | peak RSS MiB |')
    print('|---|---|---|---|---|---|---|')
    for d in dirs:
        m = measures(d)
        print('| %s | %s | %s | %.1f | %.1f | %.2f | %.0f |' %
              (m['k'], m['gc'], m['exit'], m['wall'], m['cpu'], m['busy'], m['rss_mib']))


def corpus(planf, litdir, dafny, z3, out, ks, shard='0/1', cores=None):
    i, n = (int(x) for x in shard.split('/'))
    rows = [l.rstrip('\n').split('\t') for l in open(planf) if l.strip()]
    chosen = []
    for r in rows:
        path, opts = r[0], (r[1] if len(r) > 1 else '')
        text = (Path(litdir) / path).read_text(errors='replace')
        if len(MODULE.findall(text)) >= 2:
            chosen.append((path, opts))
    mine = [r for k, r in enumerate(chosen) if k % n == i]
    print('shard %d/%d: %d of %d multi-module programs' % (i, n, len(mine), len(chosen)), flush=True)
    out = Path(out).resolve()
    failed = []
    report = []
    for path, opts in mine:
        own = shlex.split(opts)
        dirs = []
        for k in ks:
            d = out / path.replace('/', '__') / ('k%02d' % k)  # one length: Boogie truncates long log names
            names = {t.split('=')[0].split(':')[0] for t in own if t.startswith('--')}
            extra = [] if '--resource-limit' in names else ['--resource-limit', '16000000']
            extra += [] if '--allow-warnings' in names else ['--allow-warnings']
            run(d, dafny, z3, litdir, k, [path] + extra + own, cores=cores, smt=True, timeout=900)
            dirs.append(d)
        nob = len(obligations(dirs[0])) if (dirs[0] / 'log.json').exists() else 0
        line = [path, str(nob)]
        for d in dirs[1:]:
            try:
                diffs, _, _ = compare_one(dirs[0], d)
            except (OSError, ValueError, KeyError) as e:
                # No log: the program did not reach verification.  Compare the output only.
                diffs = [] if all((dirs[0] / f).read_bytes() == (d / f).read_bytes()
                                  for f in ['stdout.txt', 'stderr.txt', 'exit.txt']) else ['output differs (%s)' % e]
            line.append('yes' if not diffs else '; '.join(diffs))
            if diffs:
                failed.append(path)
        report.append(line)
        print('\t'.join(line), flush=True)
    with open(out / ('corpus-%d.tsv' % i), 'w') as f:
        for line in report:
            f.write('\t'.join(line) + '\n')
    return 1 if failed else 0


def report(artifacts, out):
    """The report of the workflow issue241-module-window.yml, from its artifacts."""
    art = Path(artifacts)
    lines = ['# --verification-module-window: evidence', '']
    runs = sorted(p for p in art.glob('issue241-time-*/*') if (p / 'time.txt').exists())
    failed = False
    for project in ['std', 'examples']:
        lines += ['## %s: time and memory' % {'std': 'Standard library (src/Std/dfyconfig.toml)',
                                                'examples': 'Standard library examples (examples/dfyconfig.toml)'}[project], '',
                  'Each K was run on one runner together with a K = 1 run (the ratio is K = 1 wall over K wall, '
                  'on that runner).  CPU is user plus system time of Dafny and the solvers it ran; busy cores '
                  'is CPU over wall; RSS is Dafny\'s peak resident set.', '',
                  '| gcServer | K | wall s | K=1 wall s | speed-up | CPU s | K=1 CPU s | busy cores | K=1 busy | '
                  'peak RSS MiB | K=1 RSS MiB | exit |',
                  '|---|---|---|---|---|---|---|---|---|---|---|---|']
        for job in sorted(art.glob('issue241-time-*'), key=lambda p: (p.name.split('-gc')[1], int(p.name.split('-k')[1].split('-')[0]))):
            k = int(job.name.split('-k')[1].split('-')[0])
            gc = job.name.split('-gc')[1]
            base = job / ('%s-gc%s-k1' % (project, gc))
            other = job / ('%s-gc%s-k%d' % (project, gc, k)) if k != 1 else job / ('%s-gc%s-k1-again' % (project, gc))
            if not (base / 'time.txt').exists() or not (other / 'time.txt').exists():
                lines.append('| %s | %d | missing | | | | | | | | | |' % (gc, k))
                continue
            a, b = measures(base), measures(other)
            lines.append('| %s | %d | %.0f | %.0f | %.2f | %.0f | %.0f | %.2f | %.2f | %.0f | %.0f | %s |' % (
                gc, k, b['wall'], a['wall'], a['wall'] / b['wall'], b['cpu'], a['cpu'], b['busy'], a['busy'],
                b['rss_mib'], a['rss_mib'], b['exit']))
        lines.append('')
    lines += ['## Identity with K = 1', '',
              'Every run against the K = 1 run of the same project and GC mode on the same runner, and the '
              'gcServer=1 runs against the gcServer=0 K = 1 run of the K = 1 job.  Compared: stdout and stderr '
              'byte for byte, exit code, the obligation set, each obligation\'s outcome and resource count, and '
              'the rows of the CSV log.', '',
              '| run | against | obligations | identical |', '|---|---|---|---|']
    for project in ['std', 'examples']:
        ref0 = art / 'issue241-time-k1-gc0' / ('%s-gc0-k1' % project)
        for job in sorted(art.glob('issue241-time-*')):
            for d in sorted(job.glob(project + '-*')):
                gc = job.name.split('-gc')[1]
                base = job / ('%s-gc%s-k1' % (project, gc))
                pairs = [(base, d)] if d != base else []
                if ref0.exists() and d != ref0 and ref0 != base:
                    pairs.append((ref0, d))
                for b, o in pairs:
                    try:
                        diffs, n, _ = compare_one(b, o)
                    except (OSError, ValueError, KeyError) as e:
                        diffs, n = ['not comparable: %s' % e], 0
                    failed |= bool(diffs)
                    lines.append('| %s/%s | %s/%s | %d | %s |' % (job.name, o.name, b.parent.name, b.name, n,
                                                                  'yes' if not diffs else '; '.join(diffs)))
    lines += ['', '## SMT streams of the standard library', '',
              'Each task\'s SMT command stream (up to the solver\'s answer to its last check), hashed, at K '
              'against K = 1, together with the other comparisons above.', '',
              '| run | obligations | SMT streams | identical to K = 1 |', '|---|---|---|---|']
    smt_base = art / 'issue241-smt-k1' / 'std-smt-k1'
    for k in [2, 4, 8]:
        o = art / ('issue241-smt-k%d' % k) / ('std-smt-k%d' % k)
        try:
            diffs, n, ns = compare_one(smt_base, o)
        except (OSError, ValueError, KeyError) as e:
            diffs, n, ns = ['not comparable: %s' % e], 0, 0
        failed |= bool(diffs)
        lines.append('| K = %d | %d | %d | %s |' % (k, n, ns, 'yes' if not diffs else '; '.join(diffs)))
    lines += ['', '## Multi-module programs of the verifier suite', '']
    rows = []
    for f in sorted(art.glob('issue241-corpus-*/corpus-*.tsv')):
        rows += [l.split('\t') for l in f.read_text().splitlines() if l.strip()]
    bad = [r for r in rows if any(x != 'yes' for x in r[2:])]
    lines.append('%d programs that declare two or more modules, %d obligations in all, each at K = 1, 4 and 16 '
                 'with its SMT streams hashed; identical to K = 1 at both K: %d.' %
                 (len(rows), sum(int(r[1]) for r in rows), len(rows) - len(bad)))
    if bad:
        failed = True
        lines += ['', '| program | K = 4 | K = 16 |', '|---|---|---|']
        lines += ['| %s | %s |' % (r[0], ' | '.join(r[2:])) for r in bad]
    lines += ['', 'Overall: %s.' % ('every comparison identical' if not failed else 'differences, listed above')]
    Path(out).write_text('\n'.join(lines) + '\n')
    return 1 if failed else 0


def main(argv):
    cmd = argv[0]
    if cmd == 'run':
        rest = argv[1:]
        extra = rest[rest.index('--') + 1:] if '--' in rest else []
        rest = rest[:rest.index('--')] if '--' in rest else rest
        flags = {}
        positional = []
        it = iter(rest)
        for a in it:
            if a in ('--gc', '--cores'):
                flags[a] = next(it)
            elif a == '--smt':
                flags[a] = True
            else:
                positional.append(a)
        out, dafny, z3, cwd, k = positional
        rc = run(out, dafny, z3, cwd, int(k), extra, gc=flags.get('--gc', '0'),
                 cores=flags.get('--cores'), smt=flags.get('--smt', False))
        print('exit code', rc)
        return 0
    if cmd == 'compare':
        return compare(argv[1], argv[2:])
    if cmd == 'report':
        return report(argv[1], argv[2])
    if cmd == 'table':
        table(argv[1:])
        return 0
    if cmd == 'corpus':
        rest = argv[1:]
        ks, shard, cores = [1, 4, 16], '0/1', None
        positional = []
        it = iter(rest)
        for a in it:
            if a == '--ks':
                ks = [int(x) for x in next(it).split(',')]
            elif a == '--shard':
                shard = next(it)
            elif a == '--cores':
                cores = next(it)
            else:
                positional.append(a)
        return corpus(*positional, ks=ks, shard=shard, cores=cores)
    print(__doc__)
    return 2


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
