#!/usr/bin/env python3
"""Qualify integer-newtype rotations and bit queries, including C# execution."""
import argparse
import csv
import importlib.util
import json
from pathlib import Path
import random
import re

ROOT = Path(__file__).resolve().parents[2]
FOUNDATION = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseCompilerFoundation.dfy'
spec = importlib.util.spec_from_file_location('runtime_gate', Path(__file__).with_name('issue193-runtime-csharp.py'))
runtime = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runtime)
WIDTHS = (1, 4, 5, 8, 12, 16, 31, 32, 63, 64, 65, 80, 128)


def definitions(widths):
    lines = [f'include "{FOUNDATION.as_posix()}"',
             'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation',
             'import R = Std.Arithmetic.IntegerBitwise']
    for n in range(max(widths)+1):
        lines += [f'lemma P{n}() ensures R.P({n}) == {2**n}',
                  f' ensures IntegerBitwiseModel.P({n}) == {2**n} {{']
        if n:
            lines += [f' P{n-1}();']
        lines += [f' F.Power({n}); IntegerBitwiseModel.unfoldP({n});', '}']
    for n in widths:
        carrier = {1:'"sbyte"',4:'"byte"',5:'"sbyte"',8:'"byte"',12:'"ushort"',16:'"ushort"',
                   31:'"int"',32:'"uint"',63:'"long"',64:'"ulong"'}.get(n,'false')
        lines += [f'newtype {{:bitWidth {n}}} {{:nativeType {carrier}}} U{n}=x:nat | x<{2**n}']
        for member, lemma, raw in [('RotateLeft','LeftRotate','rolNat'),('RotateRight','RightRotate','rorNat')]:
            lines += [f'function {member}{n}(a:U{n},k:int):U{n} requires 0<=k<={n}',
                      f' ensures {member}{n}(a,k) as int == IntegerBitwiseModel.{raw}({n},a as int,k)',
                      f' ensures {member}{n}(a,k) as int == R.{raw}({n},a as int,k)',
                      f'{{ P{n}(); F.{lemma}({n},a as int,k); R.RotationRange({n},a as int,k); a.{member}(k) }}']
        lines += [f'function Bit{n}(a:U{n},i:int):bool requires 0<=i<{n}',
                  f' ensures Bit{n}(a,i) == (IntegerBitwiseModel.shrNat({n},a as int,i)%2==1)',
                  f' ensures Bit{n}(a,i) == R.bit({n},a as int,i)',
                  f'{{ F.RightShift({n},a as int,i); R.NormalizeBits({n},a as int);',
                  f' R.BitAt({n},R.norm({n},a as int),i); reveal R.shrNat(); a.bit(i) }}']
    # Check a descriptor whose strict predicate excludes both zero and one.
    lines += ['newtype {:bitWidth 8} Even=x:nat | 0<x<256 && x%2==0 witness 2',
              'function EvenBit(a:Even,i:int):bool requires 0<=i<8',
              ' ensures EvenBit(a,i)==(IntegerBitwiseModel.shrNat(8,a as int,i)%2==1) { a.bit(i) }']
    return lines


def values(n):
    if n<=8:
        return list(range(2**n))
    rng=random.Random(n)
    return sorted({0,1,2,3,2**n-1,2**(n-1),2**(n-1)-1} |
                  {1<<i for i in range(n)} | {rng.getrandbits(n) for _ in range(8)})


