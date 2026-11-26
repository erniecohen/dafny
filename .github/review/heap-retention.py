#!/usr/bin/env python3
"""Build and run the offline heap graph controls or analyze an existing gcdump.

Examples:
  python3 .github/review/heap-retention.py -- --self-test
  python3 .github/review/heap-retention.py --work work/roots -- dump.gcdump '^Microsoft.Dafny.Program$'
"""
import argparse
import os
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / '.github/review/heap-retention/HeapRetention.csproj'


def build(project, work):
    env = dict(os.environ, MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
    temporary = work / 'tmp'
    temporary.mkdir()
    env['TMPDIR'] = str(temporary)
    with (work / 'build.log').open('w') as log:
        result = subprocess.run(['dotnet', 'build', str(project), '--artifacts-path', str(work / 'build'),
                                 '-p:NuGetAudit=false', '-p:UseSharedCompilation=false',
                                 '--disable-build-servers', '-m:1'],
                                env=env, stdout=log, stderr=subprocess.STDOUT, timeout=110)
    if result.returncode:
        for line in (work / 'build.log').read_text().splitlines():
            if re.search(r' error |Error\(s\)|Warning\(s\)|Build succeeded', line):
                print(line)
        raise RuntimeError('heap retention tool build failed')
    return work / 'build/bin/HeapRetention/debug/HeapRetention.dll', env


def execute(work, arguments):
    tool, env = build(PROJECT, work)
    return subprocess.run(['dotnet', str(tool), *arguments], env=env, timeout=110).returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, help='new directory inside this checkout\'s work/')
    parser.add_argument('arguments', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    arguments = args.arguments[1:] if args.arguments[:1] == ['--'] else args.arguments
    if not arguments:
        parser.error('supply --self-test or a dump and at least one type regular expression after --')
    (ROOT / 'work').mkdir(exist_ok=True)
    try:
        if args.work:
            work = args.work.resolve()
            if not work.is_relative_to(ROOT / 'work'):
                parser.error('--work must be inside this checkout\'s work/')
            work.mkdir(parents=True, exist_ok=False)
            return execute(work, arguments)
        with tempfile.TemporaryDirectory(prefix='heap-retention-', dir=ROOT / 'work') as directory:
            return execute(Path(directory), arguments)
    except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        parser.exit(1, str(error) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
