#!/usr/bin/env python3
"""Check the bounded-integer frontend against the independently checked model."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
FOUNDATION = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseCompilerFoundation.dfy'
OPS = [('And', '&', 'andNat'), ('Or', '|', 'orNat'), ('Xor', '^', 'xorNat'),
       ('Not', '!', 'notNat'), ('LeftShift', '<<', 'shlNat'), ('RightShift', '>>', 'shrNat')]


def clients(negative=False):
    lines = [f'include "{FOUNDATION.as_posix()}"',
             'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation',
             'import R = Std.Arithmetic.IntegerBitwise']
    # Split literal certificates so no recursive construction hides in a client.
    for j in range(65):
        lines += [f'lemma P{j}() ensures R.P({j}) == {2**j}',
                  f' ensures IntegerBitwiseModel.P({j}) == {2**j} {{']
        if j:
            lines += [f' P{j-1}();']
        lines += [f' F.Power({j}); IntegerBitwiseModel.unfoldP({j});', '}']
    for n in (8, 16, 32, 64):
        lines += [f'newtype {{:bitWidth {n}}} U{n} = x: nat | x < {2**n}']
        for lemma, op, raw in OPS:
            shift = 'Shift' in lemma
            params = f'a: U{n}, b: U{n}' + (', k: nat' if shift else '')
            args = f'{n},a as int' + ('' if op == '!' else ',k' if shift else ',b as int')
            expr = '!a' if op == '!' else f'a {op} ' + ('k' if shift else 'b')
            lines += [f'lemma {{:induction false}} {lemma}{n}({params})']
            if shift:
                lines += [f' requires k <= {n}']
            lines += ['{', f' P{n}();', f' F.{lemma}({args});']
            if op == '&':
                lines += [f' R.AndRange({n},a as int,b as int);']
            elif shift:
                lines += [f' R.ShiftViews({n},a as int,k);']
            else:
                lines += [f' R.BooleanRange({n},a as int,b as int);']
            lines += [f' assert ({expr}) as int == R.{raw}({args});']
            if negative:
                lines += [' assert false; // ERROR']
            lines += ['}']
    return lines


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', required=True, type=Path)
    parser.add_argument('--z3', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--resolver', choices=['legacy', 'refreshed', 'both'], default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    dafny = args.dafny.resolve()
    executable = ['dotnet', str(dafny)] if dafny.suffix == '.dll' else [str(dafny)]
    checks = []
    scopes = []

    def source(name, lines):
        path = output / (name + '.dfy')
        path.write_text('\n'.join(lines) + '\n')
        return path

    def invoke(name, command, expected=0, contains=()):
        process = subprocess.run(executable + command, capture_output=True, text=True, timeout=1200)
        text = process.stdout + process.stderr
        (output / (name + '.log')).write_text(text)
        assert process.returncode == expected, (name, process.returncode, text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name, text)
        for needle in contains:
            assert needle in text, (name, needle, text)
        checks.append(name)
        return text

    def verify(name, lines, common):
        path = source(name, lines)
        errors = {i for i, line in enumerate(lines, 1) if '// ERROR' in line}
        log = output / (name + '.csv')
        text = invoke(name, ['verify', str(path), *common, '--log-format', 'csv;LogFileName=' + str(log)], 4 if errors else 0)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
        assert actual == errors, (name, actual, errors, text)
        with log.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(row['TestResult.Outcome'] in ('Passed', 'Failed') for row in rows), (name, rows)
        scopes.extend(dict(check=name, **row) for row in rows)
        return path

    modes = ['legacy', 'refreshed'] if args.resolver == 'both' else [args.resolver]
    for mode in modes:
        common = ['--integer-bitwise', '--solver-path', str(args.z3.resolve()), '--cores', '2',
                  '--standard-libraries:false', '--show-snippets:false', '--allow-warnings:false',
                  '--type-system-refresh:' + str(mode == 'refreshed').lower(), '--resource-limit', '200000',
                  '--verification-time-limit', '60', '--error-limit', '0',
                  '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']
        if args.additional_axioms:
            common += ['--additional-axioms']
        verify(mode + '-model', clients(), common)
        verify(mode + '-false', clients(True), common)
        for lemma, op, raw in OPS:
            shift = 'Shift' in lemma
            expr = '!a' if op == '!' else f'a {op} ' + ('8' if shift else 'b')
            verify(mode + '-raw-range-' + lemma, [
                'newtype {:bitWidth 8} U = x:nat | x < 256',
                'lemma NoAssumedRange(a:U,b:U) {', f' var r := {expr}; // ERROR', '}'], common)
        for count in (-1, 9):
            for op, raw, unfold in [('<<', 'shlNat', 'Shl'), ('>>', 'shrNat', 'Shr')]:
                verify(f'{mode}-count-{raw}-{count}', [
                    'newtype {:bitWidth 8} U = x:nat | x < 256',
                    'lemma BadCount(a:U) {',
                    f' IntegerBitwiseModel.unfold{unfold}(8,a as int,{count});',
                    f' var r := a {op} {count}; // ERROR', '}'], common)
        # Count 0 and N, an independently typed BV count, and a stricter result.
        verify(mode + '-endpoints', [
            'newtype {:bitWidth 8} U = x:int | 0 <= x < 256',
            'newtype {:bitWidth 8} NZ = x:nat | 0 < x < 256 witness 1',
            'type Alias = U',
            'lemma Endpoints(a:Alias) {',
            ' IntegerBitwiseModel.unfoldShl(8,a as int,8);',
            ' IntegerBitwiseModel.unfoldShr(8,a as int,8);',
            *[f' IntegerBitwiseModel.unfoldP({j});' for j in range(9)],
            ' IntegerBitwiseModel.unfoldNorm(8,a as int);',
            ' IntegerBitwiseModel.unfoldShl(8,a as int,0);',
            ' IntegerBitwiseModel.unfoldShr(8,a as int,0);',
            ' assert (a << 0) == a;', ' assert (a >> 0) == a;',
            ' assert (a << 8) as int == 0;',
            ' assert (a >> (8 as bv4)) as int == 0;',
            ' IntegerBitwiseModel.unfoldShr(8,0,8);',
            ' assert ((a << 8) >> 8) as int == 0;',
            '}',
            'lemma Strict(a:NZ) {',
            ' IntegerBitwiseModel.unfoldShr(8,a as int,8);',
            ' assert (a >> 8) as int == 0; // ERROR', '}'], common)
        # Reveal the actual raw equations, then require the entire strict predicate.
        verify(mode + '-strict', [
            'newtype {:bitWidth 8} NZ = x:nat | 0 < x < 256 witness 1',
            'lemma StrictNot() {',
            *[f' IntegerBitwiseModel.unfoldP({j});' for j in range(9)],
            ' IntegerBitwiseModel.unfoldNorm(8,255);',
            ' IntegerBitwiseModel.unfoldNot(8,255);',
            ' assert (!(255 as NZ)) as int == 0; // ERROR', '}',
            'lemma StrictAnd() {',
            *[f' IntegerBitwiseModel.unfoldAnd({8-j},{1 >> j},{2 >> j});' for j in range(9)],
            ' assert ((1 as NZ) & (2 as NZ)) as int == 0; // ERROR', '}'], common)
        for name, declaration in {
            'zero': 'newtype {:bitWidth 0} T = x:nat | x < 1',
            'excessive': 'newtype {:bitWidth 2147483648} T = x:nat | x < 1',
            'nonliteral': 'newtype {:bitWidth 4+4} T = x:nat | x < 256',
            'negative': 'newtype {:bitWidth -1} T = x:nat | x < 256',
            'wide-range': 'newtype {:bitWidth 7} T = x:nat | x < 256',
            'missing-lower': 'newtype {:bitWidth 8} T = x:int | x < 256',
            'wrong-base': 'newtype {:bitWidth 8} T = x:real | 0.0 <= x < 256.0',
            'duplicate': 'newtype {:bitWidth 8} {:bitWidth 7} T = x:nat | x < 128',
            'wrong-arity': 'newtype {:bitWidth 8,9} T = x:nat | x < 256',
        }.items():
            path = source(mode + '-declaration-' + name, [declaration])
            invoke(mode + '-declaration-' + name, ['resolve', str(path), *common[:common.index('--resource-limit')]], 2, [':bitWidth requires'])
        for name, lines in {
            'different': ['newtype {:bitWidth 8} U = x:nat | x < 256',
                          'newtype {:bitWidth 8} V = x:nat | x < 256',
                          'lemma Different(a:U,b:V) { var r := a & b; }'],
            'plain': ['lemma Plain(a:int,b:int) { var r := a & b; }'],
            'unannotated-nested': ['newtype {:bitWidth 8} U = x:nat | x < 256',
                                   'newtype V = U',
                                   'ghost function F(a:V,b:V):V { a & b }'],
            'opaque': ['module L { export provides U newtype {:bitWidth 8} U = x:nat | x < 256 }',
                       'module C { import L lemma Hidden(a:L.U,b:L.U) { var r := a & b; } }'],
            'compiled': ['newtype {:bitWidth 8} U = x:nat | x < 256',
                         'function Compiled(a:U,b:U):U { a & b }'],
        }.items():
            path = source(mode + '-reject-' + name, lines)
            invoke(mode + '-reject-' + name, ['resolve', str(path), *common[:common.index('--resource-limit')]], 2)
        if mode == 'refreshed':
            members = source(mode + '-member-invariants', [
                'newtype {:bitWidth 8} T = x:nat | x < 256 witness 1 {',
                ' invariant this != 0', '}'])
            invoke(mode + '-member-invariants', ['resolve', str(members),
                   *common[:common.index('--resource-limit')], '--member-invariants',
                   '--general-newtypes'], 2, [':bitWidth requires an integer newtype with no member invariants'])
        # Reconstruct source operators through separate compilation and exports.
        library_source = source(mode + '-library', [
            'module BitLibrary {',
            ' export reveals U, Mask',
            ' newtype {:bitWidth 8} U = x:nat | x < 256',
            ' ghost function Mask(a:U,b:U):int',
            '  requires 0 <= IntegerBitwiseModel.andNat(8,a as int,b as int) < 256',
            ' { (a & b) as int }', '}'])
        library = output / (mode + '-library.doo')
        invoke(mode + '-library-build', ['build', str(library_source), '--target:lib', *common, '--output', str(library)])
        import zipfile
        with zipfile.ZipFile(library) as archive:
            program = archive.read('program').decode()
            assert re.search(r'a\s*&\s*b', program), program
        client = source(mode + '-library-client', [
            'import L = BitLibrary',
            'lemma Client(a:L.U,b:L.U)',
            ' requires 0 <= IntegerBitwiseModel.andNat(8,a as int,b as int) < 256',
            '{ assert L.Mask(a,b) == ((a & b) as int); }'])
        invoke(mode + '-library-reload', ['verify', str(client), *common, '--library', str(library)])
        invoke(mode + '-library-disabled', ['resolve', str(client), '--standard-libraries:false', '--library', str(library)],
               1, ['--integer-bitwise is set locally to False, but the library was built with True'])
        off_source = source(mode + '-off-library', [
            'module OffLibrary { newtype {:bitWidth 8} U = x:nat | x < 256 }'])
        off_library = output / (mode + '-off-library.doo')
        invoke(mode + '-off-library-build', ['build', str(off_source), '--target:lib', *common,
               '--integer-bitwise:false', '--output', str(off_library)])
        off_client = source(mode + '-off-client', ['import L = OffLibrary',
            'ghost function F(a:L.U,b:L.U):L.U { a & b }'])
        invoke(mode + '-off-capability', ['resolve', str(off_client), *common[:common.index('--resource-limit')],
               '--library', str(off_library)], 2, ['require --integer-bitwise'])
        refinement = source(mode + '-refinement', [
            'abstract module A {',
            ' newtype {:bitWidth 8} U = x:nat | x < 256',
            ' ghost function Mask(a:U,b:U):int',
            ' requires 0 <= IntegerBitwiseModel.andNat(8,a as int,b as int) < 256',
            ' { (a & b) as int }', '}',
            'module B refines A {',
            ' lemma Check(a:U,b:U)',
            ' requires 0 <= IntegerBitwiseModel.andNat(8,a as int,b as int) < 256',
            ' { assert Mask(a,b) == ((a & b) as int); }', '}'])
        invoke(mode + '-refinement', ['verify', str(refinement), *common])
        if mode == 'refreshed':
            invoke(mode + '-user-operators', ['verify', str(refinement), *common,
                   '--user-defined-operators'])
        # Unannotated nested types do not inherit a semantic width; synonyms do.
        nested = source(mode + '-nested', [
            'newtype {:bitWidth 8} U = x:nat | x < 256',
            'newtype {:bitWidth 8} V = U',
            'ghost function F(a:V,b:V):V { a & b }'])
        invoke(mode + '-nested-resolve', ['resolve', str(nested), *common[:common.index('--resource-limit')]])
        verify(mode + '-carrier-width', [
            'newtype {:bitWidth 12} {:nativeType "byte"} Small = x:nat | x < 256',
            'lemma Carrier() {',
            *[f' IntegerBitwiseModel.unfoldP({j});' for j in range(13)],
            ' IntegerBitwiseModel.unfoldNorm(12,16);',
            ' IntegerBitwiseModel.unfoldNot(12,16);',
            ' assert (!(16 as Small)) as int == 4079; // ERROR', '}'], common)
        verify(mode + '-old-intrinsics', [
            'lemma Existing(a:bv8,b:bv8,p:bool) {',
            ' assert (a & b) == (b & a);', ' assert !p == (p == false);',
            ' assert (a << 8) == 0;', '}'], common)
    (output / 'summary.json').write_text(json.dumps(dict(checks=checks, scopes=scopes), indent=2) + '\n')
    print('Integer-newtype bitwise frontend checks passed')


if __name__ == '__main__':
    main()
