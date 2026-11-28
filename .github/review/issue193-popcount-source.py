#!/usr/bin/env python3
"""Qualify PopCount source calls against checked references and an arithmetic oracle."""
import argparse
import csv
import importlib.util
import json
from pathlib import Path
import random
import re
import zipfile

ROOT = Path(__file__).resolve().parents[2]
FOUNDATION = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwisePopulation.dfy'
spec = importlib.util.spec_from_file_location('runtime_gate', Path(__file__).with_name('issue193-runtime-csharp.py'))
runtime = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runtime)
WIDTHS = (1, 4, 5, 8, 12, 16, 31, 32, 63, 64, 65, 80, 128, 129, 256)


def definitions(widths):
    lines = [f'include "{FOUNDATION.as_posix()}"',
             'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation',
             'import R = Std.Arithmetic.IntegerBitwise',
             'import C = Std.Arithmetic.IntegerBitwisePopulation']
    for n in widths:
        carrier = {1:'"sbyte"',4:'"byte"',5:'"sbyte"',8:'"byte"',12:'"ushort"',16:'"ushort"',
                   31:'"int"',32:'"uint"',63:'"long"',64:'"ulong"'}.get(n,'false')
        lines += [f'newtype {{:bitWidth {n}}} {{:nativeType {carrier}}} U{n}=x:nat | x<{2**n}',
                  f'function Population{n}(a:U{n}):nat',
                  f' ensures Population{n}(a)==IntegerBitwiseModel.popNat({n},a as int)',
                  f' ensures Population{n}(a)==R.count(R.bitsNat({n},a as int))',
                  f' ensures Population{n}(a)<={n}',
                  f'{{ C.View({n},a as int); a.PopCount() }}']
        lines += [f'lemma Zero{n}() ensures Population{n}(0)==0',
                  f'{{ R.PopZero({n}); F.Population({n},0); }}',
                  f'lemma Ones{n}(a:U{n}) requires a as int==R.P({n})-1 ensures Population{n}(a)=={n}',
                  f'{{ C.AllOnes({n}); }}',
                  f'lemma Power{n}(a:U{n},i:nat) requires i<{n} requires a as int==R.P(i)',
                  f' ensures Population{n}(a)==1 {{ R.PopPower({n},i); F.Population({n},a as int); }}',
                  f'lemma High{n}(a:U{n}) requires a as int==R.P({n-1}) ensures Population{n}(a)==1',
                  f'{{ Power{n}(a,{n-1}); }}']
    # Its inhabited predicate excludes population results 0, 1 and 8.
    lines += ['newtype {:bitWidth 8} {:nativeType "byte"} Strict=x:nat | 16<=x<256 && x%2==0 witness 16',
              'function StrictPopulation(a:Strict):nat ensures StrictPopulation(a)<=8',
              ' ensures StrictPopulation(a)==IntegerBitwiseModel.popNat(8,a as int)',
              '{ C.View(8,a as int); a.PopCount() }']
    return lines


def values(n):
    if n<=8: return list(range(2**n))
    rng=random.Random(n)
    return sorted({0,1,2,3,2**n-1,2**(n-1)-1} | {1<<i for i in range(n)} |
                  {sum(2**i for i in range(n) if i%2==p) for p in (0,1)} |
                  {rng.getrandbits(n) for _ in range(12)})


