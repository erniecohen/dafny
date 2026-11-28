#!/usr/bin/env python3
"""Qualify compiled integer-newtype operators against an independent digit oracle."""
import argparse
import csv
import json
import os
from pathlib import Path
import random
import re
import signal
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
FOUNDATION = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseCompilerFoundation.dfy'
WIDTHS = (4, 5, 8, 12, 16, 31, 32, 63, 64, 80, 128)
OPS = [('And', '&', 'andNat'), ('Or', '|', 'orNat'), ('Xor', '^', 'xorNat'),
       ('Not', '!', 'notNat'), ('LeftShift', '<<', 'shlNat'), ('RightShift', '>>', 'shrNat')]


def definitions(widths=WIDTHS):
    lines = [f'include "{FOUNDATION.as_posix()}"',
             'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation',
             'import R = Std.Arithmetic.IntegerBitwise']
    for j in range(max(widths) + 1):
        lines += [f'lemma P{j}() ensures R.P({j}) == {2**j}',
                  f' ensures IntegerBitwiseModel.P({j}) == {2**j} {{']
        if j:
            lines += [f' P{j-1}();']
        lines += [f' F.Power({j}); IntegerBitwiseModel.unfoldP({j});', '}']
    for n in widths:
        carrier = {4: '"byte"', 5: '"sbyte"', 8: '"byte"', 12: '"ushort"', 16: '"ushort"',
                   31: '"int"', 32: '"uint"', 63: '"long"', 64: '"ulong"'}.get(n, 'false')
        lines += [f'newtype {{:bitWidth {n}}} {{:nativeType {carrier}}} U{n} = x:nat | x < {2**n}']
        for lemma, op, raw in OPS:
            shift = 'Shift' in lemma
            params = f'a:U{n}' + ('' if op == '!' else ', k:nat' if shift else f', b:U{n}')
            args = f'{n},a as int' + ('' if op == '!' else ',k' if shift else ',b as int')
            expression = '!a' if op == '!' else f'a {op} ' + ('k' if shift else 'b')
            lines += [f'function {lemma}{n}({params}):U{n}']
            if shift:
                lines += [f' requires k <= {n}']
            lines += [f' ensures {lemma}{n}(a' + ('' if op == '!' else ',k' if shift else ',b') +
                      f') as int == IntegerBitwiseModel.{raw}({args})', '{',
                      f' P{n}(); F.{lemma}({args});']
            if op == '&':
                lines += [f' R.AndRange({n},a as int,b as int);']
            elif shift:
                lines += [f' R.ShiftViews({n},a as int,k);']
            else:
                lines += [f' R.BooleanRange({n},a as int,a as int);' if op == '!' else
                          f' R.BooleanRange({n},a as int,b as int);']
            lines += [f' {expression}', '}']
    return lines


def values(n):
    if n <= 8:
        return list(range(2**n))
    rng = random.Random(n)
    result = {0, 1, 2, 3, 2**n - 1, 2**(n-1), 2**(n-1)-1}
    result.update(1 << j for j in range(n))
    result.update((2**n-1) ^ (1 << j) for j in range(n))
    result.update(rng.getrandbits(n) for _ in range(16))
    return sorted(result)


