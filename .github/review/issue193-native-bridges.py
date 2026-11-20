#!/usr/bin/env python3
"""Check native Boolean bridges, included proofs, and live controls."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNative.dfy'
OPS = [('And', '&', 'andNat'), ('Not', '!', 'notNat'), ('Or', '|', 'orNat'),
       ('Xor', '^', 'xorNat'), ('AndNot', '&!', 'andNotNat')]


def declarations(text):
    """The library deliberately uses one-line signatures and contracts."""
    module = None
    result = []
    for line in text.splitlines():
        match = re.match(r'module Std.Arithmetic.IntegerBitwiseNative(\d+) \{', line)
        if match:
            module = int(match[1])
        match = re.search(r'lemma (?:\{:[^}]+\}\s*)*(\w+)\(', line)
        if match:
            result.append((module, match[1]))
    return result


def fixtures():
    lines = [f'include "{LIBRARY.as_posix()}"',
             'import R = Std.Arithmetic.IntegerBitwise',
             'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation']
    false = lines.copy()
    negative = lines.copy()
    for w in (8, 16, 32, 64):
        module = f'Std.Arithmetic.IntegerBitwiseNative{w}'
        lines += [f'import B{w} = {module}']
        false += [f'import B{w} = {module}']
        negative += [f'import B{w} = {module}']
        for name, op, raw in OPS:
            binary = op != '!'
            expr = '!x' if not binary else f'x {op} y'
            args = 'x,y' if binary else 'x'
            ints = 'x as int,y as int' if binary else 'x as int'
            lines += [f'lemma {{:induction false}} {name}{w}(x:bv{w},y:bv{w})',
                      f' ensures ({expr}) as int==R.{raw}({w},{ints})', '{',
                      f' B{w}.{name}({args}); F.{name}({w},{ints});', '}']
            converted = expr.replace('x', f'(a as bv{w})').replace('y', f'(b as bv{w})')
            model = 'a,b' if binary else 'a'
            lines += [f'lemma {{:induction false}} {{:isolate_assertions}} Composed{name}{w}(a:nat,b:nat)',
                      f' requires a<{2**w} && b<{2**w}',
                      f' ensures ({converted}) as int==R.{raw}({w},{model})', '{',
                      f' B{w}.RoundTrip(a); B{w}.RoundTrip(b);',
                      f' var x,y:=a as bv{w},b as bv{w};',
                      f' assert x as int==a; assert y as int==b;', f' {name}{w}(x,y);', '}']
            # The bridge stays active before a deliberately wrong observation.
            false += [f'lemma {{:induction false}} Wrong{name}{w}() {{',
                      f' var x,y:bv{w}:=5,3;', f' B{w}.{name}({args});',
                      f' assert ({expr}) as int!=IntegerBitwiseModel.{raw}({w},{ints}); // ERROR', '}']
        for name, args in [('RoundTrip', '-1'), ('RoundTrip', str(2**w)),
                           (f'NoOverflow{w}', f'{2**w-1},1'), (f'Next{w}', str(2**w-1)),
                           ('Successor', '5,5'),
                           ('Successor', f'{2**w-1},{2**w}')]:
            negative += [f'lemma Negative{w}_{len(negative)}() {{', f' B{w}.{name}({args}); // ERROR', '}']
        negative += [f'lemma BadCast{w}() {{ var x:= {2**w} as bv{w}; // ERROR', '}']
        calls = {f'Add{w}': '5,3', f'Widen{w}': '5', f'NoOverflow{w}': '5,3',
                 f'Split{w}': '5', f'DigitSplit{w}': '5', f'Next{w}': '5',
                 f'Inverse{w}': '5', 'Successor': '5,6', 'RoundTrip': '5',
                 f'Zero{w}': '0', f'AndStep{w}': '5,3', f'AndPrefix{w}': f'{w},5,3',
                 'AndRecurrence': '1,1,1,0,1', 'NativeNot': '5',
                 'And': '5,3', 'Not': '5', 'Or': '5,3', 'Xor': '5,3', 'AndNot': '5,3',
                 'Power': '', 'WidenTo64': '5'}
        for width, name in declarations(LIBRARY.read_text()):
            if width != w:
                continue
            false += [f'lemma {{:induction false}} False{w}_{name}() {{',
                      f' B{w}.Power();', *([' IntegerBitwiseModel.unfoldAnd(0,0,0);'] if name == 'AndRecurrence' else []), f' B{w}.{name}({calls[name]});', ' assert false; // ERROR', '}']
        if w == 64:
            negative += ['lemma BadPrefix() { B64.Power(); B64.AndPrefix64(0,1,0); // ERROR', '}',
                         'lemma BadPrefixY() { B64.Power(); B64.AndPrefix64(0,0,1); // ERROR', '}',
                         'lemma BadRecurrence() { B64.AndRecurrence(0,1,1,0,1); // ERROR', '}',
                         'lemma BadRecurrenceHalf() { B64.AndRecurrence(1,1,1,1,3); // ERROR', '}',
                         'lemma BadRecurrenceResult() { B64.AndRecurrence(1,1,1,0,0); // ERROR', '}']
    return lines, false, negative


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', type=Path, required=True)
    parser.add_argument('--z3', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--resolver', choices=['legacy', 'refreshed', 'both'], default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    parser.add_argument('--mutations', action='store_true')
    parser.add_argument('--mutations-only', action='store_true')
    parser.add_argument('--mutation-filter', default='')
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    exe = ['dotnet', str(args.dafny.resolve())] if args.dafny.suffix == '.dll' else [str(args.dafny.resolve())]
    common = ['--integer-bitwise', '--solver-path', str(args.z3.resolve()), '--cores', '2',
              '--standard-libraries:false', '--allow-warnings:false', '--show-snippets:false',
              '--verification-time-limit', '60', '--error-limit', '0',
              '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']
    if args.additional_axioms:
        common += ['--additional-axioms']
    records = []

    def verify(name, path, extra=(), expected_lines=(), mutation=False, limit=200000):
        log = out / (name + '.csv')
        result = subprocess.run(exe + ['verify', str(path), *common, '--resource-limit', str(limit),
                                     '--log-format', 'csv;LogFileName=' + str(log), *extra],
                                capture_output=True, text=True, timeout=1200)
        text = result.stdout + result.stderr
        (out / (name + '.log')).write_text(text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name, text)
        if mutation:
            assert result.returncode == 4 and 'could not be proved' in text, (name, result.returncode, text)
        else:
            assert result.returncode == (4 if expected_lines else 0), (name, result.returncode, text)
            actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
            assert actual == set(expected_lines), (name, actual, expected_lines, text)
        with log.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        records.append(dict(name=name, exit=result.returncode, scopes=len(rows),
                            failed=sum(r['TestResult.Outcome'] == 'Failed' for r in rows)))

    modes = ['legacy', 'refreshed'] if args.resolver == 'both' else [args.resolver]
    for mode in ([] if args.mutations_only else modes):
        option = ['--type-system-refresh:' + str(mode == 'refreshed').lower()]
        verify(mode + '-bodies', LIBRARY, [*option, '--verify-included-files'], limit=2000000)
        for kind, lines in zip(['clients', 'false', 'negative'], fixtures()):
            path = out / (mode + '-' + kind + '.dfy')
            path.write_text('\n'.join(lines) + '\n')
            errors = {i for i, line in enumerate(lines, 1) if '// ERROR' in line}
            verify(mode + '-' + kind, path, option, errors, limit=2000000 if kind == 'clients' else 200000)
    if args.mutations or args.mutations_only:
        mutation_start = len(records)
        original = LIBRARY.read_text()
        # Negate each proved rule separately. A body must reject its changed
        # contract even when its clients would become inconsistent under it.
        pattern = r'(lemma (?:\{:[^}]+\}\s*)*(\w+)\([^\n]*\n(?:[^\n]*\n)*?)(  +ensures )([^\n]+)'
        matches = list(re.finditer(pattern, original))
        assert len(matches) == len(declarations(original)), (len(matches), declarations(original))
        for i, match in enumerate(matches):
            mutated = original[:match.start(4)] + '!(' + match[4] + ')' + original[match.end(4):]
            # Keep included paths valid without editing the authoritative source.
            mutated = mutated.replace('include "IntegerBitwiseCompilerFoundation.dfy"',
                                      'include "' + (LIBRARY.parent / 'IntegerBitwiseCompilerFoundation.dfy').as_posix() + '"')
            path = out / 'mutation.dfy'
            path.write_text(mutated)
            width, name = declarations(original)[i]
            if args.mutation_filter and args.mutation_filter not in f'{width}.{name}':
                continue
            verify(f'mutation-{width}-{name}', path,
                   ['--type-system-refresh:true', '--filter-symbol', f'IntegerBitwiseNative{width}.{name}'],
                   mutation=True, limit=2000000)
        path.unlink()
        assert len(records) > mutation_start, 'No mutation selected'
    assert records, 'No verification or mutation selected'
    (out / 'summary.json').write_text(json.dumps(records, indent=2) + '\n')
    print('Native bridge checks passed')


if __name__ == '__main__':
    main()