def program(widths):
    lines=definitions(widths)
    for n in widths:
        lines += [f'method Test{n}(xs:seq<int>) {{ for i:=0 to |xs| {{ if 0<=xs[i]<{2**n} {{',
                  f' var a:=xs[i] as U{n}; print "CASE:{n}:",a,":",Population{n}(a),"\\n";', ' } } }']
    for n in (12,65,80,129):
        # The semantic bound is N; storage does not become a result predicate.
        lines += [f'newtype {{:bitWidth {n}}} {{:nativeType "byte"}} Narrow{n}=x:nat | 128<=x<256 witness 128',
                  f'function NarrowCount{n}(a:Narrow{n}):nat ensures NarrowCount{n}(a)<={n}',
                  f'{{ C.View({n},a as int); a.PopCount() }}']
    lines += ['function Shadow(bits:U80,count:int):nat ensures Shadow(bits,count)<=80 { C.View(80,bits as int); bits.PopCount() }',
              'function Identity(a:U64):U64 { a }',
              'function Once(a:U64):nat ensures Once(a)<=64 { C.View(64,a as int); Identity(a).PopCount() }',
              'method Out(a:U64) returns (r:U64,n:nat) { r:=a; C.View(64,r as int); n:=r.PopCount(); }',
              'method Main() { print "ONCE:",Once(1),"\\n"; var r,n:=Out(3); print "OUT:",r,":",n,"\\n";',
              ' print "STRICT:",StrictPopulation(16),"\\n";',
              ' print "SHADOW:",Shadow(3,0),"\\n";']
    for n in (12,65,80,129): lines += [f' print "NARROW:{n}:",NarrowCount{n}(255),"\\n";']
    for n in widths: lines += [f' Test{n}([{",".join(map(str,values(n)))}]);']
    return lines+['}']


