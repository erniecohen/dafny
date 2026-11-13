#!/usr/bin/env python3
"""Historical M1 noninterference plus identity of current optional proof inputs.

The only added product files are explicitly included optional proof sources.
They are outside the library source glob, packaged libraries and compiler build.
At that exact milestone, no language option or solver fact was added.
The normal suite and source-Std jobs independently check their existing verdicts.
"""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
# Preserve the exact shipped M1 claim; do not advance the recorded baseline.
INTRODUCED = '25203d9115f8f104b325c8a058bf41051bdbbe69'
BASE = 'bb521a9522ec584d4b0069a8e80ba9e48291dee4'
ADDED = {'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy',
         'Source/DafnyStandardLibraries/optional/IntegerBitwiseDigits.dfy',
         'Source/DafnyStandardLibraries/optional/README.md'}
NONPRODUCT = ('Source/IntegrationTests/', 'docs/', '.github/', 'friction/')
DOCS = {'REVIEW.md','ROADMAP.md','AGENTS.md','CLAUDE.md'}


def product(path):
    return path not in DOCS and not path.startswith(NONPRODUCT)


def check(head='HEAD'):
    subprocess.run(['git','cat-file','-e',BASE+'^{commit}'],check=True,cwd=ROOT)
    parent = subprocess.check_output(['git','rev-parse',INTRODUCED+'^'],cwd=ROOT,text=True).strip()
    if parent != BASE:
        raise ValueError('Historical base must be the introducing commit parent')
    diff = subprocess.check_output(['git','diff','--no-renames','--name-status',BASE,INTRODUCED],cwd=ROOT,text=True)
    changes = [row.split('\t') for row in diff.splitlines()]
    unexpected = [r for r in changes if product(r[-1]) and not(r[0]=='A' and r[-1] in ADDED)]
    if unexpected:
        raise ValueError(f'Old product input changed, or unexpected product input added: {unexpected}')
    if {r[-1] for r in changes if product(r[-1])} != ADDED:
        raise ValueError('Historical milestone must add every optional input')
    if subprocess.run(['git','diff','--quiet',INTRODUCED,head,'--',*sorted(ADDED)],cwd=ROOT,check=False).returncode:
        raise ValueError('Current milestone inputs differ from the shipped M1 revision')
    # None of the new sources is part of the existing source-Std input glob.
    assert all(not p.startswith('Source/DafnyStandardLibraries/src/') for p in ADDED)
    # No embedded binary changes can be hidden by excluding the source glob.
    assert not any(r[-1].endswith('.doo') for r in changes)
    result = {'base':BASE,'introduced':INTRODUCED,
              'head':subprocess.check_output(['git','rev-parse',head],cwd=ROOT,text=True).strip(),
              'existing_product_input_identity':True,'optional_additions':sorted(r[-1] for r in changes if product(r[-1])),
              'current_optional_input_identity':True,
              'both_off':'At the shipped M1 revision: no default pipeline, solver theory, language option, source-Std or embedded library changes.',
              'scope':'Historical structural identity compares the exact shipped M1 revision and its parent. '
                      'Current optional sources must still match M1; later compiler inputs are outside this claim. '
                      'Normal review CI checks the full suite and source Std against their existing verdict baselines. '
                      'Language-enabled comparisons are unavailable until M2 introduces the option.'}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--head', default='HEAD')
    args = parser.parse_args()
    result = check(args.head)
    output=args.output;output.mkdir(parents=True,exist_ok=True)
    (output/'identity.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))

if __name__=='__main__':
    main()
