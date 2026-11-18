#!/usr/bin/env python3
"""The verifier's verdicts on Dafny's own test programs.

    lit-verdicts.py plan     <LitTest dir> <plan.tsv>
    lit-verdicts.py run      <plan.tsv> <LitTest dir> <dafny> <z3> <out.tsv> [--shard I/N] [--jobs K]
    lit-verdicts.py summary  <verdicts.tsv>...                 counts, as Markdown
    lit-verdicts.py expected <verdicts.tsv>...                 the verdicts alone, for expected-verdicts.tsv
    lit-verdicts.py compare  <expected.tsv> <actual.tsv>       programs whose verdict differs

The programs are the `.dfy` files under dafny0 to dafny4 and git-issues whose first
RUN line verifies them (%verify, %testDafnyForEach*, %build or %run, without
--no-verify).  Each is verified once, with the options its RUN line passes, minus
options about compilation and output, plus these, unless the program sets them
itself:

    --resource-limit 16000000 --verification-time-limit 0 --cores 1
    --boogie /normalizeDeclarationOrder:0 --allow-warnings

(--boogie is not added to a program that uses --standard-libraries, whose
library refuses to load under --boogie options other than its own.)

A resource limit, not a time limit, so that a verdict does not depend on the
machine.  The verdicts are compared with each other, build against build, not
with the .expect files: a program whose verdict moves is either a proof a change
cost or a false proof it stopped, and reading the program tells which.

A verdict row is: program, exit code, the verifier's summary line(s) (with a
count of proofs that ran out of resource), the lines with errors, and seconds.
The verdict is the first four: `expected` drops the seconds, and `compare` looks
at nothing else.  So a program's time, and the resources its finished proofs
used, never change a verdict (optional JSON logs record resource counts separately); a
proof that no longer finishes within the limit does, since it is counted.

.github/review/expected-verdicts.tsv holds the verdicts CI expects, and the
job fails when one changes, or a program is added or removed.  To change it on
purpose, replace it with `expected-verdicts.tsv` from the run's `verdicts`
artifact (verdicts-off or verdicts-on), in a commit that says, for each program
whose verdict changed, why. `run` accepts --additional-axioms true and
--measurements <directory>; omitting them preserves the original invocation.

`run --verification-cache DIR` adds `--verification-cache DIR` to the programs that
select the refreshed type system themselves (the option requires it), and runs them
with only the environment variables in CACHE_ENVIRONMENT, since the cache keys the
solver's environment, and with PATH reduced to the directory of `dotnet` and the system
directories, so that a runner's own tool directories do not reach the key; every other
program runs as before.  `--cached-only true` runs only those programs.
`same <measurements> <measurements'> <verdicts> <verdicts'>` checks that the programs of
the second run have the verdicts and the per-proof outcomes and resource counts of the
first, and prints both runs' times.  `cache-stats <measurements> <verdicts>` prints the
cached programs' time, cache hits and solver starts of one run
(https://github.com/erniecohen/dafny/issues/261).
"""
import concurrent.futures
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import time

DIRS = ['dafny0', 'dafny1', 'dafny2', 'dafny3', 'dafny4', 'git-issues']
FIXED = ['--resource-limit', '16000000', '--verification-time-limit', '0',
         '--boogie', '/normalizeDeclarationOrder:0', '--cores', '1', '--allow-warnings']
TIMEOUT = 900  # seconds, per program; a program that takes longer is reported as TIMEOUT
CACHE_ENVIRONMENT = ('HOME', 'TMPDIR', 'DOTNET_ROOT', 'DOTNET_CLI_TELEMETRY_OPTOUT')
SYSTEM_PATH = ['/usr/bin', '/bin', '/usr/sbin', '/sbin']


def cache_environment():
    env = {k: os.environ[k] for k in CACHE_ENVIRONMENT if k in os.environ}
    dotnet = shutil.which('dotnet')
    env['PATH'] = os.pathsep.join(([str(Path(dotnet).parent)] if dotnet else []) + SYSTEM_PATH)
    return env
DROP = re.compile(r'--(target|output|compile-verbose|build|no-verify|spill-translation|unicode-char)(=|$)')


