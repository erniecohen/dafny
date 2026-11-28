#!/usr/bin/env python3
"""Qualify bounded C++ integer-newtype operations with arithmetic oracles."""
import argparse
import csv
import importlib.util
import json
from pathlib import Path
import re
import shutil
import sys

spec = importlib.util.spec_from_file_location('shared', Path(__file__).with_name('issue193-runtime-python.py'))
shared = importlib.util.module_from_spec(spec)
spec.loader.exec_module(shared)
WIDTHS = (1, 4, 5, 8, 12, 16, 31, 32, 63, 64)


def program(widths):
    lines = shared.boolean.definitions(widths)
    lines[0] = f'include "{shared.population.FOUNDATION.as_posix()}"'
    lines.insert(3, 'newtype {:nativeType "byte"} Count=x:nat | x<128')
    lines = [s.replace('k:nat', 'k:Count').replace(',k);', ',k as int);')
             .replace('as int,k)', 'as int,k as int)')
             .replace('{:nativeType false} U1 ', '{:nativeType "sbyte"} U1 ') for s in lines]
    rotation = shared.rotation.definitions(widths)
    start = next(i for i, s in enumerate(rotation) if s.startswith('newtype'))
    for s in rotation[start:]:
        if s.startswith('newtype {:bitWidth') and ' U' in s:
            continue
        lines.append(s.replace('k:int', 'k:Count').replace('i:int', 'i:Count')
                     .replace('as int,k)', 'as int,k as int)').replace('as int,i)', 'as int,i as int)')
                     .replace('),i)', '),i as int)').replace('.RotateLeft(k)', '.RotateLeft(k as int)')
                     .replace('.RotateRight(k)', '.RotateRight(k as int)').replace('.bit(i)', '.bit(i as int)')
                     .replace('} Even=', '} {:nativeType "byte"} Even=').replace(' witness 2', ' witness *'))
    lines.append('import C = Std.Arithmetic.IntegerBitwisePopulation')
    for n in widths:
        lines += [f'function Population{n}(a:U{n}):Count ensures Population{n}(a)<={n}',
                  f'{{ C.View({n},a as int); a.PopCount() as Count }}']
    lines += ['function Identity(a:U64):U64 { a }', 'function Counter(k:Count):Count { k }',
              'function Once(a:U64,k:Count):U64 requires k<=64',
              '{ P64(); F.LeftShift(64,a as int,k as int); R.ShiftViews(64,a as int,k as int); Identity(a) << Counter(k) }',
              'newtype {:bitWidth 12} {:nativeType "byte"} Narrow=x:nat | x<256',
              'function NarrowLeft(a:Narrow,k:Count):Narrow requires k<=12',
              ' requires 0<=IntegerBitwiseModel.shlNat(12,a as int,k as int)<256 { a << k }',
              'function NarrowRotate(a:Narrow,k:Count):Narrow requires k<=12',
              ' requires 0<=IntegerBitwiseModel.rolNat(12,a as int,k as int)<256 { a.RotateLeft(k as int) }',
              'function NarrowPopulation(a:Narrow):Count { C.View(12,a as int); a.PopCount() as Count }',
              'function Literal(a:U64):U64 { P64(); F.LeftShift(64,a as int,64); R.ShiftViews(64,a as int,64); a << (32 + 32) }',
              'method Main() { print Once(1,64),":",Bit64(1,0),":";',
              ' C.View(64,3); print (3 as U64).PopCount(),":",Population64(3) as U64,"\\n"; }']
    return lines


def cases(widths):
    for n in widths:
        values = sorted(set(shared.boolean.values(n) + shared.rotation.values(n) + shared.population.values(n)))
        for a in values:
            for j, b in enumerate(values):
                yield n, a, b, j % (n + 1)
            for k in range(n + 1):
                yield n, a, 0, k


