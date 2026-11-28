#!/usr/bin/env python3
"""Check the native rotation proof closure, clients and inhabited controls."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess
import os
import signal
import time
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeRotations.dfy'


def declarations(text):
    result, module = [], None
    for line in text.splitlines():
        match = re.match(r'module Std.Arithmetic.(\w+) \{', line)
        if match:
            module = match[1]
        match = re.search(r'lemma (?:\{:[^}]+\}\s*)*(\w+)\(', line)
        if match:
            result.append((module, match[1]))
    return result


def arguments(module, name):
    if module.endswith('Arithmetic'):
        return {'LegalCount': '8,3', 'Parts': '5,2,4', 'LeftMeaning': '8,5,3',
                'RightMeaning': '8,5,3', 'Finish': '5,2,3,256'}[name]
    if name.endswith('Count'):
        return '3'
    if name.endswith('Witness'):
        return '5,0,5'
    return '5,3'


def invalid_arguments(module, name):
    if module.endswith('Arithmetic'):
        return {'LegalCount': ['0,0', '8,9'],
                'Parts': ['5,0,4', '5,2,0', '-1,2,4', '8,2,4'],
                'LeftMeaning': ['0,0,0', '8,5,9', '8,-1,3', '8,256,3'],
                'RightMeaning': ['0,0,0', '8,5,9', '8,-1,3', '8,256,3'],
                'Finish': ['5,2,3,0', '5,2,3,5', '4,2,3,256']}[name]
    n = int(re.search(r'\d+$', module)[0])
    if name == 'ComplementCount':
        return [str(n+1)]
    if name.endswith('Witness'):
        return [f'0,{n+1},0', '5,0,6']
    if name.startswith('Native') or name.endswith('Rotate'):
        return [f'5,{n+1}'] if name.startswith('Native') else [f'5,{n+1}', '5,-1']
    return []


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"',
               'import R = Std.Arithmetic.IntegerBitwise',
               'import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation',
               'import B = Std.Arithmetic.IntegerBitwiseNative8']
    clients, false, negative = prelude.copy(), prelude.copy(), prelude.copy()
    for n in (8, 16, 32, 64):
        clients += [f'import H{n} = Std.Arithmetic.IntegerBitwiseNativeRotations{n}',
                    f'import B{n} = Std.Arithmetic.IntegerBitwiseNative{n}']
        for direction, ref in [('Left', 'rolNat'), ('Right', 'rorNat')]:
            clients += [f'lemma {{:induction false}} {direction}{n}(x:bv{n},k:nat)',
                        f' requires k<={n}',
                        f' ensures x.Rotate{direction}(k) as int==IntegerBitwiseModel.{ref}({n},x as int,k)',
                        f'{{ H{n}.{direction}Rotate(x,k); }}',
                        f'lemma {{:induction false}} {{:isolate_assertions}} Composed{direction}{n}(a:nat,k:nat)',
                        f' requires a<{2**n} && k<={n}',
                        f' ensures (a as bv{n}).Rotate{direction}(k) as int==IntegerBitwiseModel.{ref}({n},a,k)',
                        f'{{ B{n}.RoundTrip(a); var x:=a as bv{n}; assert x as int==a; H{n}.{direction}Rotate(x,k); }}',
                        f'lemma {{:induction false}} {{:isolate_assertions}} Bits{direction}{n}(x:bv{n},k:nat,i:nat)',
                        f' requires k<={n} && i<{n}',
                        f' ensures R.bit({n},x.Rotate{direction}(k) as int,i)==R.bit({n},x as int,(i'+('-' if direction=='Left' else '+')+f'k)%{n})',
                        f'{{ H{n}.{direction}Rotate(x,k); F.{direction}Rotate({n},x as int,k);',
                        f' R.RotationBits({n},x as int,k,i); }}']
            for k in (0, n):
                clients += [f'lemma Endpoint{direction}{n}_{k}() {{',
                            f' H{n}.{direction}Rotate({2**n-1},{k});',
                            f' assert ({2**n-1} as bv{n}).Rotate{direction}({k})=={2**n-1}; }}']
        clients += [f'lemma {{:induction false}} {{:isolate_assertions}} Inverse{n}(x:bv{n},k:nat)',
                    f' requires k<={n}', f' ensures x.RotateRight(k).RotateLeft(k)==x', '{',
                    f' H{n}.RightRotate(x,k); H{n}.LeftRotate(x.RotateRight(k),k);',
                    f' F.RightRotate({n},x as int,k); F.LeftRotate({n},x.RotateRight(k) as int,k);',
                    f' R.RotationAlgebra({n},x as int,k,0); B{n}.Power(); R.NormId({n},x as int);',
                    f' B{n}.Inverse{n}(x); B{n}.Inverse{n}(x.RotateRight(k).RotateLeft(k));','}']
    clients += ['lemma CarrierBoundary() { R.CarrierRotationCounterexample();',
                ' assert R.rolNat(12,16,8)==1;',
                ' assert (16 as bv8).RotateLeft(8) as int==16; }']
    for i, (module, name) in enumerate(declarations(LIBRARY.read_text())):
        for lines in (false, negative):
            lines.append(f'import T{i} = Std.Arithmetic.{module}')
        false += [f'lemma False{i}_{name}() {{ B.Power(); T{i}.{name}({arguments(module,name)});',
                  ' assert false; // ERROR', '}']
        for j, args in enumerate(invalid_arguments(module, name)):
            negative += [f'lemma Negative{i}_{j}() {{', f' T{i}.{name}({args}); // ERROR', '}']
    for n in (8, 16, 32, 64):
        false += [f'import H{n} = Std.Arithmetic.IntegerBitwiseNativeRotations{n}']
        for direction, wrong in [('Left', 9), ('Right', 3)]:
            false += [f'lemma Wrong{direction}{n}() {{ H{n}.{direction}Rotate(5,1);',
                      f' assert (5 as bv{n}).Rotate{direction}(1)=={wrong}; // ERROR', '}']
        false += [f'lemma Distinct{n}() {{ H{n}.LeftRotate(5,1); H{n}.RightRotate(5,1);',
                  f' assert (5 as bv{n}).RotateLeft(1)==(5 as bv{n}).RotateRight(1); // ERROR', '}']
    return clients, false, negative


def invoke(command, timeout=1200):
    """Timeout only this invocation and its descendants, including solvers."""
    start = time.monotonic()
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               text=True, start_new_session=True)
    try:
        text = process.communicate(timeout=timeout)[0]
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGTERM)
        try:
            text = process.communicate(timeout=5)[0]
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            text = process.communicate()[0]
        # A child may survive the parent terminating; the group remains owned.
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        return None, text, time.monotonic()-start
    return process.returncode, text, time.monotonic()-start


def runtime_fixture():
    lines=[f'include "{LIBRARY.as_posix()}"']
    for n in (8,16,32,64):
        lines += [f'import H{n}=Std.Arithmetic.IntegerBitwiseNativeRotations{n}']
        for direction,ref in [('Left','rolNat'),('Right','rorNat')]:
            lines += [f'function {direction}{n}(x:bv{n},k:nat):bv{n}',
                      f' requires k<={n}',
                      f' ensures {direction}{n}(x,k) as int==IntegerBitwiseModel.{ref}({n},x as int,k)',
                      f'{{ H{n}.{direction}Rotate(x,k); x.Rotate{direction}(k) }}']
    lines += ['method Main() {']
    for n in (8,16,32,64):
        # Exhaust the byte domain, sample larger words with an exactly defined
        # native recurrence, and include zero, all ones and both endpoint bits.
        lines += [' {', f' var x:bv{n}:=0; var j:=0;',
                  ' while j<260', '  invariant 0<=j<=260', ' {',
                  '  var k:=0;',f'  while k<={n}',f'   invariant 0<=k<={n+1}',
                  '  {',f'   print "ROT,{n},",j,",",x as int,",",k,",",Left{n}(x,k) as int,",",Right{n}(x,k) as int,"\\n";',
                  '   k:=k+1;', '  }',
                  f'  if j==0 {{ x:={2**n-1}; }}',
                  '  else if j==1 { x:=1; }',f'  else if j==2 {{ x:={2**(n-1)}; }}',
                  (f'  else {{ x:=((j-3)%256) as bv{n}; }}' if n==8 else
                   f'  else {{ x:=x*({6364136223846793005 % 2**n} as bv{n})+({1442695040888963407 % 2**n} as bv{n}); }}'),
                  '  j:=j+1;',' }',' }']
    return '\n'.join(lines+['}'])+'\n'


def runtime_check(exe,common,out,mode):
    path=out/(mode+'-runtime.dfy')
    path.write_text(runtime_fixture())
    option=['--type-system-refresh:'+str(mode=='refreshed').lower()]
    log=out/(mode+'-runtime.csv')
    rc,text,_=invoke(exe+['verify',str(path),*common,*option,'--resource-limit','200000',
                         '--log-format','csv;LogFileName='+str(log)])
    (out/(mode+'-runtime-verify.log')).write_text(text)
    assert rc==0 and not re.search(r'out of resource|timed out|Warning:',text,re.I),(mode,text)
    with log.open(encoding='utf-8-sig') as stream:
        rows=list(csv.DictReader(stream))
    assert rows and all(r['TestResult.Outcome']=='Passed' for r in rows),(mode,rows)
    target=out/(mode+'-native')
    rc,text,_=invoke(exe+['translate','cs',str(path),*option,'--integer-bitwise',
                         '--standard-libraries:false','--no-verify','--output',str(target)])
    (out/(mode+'-translate.log')).write_text(text)
    assert rc==0,(mode,text)
    project=target.with_suffix('.csproj')
    # Translate first, then build explicitly so compilation shares the caller's
    # resource limits and audit setting. No additional packages are necessary.
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                       '<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>'
                       '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                       '</PropertyGroup><ItemGroup><Compile Include="'+escape(str(target.with_suffix('.cs')))+'" />'
                       '<Reference Include="DafnyRuntime"><HintPath>'+escape(str(Path(exe[-1]).parent/'DafnyRuntime.dll'))+
                       '</HintPath></Reference></ItemGroup></Project>')
    binary=out/(mode+'-binary')
    rc,text,_=invoke(['dotnet','build',str(project),'-c','Release','-m:3',
                      '-p:NuGetAudit=false','-p:UseSharedCompilation=false','-o',str(binary)])
    (out/(mode+'-build.log')).write_text(text)
    # Keep package advisory text out of error reports.
    assert rc==0,(mode,'\n'.join(s for s in text.splitlines() if re.search(r' error |Error\(s\)|Warning\(s\)|Build succeeded',s)))
    assembly=binary/(target.name+'.dll')
    rc,text,_=invoke(['dotnet',str(assembly)])
    (out/(mode+'-runtime.log')).write_text(text)
    assert rc==0,(mode,rc)
    actual={}
    for line in text.splitlines():
        values=line.split(',')
        assert len(values)==7 and values[0]=='ROT',(mode,line)
        n,j,x,k,left,right=map(int,values[1:])
        key=(n,j,k)
        assert key not in actual,(mode,key)
        actual[key]=(x,left,right)
    expected={}
    for n in (8,16,32,64):
        m=2**n
        x=0
        for j in range(260):
            for k in range(n+1):
                t=k%n
                expected[n,j,k]=(x,(x*2**t)%m+x//2**(n-t),x//2**t+(x%2**t)*2**(n-t))
            x=m-1 if j==0 else 1 if j==1 else 2**(n-1) if j==2 else (j-3)%256 if n==8 else (x*6364136223846793005+1442695040888963407)%m
    assert actual==expected,(mode,'native runtime differs from integer arithmetic oracle')
    return dict(name=mode+'-runtime',scopes=len(rows),cases=len(actual),failed=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', type=Path, required=True)
    parser.add_argument('--z3', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--resolver', choices=['legacy', 'refreshed', 'both'], default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    parser.add_argument('--mutations-only', action='store_true')
    parser.add_argument('--runtime', action='store_true')
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
    for mode in ([] if args.mutations_only else ['legacy', 'refreshed'] if args.resolver == 'both' else [args.resolver]):
        option = ['--type-system-refresh:' + str(mode == 'refreshed').lower()]
        cases = [('bodies', LIBRARY, ['--verify-included-files'], set(), 2000000)]
        for kind, lines in zip(['clients', 'false', 'negative'], fixtures()):
            path = out / (mode+'-'+kind+'.dfy')
            path.write_text('\n'.join(lines)+'\n')
            cases.append((kind, path, [], {i for i,line in enumerate(lines,1) if '// ERROR' in line}, 200000))
        for kind, path, extra, expected, limit in cases:
            name = mode+'-'+kind
            log = out / (name+'.csv')
            rc, text, elapsed = invoke(exe+['verify', str(path), *common, *option, *extra,
                                        '--resource-limit', str(limit), '--log-format',
                                        'csv;LogFileName='+str(log)])
            (out/(name+'.log')).write_text(text)
            assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name,text)
            assert rc == (4 if expected else 0), (name,rc,text)
            actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:',text)}
            assert actual==expected, (name,actual,expected,text)
            with log.open(encoding='utf-8-sig') as stream:
                rows=list(csv.DictReader(stream))
            assert rows and all(r['TestResult.Outcome'] in ('Passed','Failed') for r in rows), (name,rows)
            assert sum(r['TestResult.Outcome']=='Failed' for r in rows)>=len(expected), (name,rows)
            records.append(dict(name=name,exit=rc,scopes=len(rows),failed=sum(r['TestResult.Outcome']=='Failed' for r in rows),rejected_lines=len(expected),seconds=elapsed))
        if args.runtime:
            records.append(runtime_check(exe,common,out,mode))
    if args.mutations_only:
        original = LIBRARY.read_text()
        headers = list(re.finditer(r'^[ \t]*lemma (?:\{:[^}]+\}\s*)*(\w+)\(',original,re.M))
        rules = declarations(original)
        assert rules and len(headers)==len(rules)
        path = out/'mutation.dfy'
        def check(site, source, symbol, timeout):
            source = source.replace('include "IntegerBitwiseNativeShifts.dfy"',
                                    'include "'+(LIBRARY.parent/'IntegerBitwiseNativeShifts.dfy').as_posix()+'"')
            path.write_text(source)
            log=out/(site+'.csv')
            rc,text,elapsed=invoke(exe+['verify',str(path),*common,'--type-system-refresh:true',
                                       '--filter-symbol',symbol+'.','--resource-limit','2000000',
                                       '--log-format','csv;LogFileName='+str(log)],timeout)
            (out/(site+'.log')).write_text(text)
            return rc,text,elapsed,log
        for i,header in enumerate(headers):
            module,name=rules[i]
            end=headers[i+1].start() if i+1<len(headers) else len(original)
            section=original[header.start():end]
            # Concrete inhabited inputs avoid any accidental inconsistent context.
            signature=original[header.start():original.index('\n',header.start())]
            names=[p.split(':')[0].strip() for p in re.search(r'\((.*)\)',signature)[1].split(',')]
            values=arguments(module,name).split(',')
            assert len(names)==len(values)
            at=original.index('\n',header.start())
            restriction='\n    requires '+' && '.join(n+'=='+v for n,v in zip(names,values))
            baseline=original[:at]+restriction+original[at:]
            symbol=module+'.'+name
            rc,text,clean,_=check('clean-'+symbol,baseline,symbol,120)
            assert rc==0 and not re.search(r'out of resource|timed out|Warning:',text,re.I),(symbol,text)
            timeout=max(10,3*clean)
            for j,contract in enumerate(re.finditer(r'^[ \t]+ensures (.+)$',section,re.M)):
                a,b=header.start()+contract.start(),header.start()+contract.end()
                mutant=original[:a]+'\n    ensures !('+contract[1]+')'+original[b:]
                mutant=mutant[:at]+restriction+mutant[at:]
                site=symbol+'.ensures.'+str(j)
                rc,text,elapsed,log=check(site,mutant,symbol,timeout)
                if rc is None:
                    result='killed (timeout)'
                else:
                    assert rc==4 and re.search(r'could not be proved',text),(site,rc,text)
                    assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:',text,re.I),(site,text)
                    with log.open(encoding='utf-8-sig') as stream:
                        rows=list(csv.DictReader(stream))
                    assert rows and any(r['TestResult.Outcome']=='Failed' for r in rows),(site,rows)
                    result='killed'
                records.append(dict(name=site,result=result,clean_seconds=clean,seconds=elapsed,timeout=timeout))
            if 'requires ' in section:
                # Remove the domain guard as a unit: some arithmetic premises
                # imply one another, so individual deletion is not always a
                # semantic mutation. Each domain has designated invalid callers.
                mutant=original[:header.start()]+re.sub(r'^[ \t]+requires .+\n','',section,flags=re.M)+original[end:]
                mutant=mutant.replace('include "IntegerBitwiseNativeShifts.dfy"',
                                      'include "'+(LIBRARY.parent/'IntegerBitwiseNativeShifts.dfy').as_posix()+'"')
                path.write_text(mutant)
                lines=fixtures()[2]
                lines[0]=f'include "{path.as_posix()}"'
                caller=out/'mutation-callers.dfy'
                caller.write_text('\n'.join(lines)+'\n')
                expected={j for j,line in enumerate(lines,1) if '// ERROR' in line}
                site=symbol+'.domain'
                log=out/(site+'.csv')
                # The unmodified negative gate is the clean baseline for these
                # rejection-loss mutations; use a generous explicit safety cap.
                rc,text,elapsed=invoke(exe+['verify',str(caller),*common,'--type-system-refresh:true',
                                           '--resource-limit','200000','--log-format','csv;LogFileName='+str(log)],30)
                (out/(site+'.log')).write_text(text)
                assert rc in (0,4) and not re.search(r'out of resource|timed out|Warning:',text,re.I),(site,rc,text)
                actual={int(j) for j in re.findall(r'\((\d+),\d+\): Error:',text)}
                assert actual<expected,(site,actual,expected,text)
                designated={j for j,line in enumerate(lines,1) if f'T{i}.{name}(' in line}
                assert expected-actual<=designated,(site,expected-actual,designated)
                records.append(dict(name=site,result='killed',lost_rejections=sorted(expected-actual),seconds=elapsed,timeout=30))
                caller.unlink()
        path.unlink()
    assert records, 'No checks selected'
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native rotation checks passed')


if __name__ == '__main__':
    main()
