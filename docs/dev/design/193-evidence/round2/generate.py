#!/usr/bin/env python3
"""Materialize the small round-2 corpus; no product source is changed."""
from pathlib import Path
import argparse
import shutil
import re
import runpy


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('output', type=Path)
    args = ap.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    root = Path(__file__).resolve().parent
    # The ground consistency witness relies on these very certificates,
    # whose separate pure-* input is verified with additional axioms OFF.
    normalize = lambda s: re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', s))
    pure = normalize((root / 'pure-certificates64.dfy').read_text())
    for name in ('controls64.dfy', 'lambda-controls64.dfy'):
        source = (root / name).read_text()
        prefix = source[:source.index('lemma {:induction false} control_A64')]
        if normalize(prefix) != pure:
            raise ValueError('certificate source differs from pure input: ' + name)
    for source in sorted(root.glob('*.dfy')):
        shutil.copyfile(source, args.output / source.name)
    # Included declarations are not body-verified by an adapter invocation.
    # Give both standalone support files the runner's pure-* prefix so their
    # bodies are also checked independently with additional axioms OFF.
    boolean_root = root.parent / 'derived-boolean'
    boolean_names = {
        'reference.dfy': 'pure-boolean-reference.dfy',
        'native-identities.dfy': 'pure-boolean-native.dfy',
        **{f'w{width}-derived-boolean.dfy': f'w{width}-derived-boolean.dfy'
           for width in (8, 16, 32, 64)},
    }
    occupied = {source.name for source in root.glob('*.dfy')}
    for source_name, target_name in boolean_names.items():
        if target_name in occupied:
            raise ValueError('generated filename collision: ' + target_name)
        occupied.add(target_name)
        content = (boolean_root / source_name).read_text()
        content = content.replace('include "reference.dfy"',
                                  'include "pure-boolean-reference.dfy"')
        content = content.replace('include "native-identities.dfy"',
                                  'include "pure-boolean-native.dfy"')
        (args.output / target_name).write_text(content)
    # Keep the large finite-count native proofs reproducible without storing
    # their mechanical expansion in the design commit.
    rotation_root = root.parent / 'derived-rotation'
    rotation = runpy.run_path(str(rotation_root / 'generate.py'))['generate']
    rotation_sources = {'pure-math.dfy': (rotation_root / 'pure-math.dfy').read_text()}
    for width in (8, 16, 32, 64):
        for prefix, content in rotation(width).items():
            rotation_sources[f'{prefix}-{width}.dfy'] = content + '\n'
    for target_name, content in rotation_sources.items():
        if target_name in occupied:
            raise ValueError('generated filename collision: ' + target_name)
        occupied.add(target_name)
        (args.output / target_name).write_text(content)
    # Sensitivity check: an intentionally wrong A fact must make false
    # provable at the independently certified witness. This is not a control
    # expected to fail; its name distinguishes the deliberate mutation.
    source = (root / 'controls64.dfy').read_text()
    start = source.index('lemma {:induction false} control_A64')
    end = source.index('lemma {:induction false} control_C64', start)
    body = source[start:end].replace('control_A64', 'detect_corrupt_A64')
    body = body.replace('andNat(64,5,3);', 'andNat(64,5,3)+1;')
    body = body.replace('andNat(64,(5 as bv64) as int,(3 as bv64) as int);',
                        'andNat(64,(5 as bv64) as int,(3 as bv64) as int)+1;')
    (args.output / 'mutant-A64.dfy').write_text(source[:start] + body)
    # Paired ablations change only reference opacity, BV presence, width,
    # or the use of symbolic instead of concrete arguments. These are
    # diagnostics of model search, not acceptance controls.
    for width in (8, 64):
        modulus = 2 ** width
        for opaque in (False, True):
            for bridge in (False, True):
                for symbolic in (False, True):
                    declaration = ('opaque ' if opaque else '') + '''ghost function andNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a%2 == 1 && b%2 == 1 then 1 else 0)
    + 2*andNat(w-1,a/2,b/2) }
'''
                    if bridge:
                        declaration += f'''lemma {{:axiom}} bridge(a: nat, b: nat)
  requires a < {modulus} && b < {modulus}
  ensures (((a as bv{width}) & (b as bv{width})) as int) == andNat({width},a,b)
'''
                    a, b = ('a', 'b') if symbolic else ('5', '3')
                    declaration += 'lemma {:induction false} control_case('
                    declaration += 'a: nat, b: nat)\n' if symbolic else ')\n'
                    if symbolic:
                        declaration += f'  requires a < {modulus} && b < {modulus}\n'
                    declaration += '{\n'
                    if bridge:
                        declaration += f'  bridge({a},{b});\n'
                    else:
                        declaration += f'  var z := andNat({width},{a},{b});\n  assert 0 <= z;\n'
                    declaration += '  assert false;\n}\n'
                    name = 'ablation-' + '-'.join([
                        'opaque' if opaque else 'transparent',
                        'bridge' if bridge else 'integer',
                        'w' + str(width), 'symbolic' if symbolic else 'ground']) + '.dfy'
                    (args.output / name).write_text(declaration)
    print('materialized', len(list(args.output.glob('*.dfy'))), 'Dafny source files')


if __name__ == '__main__':
    main()
