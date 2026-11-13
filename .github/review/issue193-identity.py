#!/usr/bin/env python3
"""M1 noninterference at its shipped introduction, plus current optional identity.

The only added product files are explicitly included optional proof sources.
They are outside the library source glob, packaged libraries and compiler build.
Later independent compiler fixes are qualified by their own and the normal CI
gates. They must not invalidate or silently redefine this historical claim.
"""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASE = Path(__file__).with_name('issue193-base').read_text().strip()
INTRODUCED = Path(__file__).with_name('issue193-introduced').read_text().strip()
ADDED = {'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy',
         'Source/DafnyStandardLibraries/optional/IntegerBitwiseDigits.dfy',
         'Source/DafnyStandardLibraries/optional/README.md'}
NONPRODUCT = ('Source/IntegrationTests/', 'docs/', '.github/', 'friction/')
DOCS = {'REVIEW.md','ROADMAP.md','AGENTS.md','CLAUDE.md'}


def product(path):
    return path not in DOCS and not path.startswith(NONPRODUCT)


def check():
    subprocess.run(['git','cat-file','-e',BASE+'^{commit}'],check=True,cwd=ROOT)
    parent = subprocess.check_output(['git','rev-parse',INTRODUCED+'^'],cwd=ROOT,text=True).strip()
    if parent != BASE:
        raise ValueError('The pinned base is not the introducing commit parent')
    diff = subprocess.check_output(['git','diff','--no-renames','--name-status',BASE,INTRODUCED],cwd=ROOT,text=True)
    changes = [row.split('\t') for row in diff.splitlines()]
    unexpected = [r for r in changes if product(r[-1]) and not(r[0]=='A' and r[-1] in ADDED)]
    if unexpected:
        raise ValueError(f'Old product input changed, or unexpected product input added: {unexpected}')
    if {r[-1] for r in changes if product(r[-1])} != ADDED:
        raise ValueError('The introducing commit must contain every optional input')
    if subprocess.run(['git','diff','--quiet',INTRODUCED,'HEAD','--',*sorted(ADDED)],cwd=ROOT).returncode:
        raise ValueError('Current optional inputs differ from the introducing commit')
    # None of the new sources is part of the existing source-Std input glob.
    assert all(not p.startswith('Source/DafnyStandardLibraries/src/') for p in ADDED)
    # No embedded binary changes can be hidden by excluding the source glob.
    assert not any(r[-1].endswith('.doo') for r in changes)
    return {'base':BASE,'introduced':INTRODUCED,
              'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
              'existing_product_input_identity':True,'optional_additions':sorted(r[-1] for r in changes if product(r[-1])),
              'current_optional_input_identity':True,
              'both_off':'The introducing commit changes no existing pipeline, solver theory, language option, source-Std or embedded library.',
              'scope':'Existing product identity compares the pinned introducing commit with its parent, not current HEAD. '
                      'Current optional inputs must still match that commit. Later compiler changes are checked by normal review CI. '
                      'Language-enabled comparisons are unavailable until M2 introduces the option.'}


def main():
    result = check()
    output=Path(sys.argv[1]);output.mkdir(parents=True,exist_ok=True)
    (output/'identity.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))

if __name__=='__main__':
    main()
