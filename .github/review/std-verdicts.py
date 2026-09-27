#!/usr/bin/env python3
"""The verifier's verdicts on Dafny's standard library.

    std-verdicts.py run      <DafnyStandardLibraries dir> <dafny> <z3> <out.tsv> [--cores N] [--boogie ARGS]
    std-verdicts.py summary  <verdicts.tsv>          counts, as Markdown
    std-verdicts.py expected <verdicts.tsv>          the verdicts alone, for expected-std-verdicts.tsv
    std-verdicts.py compare  <expected.tsv> <actual.tsv>   rows whose verdict differs

The library is verified as the `verify` target of its Makefile verifies it: the
project src/Std/dfyconfig.toml, and then, for each target language (notarget, cs,
java, js, go, py), the target-specific files with
src/Std/TargetSpecific/dfyconfig.toml, which uses the library's committed binary.
The Makefile checks those parts with `dafny build -t:lib`; this runs `dafny
verify` on the same project and files, which verifies the same and writes no
binary.  Each part runs with the library's own options (its resource limits),
with `--cores N`, and with

    --verification-time-limit 0

in place of the library's time limit, so that a verdict does not depend on the
machine: a proof ends when it succeeds, fails or runs out of its resource limit.
The declaration order is Boogie's default, as upstream verifies the library.  It
is not quite deterministic.  Runs of one build have always given the same
verdicts, but the resource counts of some declarations move between runs.  (In
ten runs of the 4.11.0 line's library, of 2,190 declarations: between two runs,
up to 251 counts moved under Z3 5.1.0 and up to 97 under Z3 4.12.1, mostly by
under 0.1%, the most by 15%.)  So a proof close to its limit could change its
verdict from one run to the next.  If the comparison ever reports such a change,
run again before treating it as real.  Boogie's /normalizeDeclarationOrder:0 is
no remedy: two runs in that order gave identical counts under Z3 4.12.1, but
under Z3 5.1.0 the library did not finish in it within an hour (with a 30-second
limit, one proof times out there: Producers.ProducerState.ValidChangeTransitive).
A part that runs longer than TIMEOUT is stopped; its run row then reads TIMEOUT,
and it has no declaration rows.  (`--boogie ARGS` passes Boogie options to the
library part, for measurements under another order, e.g. /randomSeed:K; the
target-specific parts cannot take them, since a library's --boogie options must
equal those its binary was built with.)

The rows, one per line, tab-separated:

    run <part>                     exit code, summary line, error lines, seconds
    <part> <declaration> (<kind>)  outcome, (empty), (empty), resource count, proof count

A run row's verdict is its exit code, the verifier's summary line(s) and the
positions of the lines that report an error, a proof out of resource or a proof
that timed out (file(line,col), sorted).  A declaration row's verdict is its
outcome, as Dafny's JSON verification log gives it: Correct, Errors,
OutOfResource, TimedOut, Inconclusive or OutOfMemory.  The last columns (seconds;
resource count and number of proofs) are recorded but are not part of any
verdict, so a finished proof that becomes dearer or cheaper does not change one;
a proof that stops finishing within the limit does.

.github/review/expected-std-verdicts-z3-<version>.tsv holds the verdicts CI
expects under that Z3, and the job fails when one changes, or a row is added or
removed.  To change one on purpose, replace it with the file of the same name in
the run's artifact `std-verdicts-z3-<version>`, in a commit that says, for each
row whose verdict changed, why.
"""
import glob
import json
import os
import re
import subprocess
import sys
import tempfile
import time

TARGETS = ['notarget', 'cs', 'java', 'js', 'go', 'py']
FIXED = ['--verification-time-limit', '0']
TIMEOUT = 5400  # seconds, per part
KINDS = ['Correct', 'Errors', 'OutOfResource', 'TimedOut', 'Inconclusive', 'OutOfMemory']
ERRLINE = re.compile(r'^(\S[^\n]*?\(\d+,\d+\)): (?:Error|Verification out of resource|Verification of .* timed out)',
                     re.M)