def program(widths):
    lines=definitions(widths)
    for n in widths:
        lines += [f'method Test{n}(xs:seq<int>) {{ for i:=0 to |xs| {{',
                  f' if 0<=xs[i]<{2**n} {{ var a:=xs[i] as U{n}; for k:=0 to {n+1} {{',
                  f'  print "CASE:{n}:",a,":",k,":",RotateLeft{n}(a,k),":",RotateRight{n}(a,k),":",Bit{n}(a,k%{n}),"\\n";',
                  ' } } } }']
    for n in (12,65,80):
        lines += [f'newtype {{:bitWidth {n}}} {{:nativeType "byte"}} Narrow{n}=x:nat | x<256',
                  f'function NarrowRotate{n}(a:Narrow{n},k:int):Narrow{n} requires 0<=k<={n}',
                  f' requires 0<=IntegerBitwiseModel.rolNat({n},a as int,k)<256 {{ a.RotateLeft(k) }}']
    lines += ['function Identity(a:U64):U64 { a }', 'function Count(k:int):int { k }',
              'function Once(a:U64,k:int):U64 requires 0<=k<=64 {',
              ' P64(); F.LeftRotate(64,a as int,k); R.RotationRange(64,a as int,k); Identity(a).RotateLeft(Count(k)) }',
              'method Out(a:U64,k:int) returns (r:U64,s:int) requires 0<=k<=64 {',
              ' r:=a; s:=k; P64(); F.LeftRotate(64,a as int,k); R.RotationRange(64,a as int,k); r:=r.RotateLeft(s); }']
    for n,a,k in ((12,16,8),(65,2,64),(80,16,76)):
        lines += [f'method NarrowCheck{n}() {{',
                  f' P{n}(); P{k}(); P{n-k}(); F.LeftRotate({n},{a},{k});',
                  f' reveal R.rolNat(); reveal R.norm();',
                  f' print "NARROW:{n}:",NarrowRotate{n}({a},{k}),"\\n"; }}']
    lines += ['method Main() { NarrowCheck12(); NarrowCheck65(); NarrowCheck80();',
              ' print "ONCE:",Once(1,64),"\\n"; var r,s:=Out(1,64); print "OUT:",r,":",s,"\\n";']
    for n in widths:
        lines += [f' Test{n}([{",".join(map(str,values(n)))}]);']
    lines += ['}']
    return lines


