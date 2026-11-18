#!/usr/bin/env python3
"""Route historical identity checks without freezing unrelated product changes."""
import argparse
import os
from pathlib import Path
import subprocess

CONTRACT_BASE = 'b98637c6677560944b2498002ecd32ec987a90af'
CONTRACT_HEAD = 'd598f87ebb0a906d1fd2122cb9b2445a9bf00ee4'  # Shipped source-contract milestone (#269).
BITWISE = ('Source/DafnyStandardLibraries/optional/',
           '.github/review/issue193-', '.github/review/test_issue193_')
CONTRACTS = ('Source/DafnyCore/Resolver/ModularTermination.cs',
             'Source/IntegrationTests/ModularTerminationContractTests.cs',
             '.github/review/source-contracts-identity.py')


def scope(event, paths):
    manual = event == 'workflow_dispatch'
    pr = event == 'pull_request'
    return {'integer_bitwise': manual or pr and any(
                path.startswith(BITWISE) or 'git-issue-193-' in path for path in paths),
            'source_contracts': manual or pr and any(path in CONTRACTS for path in paths)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--event', required=True)
    parser.add_argument('--base')
    parser.add_argument('--head', default='HEAD')
    args = parser.parse_args()
    paths = []
    base, candidate = CONTRACT_BASE, CONTRACT_HEAD
    if args.event == 'pull_request':
        if not args.base:
            parser.error('--base is required for pull requests')
        base = subprocess.check_output(['git', 'merge-base', args.base, args.head], text=True).strip()
        candidate = args.head
        # No rename folding: both deletion and addition must participate in routing.
        paths = subprocess.check_output(['git', 'diff', '--no-renames', '--name-only', '-z',
                                         base, candidate], text=True).split('\0')
    result = scope(args.event, paths)
    result.update(base=base, candidate=candidate)
    lines = [f'{key}={str(value).lower() if isinstance(value, bool) else value}'
             for key, value in result.items()]
    print('\n'.join(lines))
    if os.environ.get('GITHUB_OUTPUT'):
        with Path(os.environ['GITHUB_OUTPUT']).open('a') as output:
            output.write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
