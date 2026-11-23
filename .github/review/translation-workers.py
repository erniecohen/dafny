#!/usr/bin/env python3
"""Checks for --translation-workers (https://github.com/erniecohen/dafny/issues/241, F1).

    translation-workers.py bpl <prefix> <prefix>...   the per-module Boogie files <prefix>_<module>.bpl
                                                       of every run are the same files, byte for byte
    translation-workers.py parallel <profile.jsonl>    translations ran at once and committed in order
    translation-workers.py serial <profile.jsonl>      the run fell back to one worker
    translation-workers.py first-line <file>           the first line of a rejected run's output

`parallel` is the vacuity control of the identity checks: it requires a translation that
finished before an earlier-arriving one (so translations overlapped), and that the commits
appear in the profile in arrival order, one per translation.
"""
import glob
import json
import os
import sys


def modules(prefix):
    files = {}
    for path in glob.glob(glob.escape(prefix) + '_*.bpl'):
        with open(path, 'rb') as f:
            files[path[len(prefix) + 1:]] = f.read()
    return files


def spans(path, stage):
    with open(path) as f:
        events = [json.loads(line) for line in f if line.strip()]
    return [e for e in events if e.get('ev') == 'span' and e.get('stage') == stage]


def main(argv):
    command = argv[1]
    if command == 'bpl':
        first = modules(argv[2])
        assert len(first) > 1, 'expected a Boogie file per module'
        for prefix in argv[3:]:
            other = modules(prefix)
            assert sorted(other) == sorted(first), (os.path.basename(prefix), sorted(first), sorted(other))
            changed = [m for m in first if first[m] != other[m]]
            assert not changed, (os.path.basename(prefix), changed)
        print('Boogie text identical: %d modules, %d runs' % (len(first), len(argv) - 2))
    elif command == 'parallel':
        commits = spans(argv[2], 'translate.commit')
        translations = spans(argv[2], 'translate')
        assert commits and len(commits) == len(translations), (len(commits), len(translations))
        tickets = [c['ticket'] for c in commits]
        assert tickets == list(range(len(commits))), tickets
        order = {c['module']: c['ticket'] for c in commits}
        ends = sorted(translations, key=lambda t: t['end'])
        overtaken = any(order[a['module']] > order[b['module']] for i, a in enumerate(ends) for b in ends[i + 1:])
        assert overtaken, 'no translation finished before an earlier one: nothing ran at once'
        print('translations overlapped; %d commits in arrival order' % len(commits))
    elif command == 'serial':
        assert spans(argv[2], 'translate'), 'no translation in the profile'
        assert not spans(argv[2], 'translate.commit'), 'the run used the parallel path'
        print('fell back to one translation worker')
    elif command == 'first-line':
        with open(argv[2]) as f:
            print(f.readline().rstrip('\n'))
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    main(sys.argv)
