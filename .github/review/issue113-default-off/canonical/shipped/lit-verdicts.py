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
"""
import concurrent.futures
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import time

DIRS = ['dafny0', 'dafny1', 'dafny2', 'dafny3', 'dafny4', 'git-issues']
FIXED = ['--resource-limit', '16000000', '--verification-time-limit', '0',
         '--boogie', '/normalizeDeclarationOrder:0', '--cores', '1', '--allow-warnings']
TIMEOUT = 900  # seconds, per program; a program that takes longer is reported as TIMEOUT
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
            toks = [t for t in shlex.split(opts) if not t.startswith('%')]
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


def verify(args):
    litdir, dafny, z3, path, opts, additional_axioms, measurements = args
    own = shlex.split(opts)
    names = {t.split('=')[0].split(':')[0] for t in own if t.startswith('--')}
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
    # Put observations before the original options: a missing argument in an old
    # RUN line must retain its diagnostic rather than consume an added option.
    cmd = [dafny, 'verify', path, '--solver-path', z3] + fixed + extra + own
    if dest:
        (dest / 'command.json').write_text(json.dumps(cmd))
    start = time.monotonic()
    try:
        p = subprocess.run(cmd, cwd=litdir, capture_output=True, text=True, timeout=TIMEOUT)
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


def run(planf, litdir, dafny, z3, out, shard='0/1', jobs=4, additional_axioms=False, measurements=None):
    i, n = (int(x) for x in shard.split('/'))
    with open(planf) as f:
        rows = [l.rstrip('\n').split('\t') for l in f if l.strip()]
    rows = [r if len(r) == 2 else r + [''] for r in rows]
    mine = [r for k, r in enumerate(rows) if k % n == i]
    work = [(litdir, dafny, z3, p, o, additional_axioms, measurements) for p, o in mine]
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
            measurements=opts.get('--measurements'))
    elif cmd == 'summary':
        summary(args)
    elif cmd == 'expected':
        expected(args)
    elif cmd == 'compare':
        sys.exit(1 if compare(*args) else 0)
    else:
        sys.exit(__doc__)
