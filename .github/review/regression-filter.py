#!/usr/bin/env python3
"""Partition the standing regression manifest without losing or duplicating tests."""
from pathlib import Path
import sys


def entries(path):
    rows = [row.strip() for row in path.read_text().splitlines() if row.strip() and not row.startswith('#')]
    if len(rows) != len(set(rows)):
        raise ValueError('Duplicate regression entry: ' + str(path))
    return rows


def partition(directory):
    all_tests = entries(directory / 'regressions')
    linux = entries(directory / 'regressions-linux')
    if not set(linux) <= set(all_tests):
        raise ValueError('Linux regression is not in the standing manifest')
    portable = [name for name in all_tests if name not in linux]
    if not portable or not linux:
        raise ValueError('Empty regression partition')
    return {'all': all_tests, 'portable': portable, 'linux': linux}


if __name__ == '__main__':
    print('|'.join('DisplayName~' + name for name in partition(Path(__file__).parent)[sys.argv[1]]))