def program(widths):
    lines = definitions(widths)
    for n in widths:
        lines += [f'method Test{n}(xs:seq<int>) {{', ' for i:=0 to |xs| {',
                  '  for j:=0 to |xs| {', f'   var k:=j % {n+1};', f'   if 0 <= xs[i] < {2**n} && 0 <= xs[j] < {2**n} {{',
                  f'   var a:=xs[i] as U{n}; var b:=xs[j] as U{n};',
                  f'   print "CASE:{n}:",a,":",b,":",k,":",And{n}(a,b),":",Or{n}(a,b),":",Xor{n}(a,b),":",Not{n}(a),":",LeftShift{n}(a,k),":",RightShift{n}(a,k),"\\n";',
                  '   }', '  }', ' }', '}']
    lines += [
        'function Id64(a:U64):U64 { a }',
        'function Count(k:nat):nat { k }',
        'function Once64(a:U64,k:nat):U64 requires k<=64 {',
        ' P64(); F.LeftShift(64,a as int,k); R.ShiftViews(64,a as int,k);',
        ' Id64(a) << Count(k) }',
        'newtype {:bitWidth 12} {:nativeType "byte"} Narrow=x:nat | x<256',
        'function NarrowLeft(a:Narrow,k:nat):Narrow requires k<=12',
        ' requires 0<=IntegerBitwiseModel.shlNat(12,a as int,k)<256 { a << k }',
        'function NarrowRight(a:Narrow,k:nat):Narrow requires k<=12',
        ' requires 0<=IntegerBitwiseModel.shrNat(12,a as int,k)<256 { a >> k }',
        'function BVCount64(a:U64,k:bv8):U64 requires 0<=k as int<=64 {',
        ' P64(); F.LeftShift(64,a as int,k as int); R.ShiftViews(64,a as int,k as int); a << k }',
        'function LargeCount64(a:U64,k:bv128):U64 requires 0<=k as int<=64 {',
        ' P64(); F.RightShift(64,a as int,k as int); R.ShiftViews(64,a as int,k as int); a >> k }',
        'method OutOnce64(a:U64,b:U64,k:nat) returns (r:U64,s:nat) requires k<=64 {',
        ' r:=a; s:=k; P64(); F.And(64,b as int,r as int); R.AndRange(64,b as int,r as int);',
        ' r:=b & r; F.LeftShift(64,r as int,s); R.ShiftViews(64,r as int,s); r:=r << s; }',
        'method Main() {',
        ' print "COUNTS:",BVCount64(1,64),":",LargeCount64(1,64),"\\n";',
        ' var r,s:=OutOnce64(5,3,64); print "OUT:",r,":",s,"\\n";',
        ' print "ONCE:",Once64(1,64),"\\n";',
        ' P12(); P8(); IntegerBitwiseModel.unfoldNorm(12,16);',
        ' IntegerBitwiseModel.unfoldShl(12,16,8); IntegerBitwiseModel.unfoldShr(12,16,8);',
        ' print "NARROW:",NarrowLeft(16,8),":",NarrowRight(16,8),"\\n";']
    for n in widths:
        lines += [f' var xs{n}:seq<int>:=[' + ','.join(map(str, values(n))) + '];', f' Test{n}(xs{n});']
    lines += ['}']
    return lines