def plan(litdir, out):
    rows = []
    for d in DIRS:
        for fn in sorted(os.listdir(os.path.join(litdir, d))):
            if not fn.endswith('.dfy'):
                continue
            path = d + '/' + fn
            with open(os.path.join(litdir, path), errors='replace') as f:
                first = f.readline()
            m = re.match(r'\s*//\s*RUN:\s*(.*)$', first)
            if not m:
                continue
            cmd = m.group(1).split('>')[0]
            cmd = cmd.replace('"%s"', '').replace('%s', '')
            opts = None
            if '%testDafnyForEach' in cmd:
                opts = cmd.split(' -- ', 1)[1] if ' -- ' in cmd else ''
            elif '%verify' in cmd:
                opts = cmd.split('%verify', 1)[1]
            elif re.search(r'%(build|run)\b', cmd):
                opts = re.split(r'%(?:build|run)\b', cmd, 1)[1]
            if opts is None:
                continue
            # The suite supplies its pinned solver itself. Resolve the harness solver
            # placeholder by dropping that option and its value together.
            raw = shlex.split(opts)
            toks = []
            i = 0
            while i < len(raw):
                if raw[i] == '--solver-path' and i + 1 < len(raw) and raw[i + 1].startswith('%'):
                    i += 2
                    continue
                if not raw[i].startswith('%'):
                    toks.append(raw[i])
                i += 1
            if '--no-verify' in toks:
                continue
            keep, skip = [], False
            for t in toks:
                if skip:
                    skip = False
                    continue
                if DROP.match(t) or t.startswith('-t:'):
                    if t in ('--target', '--output'):
                        skip = True  # its value is the next token
                    continue
                keep.append(t)
            rows.append((path, ' '.join(shlex.quote(t) for t in keep)))
    with open(out, 'w') as f:
        for p, o in rows:
            f.write(p + '\t' + o + '\n')
    print(len(rows), 'programs planned')


def refreshed(own):
    """Whether a program's own options select the refreshed type system."""
    value = False
    for i, t in enumerate(own):
        m = re.match(r'--type-system-refresh(?:[:=](true|false))?$', t)
        if m:
            if m.group(1):
                value = m.group(1) == 'true'
            elif i + 1 < len(own) and own[i + 1] in ('true', 'false'):
                value = own[i + 1] == 'true'
            else:
                value = True
    return value


def cached(opts, cache):
    own = shlex.split(opts)
    return bool(cache) and refreshed(own) and not any(t.startswith('--verification-cache') for t in own)


def verify(args):
    litdir, dafny, z3, path, opts, additional_axioms, measurements, *rest = args
    cache = rest[0] if rest else None
    own = shlex.split(opts)
    names = {t.split('=')[0].split(':')[0] for t in own if t.startswith('--')}
    # A precompiled library (--standard-libraries, a .doo) records the --boogie
    # options it was built with and refuses to load under others, so a program
    # that uses one keeps its own (by default none).
    if '--standard-libraries' in names and not any(
            re.match(r'--standard-libraries[:=]false$', t) for t in own):
        names.add('--boogie')
    fixed, i = [], 0
    while i < len(FIXED):  # an option the program sets itself is left to the program
        name = FIXED[i]
        takes = i + 1 < len(FIXED) and not FIXED[i + 1].startswith('--')
        if name not in names:
            fixed += FIXED[i:i + 2] if takes else [name]
        i += 2 if takes else 1
    extra = ['--additional-axioms'] if additional_axioms else []
    dest = Path(measurements).resolve() / path if measurements else None
    if dest:
        dest.mkdir(parents=True, exist_ok=True)
        extra += ['--log-format', 'json;LogFileName=' + str(dest / 'results.json')]
    env = None
    if cached(opts, cache):
        extra += ['--verification-cache', str(Path(cache).resolve())]
        if dest:
            extra += ['--pipeline-profile', str(dest / 'profile.jsonl')]
        env = cache_environment()
    # Put observations before the original options: a missing argument in an old
    # RUN line must retain its diagnostic rather than consume an added option.
    cmd = [dafny, 'verify', path, '--solver-path', z3] + fixed + extra + own
    if dest:
        (dest / 'command.json').write_text(json.dumps(cmd))
    start = time.monotonic()
    try:
        p = subprocess.run(cmd, cwd=litdir, capture_output=True, text=True, timeout=TIMEOUT, env=env)
        out, rc = p.stdout + p.stderr, str(p.returncode)
    except subprocess.TimeoutExpired:
        if dest:
            (dest / 'output.txt').write_text('TIMEOUT')
        return (path, 'TIMEOUT', '', '', '%.1f' % (time.monotonic() - start))
    if dest:
        (dest / 'output.txt').write_text(out)
    secs = '%.1f' % (time.monotonic() - start)
    fin = ''.join(re.findall(r'verifier finished with ([^\n]*)', out))
    errs = sorted(set(re.findall(r'\((\d+),\d+\): Error', out)), key=int)
    oor = len(re.findall(r'out of resource', out))
    return (path, rc, fin + ('; %d out of resource' % oor if oor else ''), ','.join(errs), secs)