def harness(source, widths):
    # Renaming the generated main removes C++'s implicit return 0; it is never called.
    lines = ['#pragma GCC diagnostic push', '#pragma GCC diagnostic ignored "-Wreturn-type"',
             '#define main dafny_generated_main', f'#include "{source.name}"', '#undef main',
             '#pragma GCC diagnostic pop',
             'int main() {', 'using D = _module::__default;',
             'DafnySet<int> a{1,2}, b{3}, c{2};',
             'if (!a.disjoint(b) || a.disjoint(c) || !b.disjoint(a)) return 10;',
             'if (D::NarrowLeft(1,4)!=16 || D::NarrowRotate(16,8)!=1 || D::NarrowPopulation(255)!=8) return 11;',
             'if (D::Literal(~uint64(0))!=0 || !D::EvenBit(2,1) || D::EvenBit(2,0)) return 12;',
             'unsigned n; uint64 a0,b0,k; while (std::cin >> n >> a0 >> b0 >> k) {',
             'std::cout << "CASE:" << n << ":" << a0 << ":" << b0 << ":" << k << ":";', 'switch(n) {']
    for n in widths:
        operations = [f'D::{op}{n}(a0' + (')' if op == 'Not' else ',k)' if 'Shift' in op else ',b0)')
                      for op, _, _ in shared.boolean.OPS]
        operations += [f'D::RotateLeft{n}(a0,k)', f'D::RotateRight{n}(a0,k)',
                       f'D::Bit{n}(a0,k%{n})', f'D::Population{n}(a0)']
        lines += [f'case {n}: std::cout << ' + ' << ":" << '.join(f'uint64({op})' for op in operations) + '; break;']
    return '\n'.join(lines + ['default: return 13; } std::cout << "\\n"; } }']) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', required=True, type=Path)
    parser.add_argument('--z3', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--resolver', choices=['both', 'legacy', 'refreshed'], default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    parser.add_argument('--quick', action='store_true')
    parser.add_argument('--focus', choices=['all', 'runtime', 'controls'], default='all')
    parser.add_argument('--sanitize', action='store_true')
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    exe = ['dotnet', str(args.dafny.resolve())] if args.dafny.suffix == '.dll' else [str(args.dafny.resolve())]
    checks, scopes = [], []

    def run(label, command, expected=0):
        rc, text, seconds = shared.boolean.invoke(command, 1200)
        (out / (label + '.log')).write_text(text)
        assert rc == expected, (label, rc, 'see retained log')
        assert not re.search(r'out of resource|timed out|inconclusive|Warning:|internal error|Unhandled exception|runtime error:', text, re.I), label
        checks.append(dict(name=label, seconds=seconds))
        return text

    modes = ['legacy', 'refreshed'] if args.resolver == 'both' else [args.resolver]
    for mode in modes:
        common = ['--integer-bitwise', '--standard-libraries:false', '--cores', '2', '--show-snippets:false',
                  '--allow-warnings:false', '--type-system-refresh:' + str(mode == 'refreshed').lower(),
                  '--additional-axioms:' + str(args.additional_axioms).lower(), '--solver-path', str(args.z3.resolve()),
                  '--resource-limit', '200000', '--verification-time-limit', '60',
                  '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']
        if args.focus in ('all', 'runtime'):
            widths = (4, 12, 64) if args.quick else WIDTHS
            path = out / (mode + '.dfy')
            path.write_text('\n'.join(program(widths)) + '\n')
            csvpath = out / (mode + '.csv')
            run(mode + '-verify', exe + ['verify', str(path), *common, '--log-format', 'csv;LogFileName=' + str(csvpath)])
            with csvpath.open(encoding='utf-8-sig') as stream:
                rows = list(csv.DictReader(stream))
            assert rows and all(r['TestResult.Outcome'] == 'Passed' for r in rows)
            scopes.extend(dict(check=mode, **r) for r in rows)
            output = out / (mode + '-runtime')
            text = run(mode + '-run', exe + ['run', str(path), *common, '--no-verify', '--target', 'cpp',
                       '--unicode-char:false', '--output', str(output), '--spill-translation'])
            assert '0:true:2:2' in text.splitlines()
            source = output.with_suffix('.cpp')
            generated = source.read_text()
            body = re.search(r'uint64 __default::Once\([^)]*\)\s*\{(.*?)\n  }', generated, re.S).group(1)
            assert body.count('::Identity(') == body.count('::Counter(') == 1
            assert body.index('::Identity(') < body.index('::Counter(') < body.index('if (')
            assert '[&]() -> uint64' in body and body.count('const uint64') == 2
            assert not re.search(r'IntegerBitwiseModel::(andNat|orNat|xorNat|notNat|shlNat|shrNat|rolNat|rorNat|popNat)\(', generated)
            runner = out / (mode + '-oracle.cpp')
            runner.write_text(harness(source, widths))
            native = out / (mode + '-oracle')
            compiler = shutil.which('g++')
            assert compiler
            flags = ['-fsanitize=undefined', '-fno-sanitize-recover=undefined'] if args.sanitize else []
            run(mode + '-compile-oracle', [compiler, '-std=c++17', *flags, '-I', str(out), str(runner), '-o', str(native)])
            # The native consumer executes the generated functions, not a second implementation.
            data = out / (mode + '-input.txt')
            expected = list(cases(widths))
            data.write_text(''.join(' '.join(map(str, row)) + '\n' for row in expected))
            # Feed a file without shell parsing; invoke's process-group timeout still owns the child.
            driver = 'import subprocess,sys; f=open(sys.argv[2]); p=subprocess.run([sys.argv[1]],stdin=f); sys.exit(p.returncode)'
            text = run(mode + '-oracle', [sys.executable, '-c', driver, str(native), str(data)])
            actual = [list(map(int, line[5:].split(':'))) for line in text.splitlines() if line.startswith('CASE:')]
            assert [tuple(row[:4]) for row in actual] == expected
            for n, a, b, k, *results in actual:
                r = k % n
                mathematical = (*shared.boolean.oracle(n, a, b, k),
                                (a * 2**r) % 2**n + a // 2**(n-r),
                                a // 2**r + (a % 2**r) * 2**(n-r),
                                (a // 2**r) % 2, sum((a // 2**i) % 2 for i in range(n)))
                assert tuple(results) == mathematical, (n, a, b, k, results, mathematical)
            checks.append(dict(name=mode + '-arithmetic', cases=len(actual), comparisons=10*len(actual)))
        if args.focus in ('all', 'controls'):
            source = out / (mode + '-unsupported.dfy')
            declaration = 'newtype {:bitWidth 8} {:nativeType "byte"} W=x:nat | x<256\n'
            unsupported = [
                ('width', 'newtype {:bitWidth 65} {:nativeType "byte"} W=x:nat | x<256\nfunction F(a:W,b:W):W { a & b }', 'between 1 and 64'),
                ('nonnative', declaration.replace('"byte"', 'false') + 'function F(a:W,b:W):W { a & b }', 'require a native integer newtype'),
                ('count', declaration + 'function F(a:W,k:nat):W requires k<=8 { a << k }', 'counts require a native integer type'),
                ('population-storage', declaration + 'function F(a:W):nat { a.PopCount() }', 'Unbounded integers')]
            for label, text, diagnostic in unsupported:
                source.write_text(text + '\n')
                text = run(mode + '-reject-' + label, exe + ['build', str(source), *common, '--no-verify',
                           '--target', 'cpp', '--unicode-char:false', '--output', str(out / (mode + '-rejected'))], 3)
                assert diagnostic in text and 'Error while compiling C++ files' not in text
    if args.focus in ('all', 'controls'):
        command = [sys.executable, str(Path(__file__).with_name('issue193-runtime-python.py')),
                   '--dafny', str(args.dafny.resolve()), '--z3', str(args.z3.resolve()), '--output', str(out / 'proof-controls'),
                   '--resolver', args.resolver, '--focus', 'controls']
        if args.additional_axioms:
            command.append('--additional-axioms')
        run('proof-controls', command)
        proof = json.loads((out / 'proof-controls/summary.json').read_text())
        scopes.extend(proof['scopes'])
    (out / 'summary.json').write_text(json.dumps(dict(checks=checks, scopes=scopes), indent=2) + '\n')
    print('C++ integer-bitwise runtime checks passed')


if __name__ == '__main__':
    main()