def oracle(n, a, b, k):
    # Independent arithmetic digit model, without native bitwise operators.
    aa = [(a // 2**i) % 2 for i in range(n)]
    bb = [(b // 2**i) % 2 for i in range(n)]
    return [sum(x*y*2**i for i, (x,y) in enumerate(zip(aa,bb))),
            sum(max(x,y)*2**i for i, (x,y) in enumerate(zip(aa,bb))),
            sum(((x+y) % 2)*2**i for i, (x,y) in enumerate(zip(aa,bb))),
            2**n-1-a, (a*2**k) % 2**n, a // 2**k]


def invoke(command, timeout):
    start = time.monotonic()
    child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                             text=True, start_new_session=True)
    def stop():
        for sig in (signal.SIGTERM, signal.SIGKILL):
            try:
                os.killpg(child.pid, sig)
            except ProcessLookupError:
                break
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                continue
        # Kill this group's remaining descendants even if its leader exited first.
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    def interrupted(signum, frame):
        stop()
        raise RuntimeError(f'interrupted by signal {signum}')
    previous = {sig: signal.signal(sig, interrupted) for sig in (signal.SIGTERM, signal.SIGINT)}
    try:
        try:
            stdout, stderr = child.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            stop()
            stdout, stderr = child.communicate()
            raise AssertionError('qualification timed out')
    finally:
        for sig, handler in previous.items():
            signal.signal(sig, handler)
    return child.returncode, stdout + stderr, time.monotonic()-start


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', required=True, type=Path)
    parser.add_argument('--z3', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--resolver', choices=['both', 'legacy', 'refreshed'], default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    parser.add_argument('--quick', action='store_true')
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    exe = ['dotnet', str(args.dafny.resolve())] if args.dafny.suffix == '.dll' else [str(args.dafny.resolve())]
    widths = (4, 64, 80) if args.quick else WIDTHS
    modes = ['legacy', 'refreshed'] if args.resolver == 'both' else [args.resolver]
    checks = []
    scopes = []

    def run(name, command, expected=0):
        code, text, elapsed = invoke(exe + command, 1200)
        (out/(name+'.log')).write_text(text)
        assert code == expected, (name, code, 'see retained log')
        assert not re.search(r'out of resource|timed out|inconclusive|Warning:|internal error|Unhandled exception', text, re.I), name
        checks.append(dict(name=name, seconds=elapsed))
        return text

    def verify(name, lines, common, errors=()):
        path = out/(name+'.dfy')
        path.write_text('\n'.join(lines)+'\n')
        csvpath = out/(name+'.csv')
        text = run(name, ['verify', str(path), *common, '--log-format', 'csv;LogFileName='+str(csvpath)], 4 if errors else 0)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
        assert actual == set(errors), (name, actual, errors)
        with csvpath.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(row['TestResult.Outcome'] in ('Passed','Failed') for row in rows), name
        scopes.extend(dict(check=name, **row) for row in rows)
        return path

    for mode in modes:
        common = ['--integer-bitwise', '--standard-libraries:false', '--solver-path', str(args.z3.resolve()),
                  '--cores', '2', '--show-snippets:false', '--allow-warnings:false', '--error-limit','0',
                  '--type-system-refresh:'+str(mode=='refreshed').lower(),
                  '--additional-axioms:'+str(args.additional_axioms).lower(),
                  '--resource-limit','200000','--verification-time-limit','60',
                  '--boogie','/normalizeDeclarationOrder:0','--boogie','/restartProver']
        path = verify(mode+'-program', program(widths), common)
        text = run(mode+'-runtime', ['run', str(path), *common, '--no-verify', '--target','cs', '--output',str(out/(mode+'-runtime'))])
        rows = [list(map(int,line[5:].split(':'))) for line in text.splitlines() if line.startswith('CASE:')]
        expected_count = sum(len(values(n))**2 for n in widths)
        assert len(rows) == expected_count, (mode, len(rows), expected_count)
        expected_keys = [(n,a,b,j % (n+1)) for n in widths for a in values(n) for j,b in enumerate(values(n))]
        assert [tuple(row[:4]) for row in rows] == expected_keys, (mode,'missing or reordered runtime cases')
        for n,a,b,k,*results in rows:
            assert results == oracle(n,a,b,k), (mode,n,a,b,k,results)
        generated = (out/(mode+'-runtime.cs')).read_text()
        generated = re.sub(r'\[assembly: DafnyAssembly\.DafnySourceAttribute\(@"(?:[^"]|"")*"\)\]', '', generated)
        generated = re.sub(r'/\*.*?\*/|//[^\n]*', '', generated, flags=re.S)
        assert 'IntegerBitwiseModel.' not in generated and 'System.Numerics.BitOperations' not in generated
        assert 'ulong' in generated and 'Dafny.Helpers.Let' in generated and '>= 64 ? 0 :' in generated
        assert 'ONCE:0' in text.splitlines() and 'NARROW:0:0' in text.splitlines() and 'OUT:0:64' in text.splitlines() and 'COUNTS:0:0' in text.splitlines()
        body = re.search(r'public static ulong Once64\([^}]+}', generated).group()
        assert body.count('__default.Id64(') == 1 and body.count('__default.Count(') == 1
        native_body = re.search(r'public static byte And4\([^}]+}', generated).group()
        assert 'BigInteger' not in native_body and 'ulong' in native_body
        checks.append(dict(name=mode+'-oracle', cases=len(rows), comparisons=6*len(rows)))
        negative = definitions((8,))
        for lemma, op, raw in OPS:
            expression = f'{lemma}8(5' + ('' if op=='!' else ',0' if 'Shift' in lemma else ',3') + ')'
            negative += [f'lemma False{lemma}() {{ var r:U8 := {expression}; assert false; // ERROR', '}']
            negative += [f'lemma Wrong{lemma}() {{ var r:U8 := {expression}; assert r as int == {0 if op != "&" else 255}; // ERROR', '}']
        for count in (-1,9):
            for lemma in ('LeftShift','RightShift'):
                negative += [f'lemma Bad{lemma}{abs(count)}() {{ var r:= {lemma}8(1,{count}); // ERROR', '}']
        negative += ['newtype {:bitWidth 8} NZ=x:nat | 0<x<256 witness 1',
                     'function Strict(a:NZ):NZ {',
                     ' IntegerBitwiseModel.unfoldShl(8,a as int,8); a << 8 // ERROR', '}']
        errors = [i for i,line in enumerate(negative,1) if '// ERROR' in line]
        verify(mode+'-controls',negative,common,errors)
        # Source operands must still pass ordinary ghost restrictions.
        ghost = out/(mode+'-ghost.dfy')
        ghost.write_text('newtype {:bitWidth 8} U=x:nat | x<256\nfunction Bad(ghost a:U,b:U):U { a & b }\n')
        text = run(mode+'-ghost',['resolve',str(ghost),'--integer-bitwise','--type-system-refresh:'+str(mode=='refreshed').lower()],2)
        assert 'ghost variables such as a are allowed only in specification contexts' in text
        reference = out/(mode+'-reference.dfy')
        reference.write_text('function Bad(a:int):int { IntegerBitwiseModel.notNat(8,a) }\n')
        text = run(mode+'-reference',['resolve',str(reference),'--integer-bitwise','--type-system-refresh:'+str(mode=='refreshed').lower()],2)
        assert 'a call to a ghost function is allowed only in specification contexts' in text
    (out/'summary.json').write_text(json.dumps(dict(checks=checks,scopes=scopes),indent=2)+'\n')
    print('C# integer-bitwise runtime checks passed')


if __name__ == '__main__':
    main()
