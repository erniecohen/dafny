#!/usr/bin/env python3
"""Compare the declaration rows of several std-verdicts.py runs of one tree (#30).

    process-history.py RUN.tsv RUN.tsv ...

The gate (review.yml, job stdlib) verifies the library with /restartProver, a fresh
solver process per proof batch, so its resource counts do not depend on the order in
which a reused process checked batches.  A user's run reuses processes, and under
Z3 5.1.0 a count can then move from run to run.  The process-history workflow runs
the library several times without /restartProver and prints, as Markdown:

  - the number of distinct resource-count vectors among the runs;
  - each declaration whose count or outcome differs, with its minimum, maximum,
    relative spread (max - min) / min and maximum as a fraction of the library's
    resource limit (1e6).

It records and never fails: a nonzero exit means a malformed input only.
"""
import sys

LIMIT = 1_000_000  # Source/DafnyStandardLibraries: --resource-limit 1e6


def rows(path):
    out = {}
    with open(path, encoding='utf-8') as f:
        for line in f:
            cols = line.rstrip('\r\n').split('\t')
            if not cols[0] or cols[0].startswith('run '):
                continue
            cols += [''] * (6 - len(cols))
            count = int(cols[4]) if cols[4].isdigit() else None
            out[cols[0]] = (cols[1], count)
    return out


def main(paths):
    runs = [rows(p) for p in paths]
    names = sorted(set().union(*runs))
    vectors = {tuple(r.get(n) for n in names) for r in runs}
    print(f'{len(runs)} runs, {len(names)} declarations, '
          f'{len(vectors)} distinct resource-count vector(s).')
    diff = []
    for n in names:
        vals = [r.get(n) for r in runs]
        if len(set(vals)) > 1:
            counts = [v[1] for v in vals if v and v[1] is not None]
            outcomes = sorted({v[0] if v else 'missing' for v in vals})
            lo, hi = (min(counts), max(counts)) if counts else (None, None)
            diff.append((n, outcomes, lo, hi))
    if not diff:
        print('\nNo declaration differs.')
        return 0
    print(f'\n{len(diff)} declaration(s) differ:\n')
    print('| declaration | outcomes | min | max | spread | max / limit |')
    print('|---|---|---|---|---|---|')
    for n, outcomes, lo, hi in sorted(diff, key=lambda d: -(d[3] or 0)):
        spread = f'{(hi - lo) / lo:.3%}' if lo else '-'
        frac = f'{hi / LIMIT:.2%}' if hi is not None else '-'
        print(f'| {n} | {", ".join(outcomes)} | {lo} | {hi} | {spread} | {frac} |')
    return 0


if __name__ == '__main__':
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1:]))