def controls():
    lines=definitions((8,))
    for member in ('RotateLeft','RotateRight'):
        for count in (-1,9):
            lines += [f'lemma Bad{member}{abs(count)}(a:U8) {{ P8(); F.{"LeftRotate" if member=="RotateLeft" else "RightRotate"}(8,a as int,{count}); R.RotationRange(8,a as int,{count});',
                      f' var r:=a.{member}({count}); // ERROR', '}']
        lines += [f'lemma False{member}(a:U8,k:int) requires 0<=k<=8 {{',
                  f' var r:U8:={member}8(a,k); assert false; // ERROR', '}']
        lines += [f'lemma Wrong{member}(a:U8,k:int) requires 0<=k<=8 {{',
                  f' var r:U8:={member}8(a,k); assert r as int != R.{"rolNat" if member=="RotateLeft" else "rorNat"}(8,a as int,k); // ERROR', '}']
    for count in (-1,8,9):
        lines += [f'lemma BadBit{abs(count)}(a:U8) {{ var r:=a.bit({count}); // ERROR', '}']
    lines += ['lemma FalseBit(a:U8,i:int) requires 0<=i<8 { var r:=Bit8(a,i); assert false; // ERROR', '}',
              'lemma WrongBit(a:U8,i:int) requires 0<=i<8 { var r:=Bit8(a,i);',
              ' assert r != (IntegerBitwiseModel.shrNat(8,a as int,i)%2==1); // ERROR', '}',
              'newtype {:bitWidth 8} Strict=x:nat | 0<x<256 && x%2==0 witness 2']
    for member,raw in [('RotateLeft','rolNat'),('RotateRight','rorNat')]:
        lines += [f'function Strict{member}(a:Strict,k:int):Strict requires 0<=k<=8 {{',
                  f' P8(); F.{"LeftRotate" if member=="RotateLeft" else "RightRotate"}(8,a as int,k); R.RotationRange(8,a as int,k);',
                  f' a.{member}(k) // ERROR', '}']
    return lines


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny',required=True,type=Path)
    parser.add_argument('--z3',required=True,type=Path)
    parser.add_argument('--output',required=True,type=Path)
    parser.add_argument('--resolver',choices=['both','legacy','refreshed'],default='both')
    parser.add_argument('--additional-axioms',action='store_true')
    parser.add_argument('--quick',action='store_true')
    parser.add_argument('--focus',choices=['all','proof','runtime','controls'],default='all')
    args=parser.parse_args();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
    exe=['dotnet',str(args.dafny.resolve())] if args.dafny.suffix=='.dll' else [str(args.dafny.resolve())]
    modes=['legacy','refreshed'] if args.resolver=='both' else [args.resolver]
    widths=(4,12,64,80) if args.quick else WIDTHS
    checks=[];scopes=[]
    def run(name,command,expected=0):
        rc,text,elapsed=runtime.invoke(exe+command,1200)
        (out/(name+'.log')).write_text(text)
        assert rc==expected,(name,rc,'see retained log')
        assert not re.search(r'out of resource|timed out|inconclusive|Warning:|internal error|Unhandled exception',text,re.I),name
        checks.append(dict(name=name,seconds=elapsed))
        return text
    def verify(name,lines,common,errors=()):
        path=out/(name+'.dfy');path.write_text('\n'.join(lines)+'\n');csvpath=out/(name+'.csv')
        text=run(name,['verify',str(path),*common,'--log-format','csv;LogFileName='+str(csvpath)],4 if errors else 0)
        actual={int(i) for i in re.findall(r'\((\d+),\d+\): Error:',text)}
        assert actual==set(errors),(name,actual,errors)
        with csvpath.open(encoding='utf-8-sig') as f: rows=list(csv.DictReader(f))
        assert rows and all(row['TestResult.Outcome'] in ('Passed','Failed') for row in rows),name
        scopes.extend(dict(check=name,**row) for row in rows)
        return path
    for mode in modes:
        common=['--integer-bitwise','--standard-libraries:false','--solver-path',str(args.z3.resolve()),
                '--cores','2','--show-snippets:false','--allow-warnings:false','--error-limit','0',
                '--type-system-refresh:'+str(mode=='refreshed').lower(),
                '--additional-axioms:'+str(args.additional_axioms).lower(),
                '--resource-limit','200000','--verification-time-limit','60',
                '--boogie','/normalizeDeclarationOrder:0','--boogie','/restartProver']
        if args.focus in ('all','proof'):
            verify(mode+'-proof',definitions(widths),common)
        if args.focus in ('all','runtime'):
            lines=program(widths)
            path=verify(mode+'-program',lines,common)
            text=run(mode+'-runtime',['run',str(path),*common,'--no-verify','--target','cs','--output',str(out/(mode+'-runtime'))])
            rows=[line[5:].split(':') for line in text.splitlines() if line.startswith('CASE:')]
            keys=[(n,a,k) for n in widths for a in values(n) for k in range(n+1)]
            assert [(int(n),int(a),int(k)) for n,a,k,*_ in rows]==keys,(mode,'missing or reordered runtime cases')
            for n,a,k,left,right,bit in rows:
                n,a,k=int(n),int(a),int(k);r=k%n
                # Independent arithmetic quotient/remainder oracle, no source runtime code.
                expected_left=(a*2**r)%(2**n)+a//(2**(n-r))
                expected_right=a//(2**r)+(a%(2**r))*2**(n-r)
                expected_bit=(a//(2**(k%n)))%2==1
                assert (int(left),int(right),bit=='true')==(expected_left,expected_right,expected_bit),(n,a,k)
            assert 'NARROW:12:1' in text.splitlines() and 'NARROW:65:1' in text.splitlines() and 'NARROW:80:1' in text.splitlines()
            assert 'ONCE:1' in text.splitlines() and 'OUT:1:64' in text.splitlines()
            generated=(out/(mode+'-runtime.cs')).read_text()
            body=re.search(r'public static ulong Once\([^}]+}',generated).group()
            assert body.count('__default.Identity(')==1 and body.count('__default.Count(')==1
            checks.append(dict(name=mode+'-oracle',cases=len(rows),comparisons=3*len(rows)))
        if args.focus not in ('all','controls'): continue
        negative=controls();errors=[i for i,line in enumerate(negative,1) if '// ERROR' in line]
        verify(mode+'-controls',negative,common,errors)
        # Syntax and permission errors must be conclusive on both resolution engines.
        for label,body,diagnostic in [
            ('real','function Bad(a:U8):bool { a.bit(0.5) }','must have type int'),
            ('real-variable','function Bad(a:U8,r:real):bool { a.bit(r) }','integer bitwise count or index must have type int'),
            ('arity','function Bad(a:U8):U8 { a.RotateLeft() }','expects one integer argument'),
            ('extra','function Bad(a:U8):bool { a.bit(0,1) }','expects one integer argument'),
            ('named','function Bad(a:U8):bool { a.bit(k:=0) }','expects one integer argument'),
            ('generic','function Bad(a:U8):U8 { a.RotateRight<int>(0) }','no type arguments'),
            ('ghost','function Bad(ghost a:U8):bool { a.bit(0) }','ghost variables such as a'),
            ('collision','newtype {:bitWidth 8} C=x:nat | x<256 { function bit(i:int):bool { true } }','reserved'),
            ('disabled','function Bad(a:U8):U8 { a.RotateLeft(0) }','member'),
        ]:
            path=out/(mode+'-'+label+'.dfy');path.write_text('newtype {:bitWidth 8} U8=x:nat | x<256\n'+body+'\n')
            opts=[v for v in common if v!='--integer-bitwise'] if label=='disabled' else common
            opts=[v for v in opts if v.startswith(('--integer-bitwise','--type-system-refresh:','--standard-libraries:'))]
            text=run(mode+'-'+label,['resolve',str(path),*opts],2)
            assert diagnostic in text or label=='real' and 'type' in text and 'real' in text,(mode,label,'wrong diagnostic')
        def source(label, text):
            path=out/(mode+'-'+label+'.dfy');path.write_text(text+'\n');return path
        for label,body,diagnostic in [
            ('opaque', 'module L { export provides U newtype {:bitWidth 8} U=x:nat | x<256 }\nmodule C { import X=L function Bad(a:X.U):bool { a.bit(0) } }', 'member'),
            ('escape', 'newtype {:bitWidth 8} U=x:nat | x<256\nfunction Bad(a:U):(int->bool) { a.bit }', 'member'),
            ('ghost-count', 'newtype {:bitWidth 8} U=x:nat | x<256\nfunction Bad(a:U,ghost i:int):bool requires 0<=i<8 { a.bit(i) }', 'ghost variables such as i'),
            ('at-label', 'newtype {:bitWidth 8} U=x:nat | x<256\nmethod Bad(a:U) { label L: assert true; var r:=a.bit@L(0); }', '@-label cannot be applied'),
        ]:
            path=source(label,body)
            text=run(mode+'-'+label,['resolve',str(path),'--integer-bitwise','--type-system-refresh:'+str(mode=='refreshed').lower()],2)
            assert diagnostic in text,(mode,label,'wrong diagnostic')
        library_source=source('library',
            'module L { export reveals U, Bit, Left, Right\n'
            ' newtype {:bitWidth 8} U=x:nat | x<256\n'
            ' function Bit(a:U,i:int):bool requires 0<=i<8 { a.bit(i:=i) }\n'
            ' function Left(a:U,k:int):U requires 0<=k<=8 requires 0<=IntegerBitwiseModel.rolNat(8,a as int,k)<256 { a.RotateLeft(k:=k) }\n'
            ' function Right(a:U,k:int):U requires 0<=k<=8 requires 0<=IntegerBitwiseModel.rorNat(8,a as int,k)<256 { a.RotateRight(k) } }')
        library=out/(mode+'-library.doo')
        run(mode+'-library-build',['build',str(library_source),'--target:lib',*common,'--output',str(library)])
        import zipfile
        with zipfile.ZipFile(library) as archive:
            text=archive.read('program').decode()
            assert 'RotateLeft' in text and 'RotateRight' in text and '.bit' in text,(mode,'source surface lost in library')
        client=source('library-client','import X=L\n'
            'lemma Bit(a:X.U,i:int) requires 0<=i<8 { assert X.Bit(a,i)==a.bit(i); }\n'
            'lemma Left(a:X.U,k:int) requires 0<=k<=8 requires 0<=IntegerBitwiseModel.rolNat(8,a as int,k)<256 { assert X.Left(a,k)==a.RotateLeft(k); }\n'
            'lemma Right(a:X.U,k:int) requires 0<=k<=8 requires 0<=IntegerBitwiseModel.rorNat(8,a as int,k)<256 { assert X.Right(a,k)==a.RotateRight(k); }')
        run(mode+'-library-client',['verify',str(client),*common,'--library',str(library)])
        run(mode+'-library-disabled',['verify',str(client),*[v for v in common if v!='--integer-bitwise'],'--library',str(library)],1)
        off_source=source('off-library','module Off { newtype {:bitWidth 8} U=x:nat | x<256 }')
        off_library=out/(mode+'-off-library.doo')
        run(mode+'-off-library-build',['build',str(off_source),'--target:lib',*common,'--integer-bitwise:false','--output',str(off_library)])
        off_client=source('off-client','import X=Off\nfunction Bad(a:X.U):bool { a.bit(0) }')
        text=run(mode+'-off-client',['verify',str(off_client),*common,'--library',str(off_library)],2)
        assert 'require --integer-bitwise' in text
        refinement=source('refinement',library_source.read_text().replace('module L { export reveals U, Bit, Left, Right','abstract module A {')+
            '\nmodule B refines A { lemma Check(a:U,i:int) requires 0<=i<8 { assert Bit(a,i)==a.bit(i); } }')
        run(mode+'-refinement',['verify',str(refinement),*common])
    (out/'summary.json').write_text(json.dumps(dict(checks=checks,scopes=scopes),indent=2)+'\n')
    print('Integer-newtype rotation and bit-source checks passed')


if __name__=='__main__': main()