def run(planf, litdir, dafny, z3, out, shard='0/1', jobs=4, additional_axioms=False, measurements=None,
        cache=None, cached_only=False):
    i, n = (int(x) for x in shard.split('/'))
    with open(planf) as f:
        rows = [l.rstrip('\n').split('\t') for l in f if l.strip()]
    rows = [r if len(r) == 2 else r + [''] for r in rows]
    mine = [r for k, r in enumerate(rows) if k % n == i]
    if cached_only:
        mine = [r for r in mine if cached(r[1], cache)]
    elif cache:
        print('%d programs with --verification-cache' % sum(1 for r in mine if cached(r[1], cache)), flush=True)
    work = [(litdir, dafny, z3, p, o, additional_axioms, measurements, cache) for p, o in mine]
    print('shard %d/%d: %d of %d programs, %d at a time' % (i, n, len(mine), len(rows), jobs), flush=True)
    with concurrent.futures.ThreadPoolExecutor(jobs) as ex, open(out, 'w') as f:
        for r in ex.map(verify, work):
            f.write('\t'.join(r) + '\n')
            f.flush()
            print('\t'.join(r), flush=True)


def load(fn):
    """Rows by program, padded to the five columns, so that an editor's trimming of
    trailing tabs or a CRLF checkout does not change a verdict."""
    with open(fn) as f:
        rows = [l.rstrip('\r\n').split('\t') for l in f if l.strip()]
    return {r[0]: (r + [''] * 5)[:5] for r in rows}


def verdict(row):
    """One word for a row: verified, errors, out-of-resource, resolution, timeout or other."""
    rc, fin = row[1], row[2]
    if rc == 'TIMEOUT':
        return 'timeout'
    if not fin:
        return 'resolution' if rc == '2' else 'other'
    counts = [int(x) for x in re.findall(r'(\d+) errors?', fin)]
    if any(counts):
        return 'errors'
    if 'out of resource' in fin:
        return 'out-of-resource'
    return 'verified' if rc == '0' else 'other'


def summary(files):
    rows = {}
    for fn in files:
        rows.update(load(fn))
    kinds = {}
    for r in rows.values():
        kinds[verdict(r)] = kinds.get(verdict(r), 0) + 1
    secs = sum(float(r[4]) for r in rows.values() if r[4])
    print('| verdict | programs |')
    print('|---|---|')
    for k in ['verified', 'errors', 'out-of-resource', 'resolution', 'timeout', 'other']:
        if k in kinds:
            print('| %s | %d |' % (k, kinds[k]))
    print('| **all** | **%d** |' % len(rows))
    print()
    print('Verifier time, summed over programs: %.0f s.' % secs)
    slow = sorted(rows.values(), key=lambda r: -float(r[4] or 0))[:5]
    print('Slowest: ' + ', '.join('`%s` %s s' % (r[0], r[4]) for r in slow) + '.')


def expected(files):
    rows = {}
    for fn in files:
        rows.update(load(fn))
    for k in sorted(rows):
        print('\t'.join(rows[k][:4]))