def part(label, cwd, args, dafny, z3, cores, fixed):
    """Verify one part; return its run row and its declaration rows."""
    fd, log = tempfile.mkstemp(suffix='.json')
    os.close(fd)
    os.unlink(log)
    cmd = [dafny, 'verify'] + args + ['--solver-path', z3, '--cores', str(cores)] + fixed + \
          ['--log-format', 'json;LogFileName=' + log]
    print('+ (cd %s && %s)' % (cwd, ' '.join(cmd)), flush=True)
    start = time.monotonic()
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=TIMEOUT)
        out, rc = p.stdout + p.stderr, str(p.returncode)
    except subprocess.TimeoutExpired as e:
        text = lambda b: b.decode(errors='replace') if isinstance(b, bytes) else (b or '')
        out, rc = text(e.stdout) + text(e.stderr), 'TIMEOUT'
    secs = '%.1f' % (time.monotonic() - start)
    sys.stdout.write(out)
    fin = '; '.join(re.findall(r'verifier finished with ([^\n]*)', out))
    errs = sorted(set(ERRLINE.findall(out)))
    rows = [('run ' + label, rc, fin, ' '.join(errs), secs)]
    decls = []
    if os.path.exists(log):
        with open(log) as f:
            for r in json.load(f).get('verificationResults', []):
                decls.append((label + ' ' + r['name'], r['outcome'], '', '',
                              str(r.get('resourceCount', '')), str(len(r.get('vcResults', [])))))
        os.unlink(log)
    return rows + sorted(decls)


def run(libdir, dafny, z3, out, cores=4, boogie=None):
    ts = os.path.join(libdir, 'src', 'Std', 'TargetSpecific')
    # The paths as the Makefile passes them, so that error lines read as in upstream's CI.
    extra = [a for b in (boogie or '').split() for a in ('--boogie', b)]
    rows = part('Std', libdir, ['src/Std/dfyconfig.toml'], dafny, z3, cores, FIXED + extra)
    for t in TARGETS:
        files = sorted('./' + os.path.relpath(f, ts)
                       for f in glob.glob(os.path.join(ts, '**', '*-%s*.dfy' % t), recursive=True))
        rows += part('TargetSpecific-' + t, ts, ['dfyconfig.toml'] + files, dafny, z3, cores, FIXED)
    with open(out, 'w') as f:
        for r in rows:
            f.write('\t'.join(r) + '\n')
    print('%d rows written to %s' % (len(rows), out))


def load(fn):
    """Rows by name, padded to six columns, so that trailing tabs or CRLF do not matter."""
    with open(fn) as f:
        rows = [l.rstrip('\r\n').split('\t') for l in f if l.strip()]
    return {r[0]: (r + [''] * 6)[:6] for r in rows}


def verdict(row):
    return tuple(row[1:4])


def summary(fn):
    rows = load(fn)
    runs = [r for k, r in rows.items() if k.startswith('run ')]
    decls = [r for k, r in rows.items() if not k.startswith('run ')]
    print('| part | exit code | summary | seconds |')
    print('|---|---|---|---|')
    for r in sorted(runs):
        print('| %s | %s | %s | %s |' % (r[0][4:], r[1], r[2] or '(none)', r[4]))
    print()
    kinds = {}
    for r in decls:
        kinds[r[1]] = kinds.get(r[1], 0) + 1
    print('| declaration outcome | declarations |')
    print('|---|---|')
    for k in KINDS + sorted(set(kinds) - set(KINDS)):
        if k in kinds:
            print('| %s | %d |' % (k, kinds[k]))
    print('| **all** | **%d** |' % len(decls))
    bad = sorted(r for r in decls if r[1] != 'Correct')
    if bad:
        print()
        print('Declarations that did not verify:')
        print()
        for r in bad:
            print('- `%s`: %s (resource count %s)' % (r[0], r[1], r[4]))
    for r in sorted(runs):
        errs = [e for e in r[3].split(' ') if e]
        if errs:
            print()
            print('%s: lines with an error, a proof out of resource or a proof that timed out: %s.'
                  % (r[0][4:], ', '.join('`%s`' % e for e in errs)))


def expected(fn):
    rows = load(fn)
    for k in sorted(rows):
        print('\t'.join(rows[k][:4]))


def compare(expected, actual):
    a, b = load(expected), load(actual)
    n = 0
    for k in sorted(set(a) | set(b)):
        ra, rb = a.get(k), b.get(k)
        if ra is None or rb is None or verdict(ra) != verdict(rb):
            n += 1
            print(k)
            print('   expected', verdict(ra) if ra else 'missing')
            print('   actual  ', verdict(rb) if rb else 'missing')
    print(n, 'of', len(set(a) | set(b)), 'rows differ')
    return n


if __name__ == '__main__':
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    cmd, args = sys.argv[1], sys.argv[2:]
    if cmd == 'run':
        opts = dict(zip(args[4::2], args[5::2]))
        run(*args[:4], cores=int(opts.get('--cores', '4')), boogie=opts.get('--boogie'))
    elif cmd == 'summary':
        summary(*args)
    elif cmd == 'expected':
        expected(*args)
    elif cmd == 'compare':
        sys.exit(1 if compare(*args) else 0)
    else:
        sys.exit(__doc__)