def controls():
    return definitions((8,))+[
        'lemma FalseCount(a:U8) { var n:=Population8(a); assert false; // ERROR', '}',
        'lemma WrongCount(a:U8) { var n:=Population8(a); assert n!=R.count(R.bitsNat(8,a as int)); // ERROR', '}',
        'lemma WrongRange(a:U8) { var n:=Population8(a); assert n>8; // ERROR', '}',
        'lemma BelowZero(a:U8) { var n:=Population8(a); assert n<0; // ERROR', '}',
        'lemma WrongStrict(a:Strict) { var n:=StrictPopulation(a); assert n>=16; // ERROR', '}',
        'function MissingRange(a:U8):nat { a.PopCount() } // ERROR',
        'lemma GenericFalse(n:nat,a:int) { C.View(n,a); C.AllOnes(n); assert false; // ERROR', '}',
        'lemma WrongOnes(n:nat) { C.AllOnes(n); assert IntegerBitwiseModel.popNat(n,R.P(n)-1)==n+1; // ERROR', '}',
        'lemma EmptyWord() { C.AllOnes(0); C.View(0,-1); assert false; // ERROR', '}']


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
        if args.focus in ('all','proof'): verify(mode+'-proof',definitions(widths),common)
        if args.focus in ('all','runtime'):
            path=verify(mode+'-program',program(widths),common)
            text=run(mode+'-runtime',['run',str(path),*common,'--no-verify','--target','cs','--output',str(out/(mode+'-runtime'))])
            rows=[line[5:].split(':') for line in text.splitlines() if line.startswith('CASE:')]
            keys=[(n,a) for n in widths for a in values(n)]
            assert [(int(n),int(a)) for n,a,_ in rows]==keys,(mode,'missing or reordered runtime cases')
            for n,a,population in rows:
                n,a=int(n),int(a)
                # Independent arithmetic digits; no host popcount or bitwise API.
                expected=sum((a//2**i)%2 for i in range(n))
                assert int(population)==expected,(n,a,population,expected)
            for n in (12,65,80,129): assert f'NARROW:{n}:8' in text.splitlines()
            assert 'SHADOW:2' in text.splitlines()
            assert 'STRICT:1' in text.splitlines() and 'ONCE:1' in text.splitlines() and 'OUT:3:2' in text.splitlines()
            generated=(out/(mode+'-runtime.cs')).read_text()
            body=re.search(r'public static BigInteger Once\([^}]+}',generated).group()
            assert body.count('__default.Identity(')==1
            assert 'BitOperations.PopCount' in generated and '>>= 64;' in generated
            checks.append(dict(name=mode+'-oracle',cases=len(rows),comparisons=len(rows)))
        if args.focus not in ('all','controls'): continue
        negative=controls();errors=[i for i,line in enumerate(negative,1) if '// ERROR' in line]
        verify(mode+'-controls',negative,common,errors)
        for label,body,diagnostic in [
            ('arity','function Bad(a:U):nat { a.PopCount(0) }','expects no arguments'),
            ('named','function Bad(a:U):nat { a.PopCount(k:=0) }','expects no arguments'),
            ('generic','function Bad(a:U):nat { a.PopCount<int>() }','no type arguments'),
            ('ghost','function Bad(ghost a:U):nat { a.PopCount() }','ghost variables such as a'),
            ('collision','newtype {:bitWidth 8} C=x:nat | x<256 { function PopCount():nat { 7 } }','reserved'),
            ('disabled','function Bad(a:U):nat { a.PopCount() }','member'),
            ('escape','function Bad(a:U):(() -> nat) { a.PopCount }','member'),
            ('at-label','method Bad(a:U) { label L: assert true; var r:=a.PopCount@L(); }','@-label cannot be applied'),
            ('opaque','module L { export provides T newtype {:bitWidth 8} T=x:nat | x<256 } module C { import X=L function Bad(a:X.T):nat { a.PopCount() } }','member'),
        ]:
            path=out/(mode+'-'+label+'.dfy');path.write_text('newtype {:bitWidth 8} U=x:nat | x<256\n'+body+'\n')
            opts=['--type-system-refresh:'+str(mode=='refreshed').lower(),'--standard-libraries:false']
            if label!='disabled': opts+=['--integer-bitwise']
            text=run(mode+'-'+label,['resolve',str(path),*opts],2)
            assert diagnostic in text,(mode,label,'wrong diagnostic')
        def source(label, text):
            path=out/(mode+'-'+label+'.dfy');path.write_text(text+'\n');return path
        library_source=source('library',f'include "{FOUNDATION.as_posix()}"\nmodule L {{ export reveals U, Count provides C\n'
            ' import C=Std.Arithmetic.IntegerBitwisePopulation newtype {:bitWidth 8} U=x:nat | x<256\n'
            ' function Count(a:U):nat ensures Count(a)<=8 ensures Count(a)==IntegerBitwiseModel.popNat(8,a as int) { C.View(8,a as int); a.PopCount() } }')
        library=out/(mode+'-library.doo')
        run(mode+'-library-build',['build',str(library_source),'--target:lib',*common,'--output',str(library)])
        with zipfile.ZipFile(library) as archive: assert '.PopCount()' in archive.read('program').decode()
        client=source('library-client','import X=L\nlemma Count(a:X.U) { var n:=X.Count(a); assert n==a.PopCount(); assert a.PopCount()<=8; }')
        run(mode+'-library-client',['verify',str(client),*common,'--library',str(library)])
        run(mode+'-library-disabled',['verify',str(client),*[v for v in common if v!='--integer-bitwise'],'--library',str(library)],1)
        off_source=source('off-library','module Off { newtype {:bitWidth 8} U=x:nat | x<256 }')
        off_library=out/(mode+'-off-library.doo')
        run(mode+'-off-library-build',['build',str(off_source),'--target:lib',*common,'--integer-bitwise:false','--output',str(off_library)])
        off_client=source('off-client','import X=Off\nfunction Bad(a:X.U):nat { a.PopCount() }')
        text=run(mode+'-off-client',['verify',str(off_client),*common,'--library',str(off_library)],2)
        assert 'require --integer-bitwise' in text
        refinement=source('refinement',library_source.read_text().replace('module L { export reveals U, Count provides C','abstract module A {')+
            '\nmodule B refines A { lemma Check(a:U) { var n:=Count(a); assert n==a.PopCount(); assert a.PopCount()<=8; } }')
        run(mode+'-refinement',['verify',str(refinement),*common])
    (out/'summary.json').write_text(json.dumps(dict(checks=checks,scopes=scopes),indent=2)+'\n')
    print('Integer-newtype population-count source checks passed')


if __name__=='__main__': main()
