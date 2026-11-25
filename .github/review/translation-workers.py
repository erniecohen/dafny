#!/usr/bin/env python3
"""Checks for --translation-workers (https://github.com/erniecohen/dafny/issues/241, F1).

    translation-workers.py bpl <prefix> <prefix>...   the per-module Boogie files <prefix>_<module>.bpl
                                                       of every run are the same files, byte for byte
    translation-workers.py parallel <profile.jsonl>    translations ran at once and committed in order
    translation-workers.py serial <profile.jsonl>      the run fell back to one worker
    translation-workers.py first-line <file>           the first line of a rejected run's output
    translation-workers.py tokens <profile.jsonl>...   every run's translation of each module has the
                                                       same token digest (issue 569)
    translation-workers.py tokens-moved <a> <b>        no module's token digest is the same in a and b
    translation-workers.py persistent <profile.jsonl>...  every module of every run was translated for
                                                       the persistent tables (--persistent-translation)
                                                       and recorded
    translation-workers.py shifted <source> <copy>     a copy of the source one line further down

`parallel` is the vacuity control of the identity checks: it requires a translation that
finished before an earlier-arriving one (so translations overlapped), and that the commits
appear in the profile in arrival order, one per translation.

`tokens` compares what the printed Boogie text omits: the source locations of every node of each
module's Boogie graph, which the profile records under --translation-workers-stress.
`tokens-moved` is its sensitivity control: the digests of a program and of its shifted copy differ.
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


def digests(path):
    result = {}
    for span in spans(path, 'translate'):
        assert span.get('tokens'), ('no token digest', span.get('module'))
        assert span['module'] not in result, ('module translated twice', span['module'])
        result[span['module']] = span['tokens']
    assert len(result) > 1, 'expected a translation per module'
    return result


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
    elif command == 'tokens':
        first = digests(argv[2])
        for path in argv[3:]:
            other = digests(path)
            assert sorted(other) == sorted(first), (os.path.basename(path), sorted(first), sorted(other))
            changed = sorted(m for m in first if first[m] != other[m])
            assert not changed, (os.path.basename(path), changed)
        print('Boogie tokens identical: %d modules, %d runs' % (len(first), len(argv) - 2))
    elif command == 'tokens-moved':
        first, other = digests(argv[2]), digests(argv[3])
        assert sorted(other) == sorted(first), (sorted(first), sorted(other))
        same = sorted(m for m in first if first[m] == other[m])
        assert not same, ('token digest blind to a moved source', same)
        print('Boogie tokens follow the source: %d modules moved' % len(first))
    elif command == 'persistent':
        counts = set()
        for path in argv[2:]:
            translations = spans(path, 'translate')
            assert translations, ('no translation in the profile', os.path.basename(path))
            unrecorded = sorted(t['module'] for t in translations if t.get('persistent') is not True)
            assert not unrecorded, ('not on the persistent path', os.path.basename(path), unrecorded)
            counts.add(len(translations))
        assert len(counts) == 1, counts
        print('persistent translations recorded: %d modules, %d runs' % (counts.pop(), len(argv) - 2))
    elif command == 'shifted':
        with open(argv[2]) as f:
            text = f.read()
        with open(argv[3], 'w') as f:
            f.write('\n' + text)
    elif command == 'first-line':
        with open(argv[2]) as f:
            print(f.readline().rstrip('\n'))
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    main(sys.argv)
