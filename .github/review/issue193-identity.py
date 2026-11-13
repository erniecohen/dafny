#!/usr/bin/env python3
"""M1 noninterference: all old compiler/runtime/library/build inputs are identical.

The only added product files are explicitly included optional proof sources.
They are outside the library source glob, packaged libraries and compiler build.
No new option, intrinsic, resolver rule, prelude declaration or solver fact exists.
The normal suite and source-Std jobs independently check their existing verdicts.
"""
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASE = Path(__file__).with_name('issue193-base').read_text().strip()
ADDED = {'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy',
         'Source/DafnyStandardLibraries/optional/IntegerBitwiseDigits.dfy',
         'Source/DafnyStandardLibraries/optional/README.md'}
NONPRODUCT = ('Source/IntegrationTests/', 'docs/', '.github/', 'friction/')
DOCS = {'REVIEW.md','ROADMAP.md','AGENTS.md','CLAUDE.md'}


def product(path):
    return path not in DOCS and not path.startswith(NONPRODUCT)


def main():
    subprocess.run(['git','cat-file','-e',BASE+'^{commit}'],check=True,cwd=ROOT)
    diff = subprocess.check_output(['git','diff','--name-status',BASE,'HEAD'],cwd=ROOT,text=True)
    changes = [row.split('\t') for row in diff.splitlines()]
    unexpected = [r for r in changes if product(r[-1]) and not(r[0]=='A' and r[-1] in ADDED)]
    if unexpected:
        raise ValueError(f'Old product input changed, or unexpected product input added: {unexpected}')
    # None of the new sources is part of the existing source-Std input glob.
    assert all(not p.startswith('Source/DafnyStandardLibraries/src/') for p in ADDED)
    # No embedded binary changes can be hidden by excluding the source glob.
    assert not any(r[-1].endswith('.doo') for r in changes)
    result = {'base':BASE,'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
              'existing_product_input_identity':True,'optional_additions':sorted(r[-1] for r in changes if product(r[-1])),
              'both_off':'No default pipeline, solver theory, language option, source-Std or embedded library changes.',
              'scope':'Structural identity for all existing product inputs; new optional proofs checked separately. '
                      'Normal review CI checks the full suite and source Std against their existing verdict baselines. '
                      'Language-enabled comparisons are unavailable until M2 introduces the option.'}
    output=Path(sys.argv[1]);output.mkdir(parents=True,exist_ok=True)
    (output/'identity.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))

if __name__=='__main__':
    main()
