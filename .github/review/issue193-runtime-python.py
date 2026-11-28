#!/usr/bin/env python3
"""Qualify Python integer-newtype operations with the C# arithmetic oracles."""
import argparse
import ast
import csv
import importlib.util
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


boolean = load('issue193-runtime-csharp')
rotation = load('issue193-rotation-bit-source')
population = load('issue193-popcount-source')


def inspect_python(output, kind, text):
    generated = output.with_name(output.name + '-py')
    files = list(generated.rglob('*.py'))
    assert files
    # Syntax remains compatible with the runtime's Python >=3.8 contract.
    for file in files:
        ast.parse(file.read_text(), feature_version=(3, 8))
    bodies = [node for file in files for node in ast.walk(ast.parse(file.read_text()))
              if isinstance(node, ast.FunctionDef) and node.name in ('Once64', 'Once')]
    assert len(bodies) == 1
    calls = [node.func.attr for node in ast.walk(bodies[0])
             if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)]
    assert calls.count('Id64' if kind == 'boolean' else 'Identity') == 1
    if kind != 'population':
        assert calls.count('Count') == 1


def main(target='py', inspect=None, program=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', required=True, type=Path)
    parser.add_argument('--z3', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--resolver', choices=['both', 'legacy', 'refreshed'], default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    parser.add_argument('--quick', action='store_true')
    parser.add_argument('--focus', choices=['all', 'runtime', 'controls'], default='all')
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    exe = ['dotnet', str(args.dafny.resolve())] if args.dafny.suffix == '.dll' else [str(args.dafny.resolve())]
    modes = ['legacy', 'refreshed'] if args.resolver == 'both' else [args.resolver]
    checks, scopes = [], []

    def run(name, command, expected=0):
        rc, text, elapsed = boolean.invoke(exe + command, 1200)
        (out / (name + '.log')).write_text(text)
        assert rc == expected, (name, rc, 'see retained log')
        assert not re.search(r'out of resource|timed out|inconclusive|Warning:|internal error|Unhandled exception', text, re.I), name
        checks.append(dict(name=name, seconds=elapsed))
        return text

    for mode in modes:
        common = ['--integer-bitwise', '--standard-libraries:false', '--solver-path', str(args.z3.resolve()),
                  '--cores', '2', '--show-snippets:false', '--allow-warnings:false', '--error-limit', '0',
                  '--type-system-refresh:' + str(mode == 'refreshed').lower(),
                  '--additional-axioms:' + str(args.additional_axioms).lower(),
                  '--resource-limit', '200000', '--verification-time-limit', '60',
                  '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']

        def verify(label, lines, negative=False):
            name = mode + '-' + label
            path = out / (name + '.dfy')
            path.write_text('\n'.join(lines) + '\n')
            csvpath = out / (name + '.csv')
            text = run(name, ['verify', str(path), *common, '--log-format', 'csv;LogFileName=' + str(csvpath)], 4 if negative else 0)
            expected = {i for i, line in enumerate(lines, 1) if '// ERROR' in line}
            actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
            assert actual == expected, (name, actual, expected)
            with csvpath.open(encoding='utf-8-sig') as stream:
                rows = list(csv.DictReader(stream))
            assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), name
            scopes.extend(dict(check=name, **r) for r in rows)
            return path

        if args.focus in ('all', 'runtime'):
            for kind, module in [('boolean', boolean), ('rotation', rotation), ('population', population)]:
                widths = (4, 12, 64, 80) if args.quick else module.WIDTHS
                path = verify(kind, program(kind, module, widths) if program else module.program(widths))
                output = out / (mode + '-' + kind + '-runtime')
                text = run(mode + '-' + kind + '-execute', ['run', str(path), *common, '--no-verify', '--target', target, '--output', str(output), *(['--spill-translation'] if target != 'py' else [])])
                rows = [line[5:].split(':') for line in text.splitlines() if line.startswith('CASE:')]
                if kind == 'boolean':
                    keys = [(n, a, b, j % (n + 1)) for n in widths for a in module.values(n) for j, b in enumerate(module.values(n))]
                    assert [tuple(map(int, row[:4])) for row in rows] == keys
                    for row in rows:
                        n, a, b, k, *results = map(int, row)
                        assert results == module.oracle(n, a, b, k), (n, a, b, k, results)
                    fixed = ['ONCE:0', 'NARROW:0:0', 'OUT:0:64', 'COUNTS:0:0']
                    comparisons = 6 * len(rows)
                elif kind == 'rotation':
                    keys = [(n, a, k) for n in widths for a in module.values(n) for k in range(n + 1)]
                    assert [tuple(map(int, row[:3])) for row in rows] == keys
                    for n, a, k, left, right, bit in rows:
                        n, a, k = int(n), int(a), int(k)
                        r = k % n
                        expected = ((a * 2**r) % 2**n + a // 2**(n-r),
                                    a // 2**r + (a % 2**r) * 2**(n-r), (a // 2**r) % 2 == 1)
                        assert (int(left), int(right), bit == 'true') == expected, (n, a, k)
                    fixed = ['NARROW:12:1', 'NARROW:65:1', 'NARROW:80:1', 'ONCE:1', 'OUT:1:64']
                    comparisons = 3 * len(rows)
                else:
                    keys = [(n, a) for n in widths for a in module.values(n)]
                    assert [tuple(map(int, row[:2])) for row in rows] == keys
                    for n, a, count in rows:
                        n, a = int(n), int(a)
                        assert int(count) == sum((a // 2**i) % 2 for i in range(n)), (n, a, count)
                    fixed = ['NARROW:' + str(n) + ':8' for n in (12, 65, 80, 129)]
                    fixed += ['STRICT:1', 'SHADOW:2', 'ONCE:1', 'OUT:3:2']
                    comparisons = len(rows)
                assert all(line in text.splitlines() for line in fixed), kind
                (inspect or inspect_python)(output, kind, text)
                checks.append(dict(name=mode + '-' + kind + '-oracle', cases=len(rows), comparisons=comparisons))
        if args.focus in ('all', 'controls'):
            # The exact false/wrong-result and invalid-count checks used by C#.
            for label, module in [('rotation', rotation), ('population', population)]:
                verify(label + '-negative', module.controls(), True)
            lines = boolean.definitions((8,))
            for lemma, op, raw in boolean.OPS:
                expression = lemma + '8(5' + ('' if op == '!' else ',0' if 'Shift' in lemma else ',3') + ')'
                lines += ['lemma False' + lemma + '() { var r:U8 := ' + expression + '; assert false; // ERROR', '}']
                lines += ['lemma Wrong' + lemma + '() { var r:U8 := ' + expression + '; assert r as int == ' + ('255' if op == '&' else '0') + '; // ERROR', '}']
            for count in (-1, 9):
                for lemma in ('LeftShift', 'RightShift'):
                    lines += [f'lemma Bad{lemma}{abs(count)}() {{ var r:={lemma}8(1,{count}); // ERROR', '}']
            lines += ['newtype {:bitWidth 8} NZ=x:nat | 0<x<256 witness 1',
                      'function Strict(a:NZ):NZ {',
                      ' IntegerBitwiseModel.unfoldShl(8,a as int,8); a << 8 // ERROR', '}']
            verify('boolean-negative', lines, True)
    (out / 'summary.json').write_text(json.dumps(dict(checks=checks, scopes=scopes), indent=2) + '\n')
    print(('Python' if target == 'py' else target.capitalize()) + ' integer-bitwise runtime checks passed')


if __name__ == '__main__':
    main()