def without_timing(value):
    if isinstance(value, dict):
        return {k: without_timing(v) for k, v in value.items() if k not in ('runTime', 'timingReplayed')}
    if isinstance(value, list):
        return [without_timing(v) for v in value]
    return value


def same(first_measurements, second_measurements, first, second):
    """The second run's programs, against the same programs of the first run."""
    a, b = load(first), load(second)
    differ, secs, hits, starts, ru = [], [0.0, 0.0], 0, 0, 0
    for k in sorted(b):
        logs = [Path(m) / k / 'results.json' for m in (first_measurements, second_measurements)]
        results = [without_timing(json.loads(l.read_text())) if l.exists() else None for l in logs]
        if k not in a or a[k][1:4] != b[k][1:4] or results[0] != results[1]:
            differ.append(k)
        secs[0] += float(a[k][4] or 0) if k in a else 0
        secs[1] += float(b[k][4] or 0)
        profile = Path(second_measurements) / k / 'profile.jsonl'
        if profile.exists():
            for line in profile.read_text().splitlines():
                event = json.loads(line) if line.strip() else {}
                if event.get('ev') == 'result-cache':
                    hits += event.get('hits', 0)
                    starts += event.get('solverStarts', 0)
        if results[1]:
            ru += sum(r.get('resourceCount', 0) for r in results[1].get('verificationResults', []))
    for k in differ:
        print('differs:', k)
    print('%d programs, %d differ in verdict, outcomes or resource counts; %d resource units; '
          'verifier time %.1f s then %.1f s; second run: %d cache hits, %d solver starts'
          % (len(b), len(differ), ru, secs[0], secs[1], hits, starts))
    return len(differ)


def cache_stats(measurements, verdicts):
    """One run's programs that used the cache: their time and the cache's counters."""
    rows = load(verdicts)
    n, secs, counts = 0, 0.0, {'tasks': 0, 'hits': 0, 'solverStarts': 0, 'imagesHashed': 0}
    for k, row in rows.items():
        profile = Path(measurements) / k / 'profile.jsonl'
        if not profile.exists():
            continue
        n += 1
        secs += float(row[4] or 0)
        for line in profile.read_text().splitlines():
            event = json.loads(line) if line.strip() else {}
            if event.get('ev') == 'result-task':
                counts['tasks'] += 1
            if event.get('ev') == 'result-cache':
                for c in ('hits', 'solverStarts', 'imagesHashed'):
                    counts[c] += event.get(c, 0)
    print('%d cached programs, verifier time %.1f s; %d tasks, %d cache hits, %d solver starts, '
          '%d execution images hashed' % (n, secs, counts['tasks'], counts['hits'], counts['solverStarts'],
                                          counts['imagesHashed']))


def compare(expected, actual):
    a, b = load(expected), load(actual)
    n = 0
    for k in sorted(set(a) | set(b)):
        ra, rb = a.get(k), b.get(k)
        if ra is None or rb is None or ra[1:4] != rb[1:4]:
            n += 1
            print(k)
            print('   expected', ra[1:4] if ra else 'missing')
            print('   actual  ', rb[1:4] if rb else 'missing')
    print(n, 'of', len(set(a) | set(b)), 'programs differ')
    return n


if __name__ == '__main__':
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    cmd, args = sys.argv[1], sys.argv[2:]
    if cmd == 'plan':
        plan(*args)
    elif cmd == 'run':
        opts = dict(zip(args[5::2], args[6::2]))
        run(*args[:5], shard=opts.get('--shard', '0/1'), jobs=int(opts.get('--jobs', '4')),
            additional_axioms=opts.get('--additional-axioms', 'false') == 'true',
            measurements=opts.get('--measurements'), cache=opts.get('--verification-cache'),
            cached_only=opts.get('--cached-only', 'false') == 'true')
    elif cmd == 'same':
        sys.exit(1 if same(*args) else 0)
    elif cmd == 'cache-stats':
        cache_stats(*args)
    elif cmd == 'summary':
        summary(args)
    elif cmd == 'expected':
        expected(args)
    elif cmd == 'compare':
        sys.exit(1 if compare(*args) else 0)
    else:
        sys.exit(__doc__)
