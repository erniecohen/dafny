#!/usr/bin/env python3
"""Check native shifts and modular addition, proof bodies and live controls."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeShifts.dfy'


def declarations(text):
    module = None
    result = []
    for line in text.splitlines():
        match = re.match(r'module Std.Arithmetic.(\w+) \{', line)
        if match:
            module = match[1]
        match = re.search(r'lemma (?:\{:[^}]+\}\s*)*(\w+)\(', line)
        if match:
            result.append((module, match[1]))
    return result


def calls(module, name):
    if module.endswith('Arithmetic'):
        return {'Reduce': '5,3,8,0,256', 'DoubleMod': '5,256',
                'RightArithmetic': '6,3,3,1', 'RightZeroArithmetic': '5,5,0',
                'LeftArithmetic': '5,5,10,1,256', 'ZeroArithmetic': '5,5,0,256'}[name]
    if 'Count' in module:
        return '5,3' if name.startswith('Add') else '5'
    return {'Carry': '5,3', 'CarryValue': '5', 'Plus': '5,3',
            'PlusWitness': '5,3,8', 'CountBound': '3',
            'RightStep': '5,1', 'LeftStep': '5,1',
            'RightWitness': '5,1,2', 'LeftWitness': '5,1,10',
            'NativeRight': '5,1', 'NativeLeft': '5,1',
            'RightShift': '5,1', 'LeftShift': '5,1'}[name]


def negative_calls(module, name):
    if module.endswith('Arithmetic'):
        return {'Reduce': ['5,3,8,0,0', '5,3,-1,0,256', '5,3,256,0,256', '5,3,7,0,256'],
                'DoubleMod': ['5,0'],
                'RightArithmetic': ['6,3,3,0', '6,2,2,1', '6,3,2,1'],
                'RightZeroArithmetic': ['5,5,1', '5,4,0'],
                'LeftArithmetic': ['5,5,10,0,256', '5,5,10,1,0', '5,4,8,1,256', '5,5,11,1,256'],
                'ZeroArithmetic': ['5,5,1,256', '5,5,0,0', '5,4,0,256', '-1,-1,0,256', '256,256,0,256']}[name]
    if 'Count' in module:
        c = int(re.search(r'(\d+)$', module)[1])
        if name.startswith('Next'):
            return [str(2**c-1)]
        if name.startswith('RoundTrip'):
            return ['-1', str(2**c)]
        if name.startswith('Previous'):
            return ['0']
        return []
    w = int(re.search(r'(\d+)$', module)[1])
    return {'PlusWitness': ['5,3,7'],
            'RightStep': ['5,0', f'5,{w+1}'], 'LeftStep': ['5,0', f'5,{w+1}'],
            'RightWitness': [f'5,{w+1},0', '5,1,3'],
            'LeftWitness': [f'5,{w+1},0', '5,1,9'],
            'NativeRight': [f'5,{w+1}', '5,-1'], 'NativeLeft': [f'5,{w+1}', '5,-1'],
            'RightShift': [f'5,{w+1}', '5,-1'], 'LeftShift': [f'5,{w+1}', '5,-1']}.get(name, [])


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"',
               'import R = Std.Arithmetic.IntegerBitwise',
               'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation']
    lines, false, negative = prelude.copy(), prelude.copy(), prelude.copy()
    for w in (8, 16, 32, 64):
        for target in (lines, false, negative):
            target += [f'import B{w} = Std.Arithmetic.IntegerBitwiseNative{w}',
                       f'import S{w} = Std.Arithmetic.IntegerBitwiseNativeShifts{w}']
        lines += [f'newtype {{:bitWidth {w}}} U{w} = a:nat | a<{2**w}']
        for name, op, ref in [('LeftShift', '<<', 'shlNat'), ('RightShift', '>>', 'shrNat')]:
            foundation = 'LeftShift' if op == '<<' else 'RightShift'
            lines += [f'lemma {{:induction false}} {{:isolate_assertions}} {name}{w}(x:bv{w},k:nat)',
                      f' requires k<={w}', f' ensures (x{op}k) as int==R.{ref}({w},x as int,k)',
                      '{', f' S{w}.{name}(x,k); F.{foundation}({w},x as int,k);', '}']
            lines += [f'lemma {{:induction false}} {{:isolate_assertions}} Composed{name}{w}(a:U{w},k:nat)',
                      f' requires k<={w}',
                      '{',
                      f' B{w}.Power(); B{w}.RoundTrip(a as int);',
                      f' var x:=a as bv{w}; assert x as int==a as int;',
                      f' S{w}.{name}(x,k);',
                      f' ghost var observed:=(x{op}k) as int;',
                      f' assert observed==IntegerBitwiseModel.{ref}({w},a as int,k);',
                      f' assert 0<=IntegerBitwiseModel.{ref}({w},a as int,k)<{2**w} by {{',
                      f'   F.{foundation}({w},a as int,k); R.ShiftViews({w},a as int,k);', ' }',
                      f' ghost var q:U{w}:=a{op}k;',
                      f' assert q as int==IntegerBitwiseModel.{ref}({w},a as int,k);',
                      f' assert q as int==observed;', '}']
            # Counts zero and the width endpoint, including all-ones input.
            for k in (0, w):
                lines += [f'lemma {{:induction false}} Endpoint{name}{w}_{k}() {{',
                          f' var x:bv{w}:={2**w-1}; S{w}.{name}(x,{k});',
                          f' assert (x{op}{k}) as int=={2**w-1 if k==0 else 0};', '}']
            false += [f'lemma {{:induction false}} Wrong{name}{w}() {{',
                      f' S{w}.{name}(5,1);',
                      f' assert ((5 as bv{w}){op}1) as int!=IntegerBitwiseModel.{ref}({w},5,1); // ERROR', '}']
        lines += [f'lemma {{:induction false}} Plus{w}(x:bv{w},y:bv{w})',
                  f' ensures (x+y) as int==(x as int+y as int)%{2**w}',
                  f'{{ S{w}.Plus(x,y); }}',
                  f'lemma {{:induction false}} {{:isolate_assertions}} ComposedPlus{w}(a:nat,b:nat)',
                  f' requires a<{2**w} && b<{2**w}',
                  f' ensures ((a as bv{w})+(b as bv{w})) as int==(a+b)%{2**w}', '{',
                  f' B{w}.RoundTrip(a); B{w}.RoundTrip(b); S{w}.Plus(a as bv{w},b as bv{w});', '}',
                  f'lemma PlusBoundaries{w}() {{ S{w}.Plus({2**w-1},1); S{w}.Plus(0,0);',
                  f' assert (({2**w-1} as bv{w})+1) as int==0; }}']
        false += [f'lemma WrongPlus{w}() {{ S{w}.Plus({2**w-1},1);',
                  f' assert (({2**w-1} as bv{w})+1) as int=={2**w}; // ERROR', '}']
    for i, (module, name) in enumerate(declarations(LIBRARY.read_text())):
        for target in (false, negative):
            target += [f'import T{i} = Std.Arithmetic.{module}']
        false += [f'lemma {{:induction false}} False{i}_{name}() {{',
                  f' R.PowerZero(); T{i}.{name}({calls(module,name)});',
                  ' assert false; // ERROR', '}']
        for j, arguments in enumerate(negative_calls(module, name)):
            negative += [f'lemma {{:induction false}} Negative{i}_{j}() {{',
                         f' R.PowerZero(); T{i}.{name}({arguments}); // ERROR', '}']
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

    def verify(name, path, extra=(), expected_lines=(), mutation=False, guard=False, limit=200000):
        log = out / (name + '.csv')
        result = subprocess.run(exe + ['verify', str(path), *common, '--resource-limit', str(limit),
                                     '--log-format', 'csv;LogFileName=' + str(log), *extra],
                                capture_output=True, text=True, timeout=1200)
        text = result.stdout + result.stderr
        (out / (name + '.log')).write_text(text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name, text)
        if guard:
            assert result.returncode in (0, 4), (name, result.returncode, text)
            actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
            assert actual < set(expected_lines), (name, actual, expected_lines, text)
        elif mutation:
            assert result.returncode == 4 and 'could not be proved' in text, (name, result.returncode, text)
        else:
            assert result.returncode == (4 if expected_lines else 0), (name, result.returncode, text)
            actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
            assert actual == set(expected_lines), (name, actual, expected_lines, text)
        with log.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        record = dict(name=name, exit=result.returncode, scopes=len(rows),
                      failed=sum(r['TestResult.Outcome'] == 'Failed' for r in rows))
        if guard:
            record['lost_rejections'] = sorted(set(expected_lines) - actual)
        records.append(record)

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
        original = LIBRARY.read_text()
        headers = list(re.finditer(r'^[ \t]*lemma (?:\{:[^}]+\}\s*)*(\w+)\(', original, re.M))
        rules = declarations(original)
        assert len(headers) == len(rules)
        selected = 0
        path = out / 'mutation.dfy'
        for i, header in enumerate(headers):
            module, name = rules[i]
            end = headers[i+1].start() if i+1 < len(headers) else len(original)
            section = original[header.start():end]
            for j, contract in enumerate(re.finditer(r'^[ \t]+(ensures|requires) (.+)$', section, re.M)):
                site = f'{module}.{name}.{contract[1]}.{j}'
                if args.mutation_filter and args.mutation_filter not in site:
                    continue
                selected += 1
                start, finish = header.start()+contract.start(), header.start()+contract.end()
                replacement = '\n    ensures !(' + contract[2] + ')' if contract[1] == 'ensures' else '\n'
                mutated = original[:start] + replacement + original[finish:]
                # Broken recursive facts must be rejected on inhabited base inputs,
                # without an inconsistent induction hypothesis at positive inputs.
                if contract[1] == 'ensures':
                    signature = original[header.start():original.index('\n', header.start())]
                    params = re.search(r'\((.*)\)', signature)[1]
                    names = [p.split(':')[0].strip() for p in params.split(',')]
                    values = calls(module, name).split(',')
                    if name.startswith('RoundTrip'):
                        values = ['0']
                    elif name in ('RightWitness', 'LeftWitness'):
                        values = ['0', '0', '0']
                    assert len(names) == len(values), (site, names, values)
                    at = mutated.index('\n', header.start())
                    base = ' && '.join(n + '==' + v for n, v in zip(names, values))
                    mutated = mutated[:at] + '\n    requires ' + base + mutated[at:]
                mutated = mutated.replace('include "IntegerBitwiseNative.dfy"',
                                          'include "' + (LIBRARY.parent / 'IntegerBitwiseNative.dfy').as_posix() + '"')
                path.write_text(mutated)
                if contract[1] == 'requires':
                    negative = fixtures()[2]
                    negative[0] = f'include "{path.as_posix()}"'
                    caller = out / 'mutation-callers.dfy'
                    caller.write_text('\n'.join(negative) + '\n')
                    errors = {k for k, line in enumerate(negative, 1) if '// ERROR' in line}
                    verify('mutation-' + site, caller, ['--type-system-refresh:true'], errors, guard=True)
                    caller.unlink()
                else:
                    verify('mutation-' + site, path,
                           ['--type-system-refresh:true', '--filter-symbol', f'{module}.{name}'],
                           mutation=True, limit=2000000)
        path.unlink(missing_ok=True)
        assert selected, 'No mutation selected'
    assert records, 'No verification or mutation selected'
    (out / 'summary.json').write_text(json.dumps(records, indent=2) + '\n')
    print('Native shift checks passed')


if __name__ == '__main__':
    main()
