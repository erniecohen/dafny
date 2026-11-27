#!/usr/bin/env python3
"""Qualify sign-preserving native right shifts, live controls and mutations."""
import argparse
import csv
import json
import os
from pathlib import Path
import random
import re
import signal
import shutil
import subprocess
import time

ROOT=Path(__file__).resolve().parents[2]
LIBRARY=ROOT/'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeSignedShifts.dfy'
SIGNED=LIBRARY.with_name('IntegerBitwiseNativeSigned.dfy')
DEPENDENCY=LIBRARY.with_name('IntegerBitwiseNativeShifts.dfy')
WIDTHS=(8,16,32,64)


def fixtures():
    prelude=[f'include "{LIBRARY.as_posix()}"',
             'import R=Std.Arithmetic.IntegerBitwise',
             'import A=Std.Arithmetic.IntegerBitwiseSignedShiftArithmetic']
    positive,negative=prelude.copy(),prelude.copy()
    positive += ['lemma Arithmetic(a:int,d:int) requires d>0',
                 ' ensures (-a-1)/d == -1-a/d',
                 '{ A.NegativeQuotient(a,d); }']
    negative += ['lemma FalseArithmetic() { A.NegativeQuotient(2,2);',
                 ' assert false; // ERROR','}',
                 'lemma ZeroDivisor() { A.NegativeQuotient(2,0); // ERROR','}',
                 'lemma NegativeDivisor() { A.NegativeQuotient(2,-1); // ERROR','}',
                 'lemma WrongArithmetic() { A.NegativeQuotient(2,2);',
                 ' assert (-2-1)/2 == -2/2; // ERROR','}']
    for w in WIDTHS:
        m,h=2**w,2**(w-1)
        for lines in (positive,negative):
            lines += [f'import N{w}=Std.Arithmetic.IntegerBitwiseNativeSignedShifts{w}',
                      f'import S{w}=Std.Arithmetic.IntegerBitwiseNativeSigned{w}',
                      f'import B{w}=Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'lemma Native{w}(x:bv{w},k:nat) requires k<={w}',
                     f' ensures S{w}.Decode(N{w}.Right(x,k))==S{w}.Decode(x)/R.P(k)',
                     f'{{ var r:=N{w}.Right(x,k); }}',
                     f'lemma Composed{w}(a:int,k:nat)',
                     f' requires -{h}<=a<{h} && k<={w}',
                     f' ensures S{w}.Decode(N{w}.Right(S{w}.Encode(a),k))==a/R.P(k)',
                     f'{{ S{w}.RoundTrip(a); var r:=N{w}.Right(S{w}.Encode(a),k); }}',
                     f'lemma ZeroCount{w}(x:bv{w}) ensures N{w}.Right(x,0)==x',
                     f'{{ R.PowerZero(); var r:=N{w}.Right(x,0); S{w}.Inverse(x); S{w}.Inverse(r); }}',
                     f'lemma FullCount{w}(x:bv{w})',
                     f' ensures S{w}.Decode(N{w}.Right(x,{w}))==(if S{w}.Decode(x)<0 then -1 else 0)',
                     f'{{ B{w}.Power(); S{w}.Range(x); var a:=S{w}.Decode(x);',
                     f' if a<0 {{ R.Quotient(a,{m},-1,a+{m}); }} else {{ R.Quotient(a,{m},0,a); }}',
                     f' var r:=N{w}.Right(x,{w}); }}']
        values=[(0,0,0),(0,w,0),(h-1,1,(h-1)//2),(h,1,-h//2),
                (m-1,0,-1),(m-1,w,-1),(m-3,1,-2),(3,1,1)]
        for j,(x,k,expected) in enumerate(values):
            signed=x if x<h else x-m
            positive += [f'lemma Concrete{w}_{j}() {{ var x:bv{w}:={x};',
                         f' assert S{w}.Decode(x)=={signed} by {{ B{w}.RoundTrip({x}); }}',
                         f' assert R.P({k})=={1<<k} by {{ B{w}.Power(); R.PowerZero(); reveal R.P(); }}',
                         f' R.Quotient({signed},{1<<k},{expected},{signed-expected*(1<<k)});',
                         f' var r:=N{w}.Right(x,{k}); assert S{w}.Decode(r)=={expected};', '}']
        observations = [f'N{w}.Observe(0,0,0)', f'N{w}.Observe({m-1},{w},{m-1})']
        together = [f' N{w}.SignDecision({m-3}); N{w}.Observe({m-3},1,{m-2});']
        calls=[('SignPositive',f'N{w}.SignDecision(0)'),
               ('SignNegative',f'N{w}.SignDecision({m-1})'),
               ('ObservePositive',observations[0]), ('ObserveNegative',observations[1])]
        for name,call in calls:
            negative += [f'lemma False{name}{w}() {{ {call};', ' assert false; // ERROR','}']
        for j,(x,k) in enumerate(((0,0),(0,w),(m-1,0),(m-1,w),(m-3,1))):
            signed=x if x<h else x-m
            expected=(signed>>k)%m
            negative += [f'lemma FalseRight{w}_{j}() {{ var r:=N{w}.Right({x},{k});',
                         f' assert r=={expected} by {{ reveal N{w}.Right(); }}',
                         ' assert false; // ERROR','}']
        negative += [f'lemma FalseTogether{w}() {{ A.NegativeQuotient(2,2);', *together,
                     f' var r:=N{w}.Right({m-3},1);',
                     f' assert r=={m-2} by {{ reveal N{w}.Right(); }}', ' assert false; // ERROR','}']
        bad={'RightCount':f'var r:=N{w}.Right(0,{w+1})',
             'RightNegative':f'var r:=N{w}.Right(0,-1)',
             'ObserveCount':f'N{w}.Observe(0,{w+1},0)',
             'ObserveNegative':f'N{w}.Observe(0,-1,0)',
             'ObserveResult':f'N{w}.Observe(0,0,1)',
             'SignCast':f'N{w}.SignDecision({m} as bv{w})',
             'RightUpper':f'var r:=N{w}.Right({m} as bv{w},0)',
             'RightLower':f'var r:=N{w}.Right((-1) as bv{w},0)'}
        for name,call in bad.items():
            negative += [f'lemma {name}{w}() {{ {call}; // ERROR','}']
        for name,x,k,wrong in [('NegativeRounding',m-3,1,-1),('PositiveRounding',3,1,2),
                                ('NegativeEnd',m-1,w,0),('PositiveEnd',h-1,w,1),
                                ('Direction',4,1,8),('Sign',m-1,1,1)]:
            correct=((x if x<h else x-m)>>k)%m
            negative += [f'lemma Wrong{name}{w}() {{ var r:=N{w}.Right({x},{k});',
                         f' assert r=={correct} by {{ reveal N{w}.Right(); }}',
                         f' assert S{w}.Decode(r)=={wrong}; // ERROR','}']
    return positive,negative


def runtime_fixture():
    lines=[f'include "{LIBRARY.as_posix()}"']
    helpers=[];main=['method Main() {'];expected=[]
    for w in WIDTHS:
        m,h=2**w,2**(w-1)
        rng=random.Random(w)
        values=set(range(256)) if w==8 else {0,1,2,3,4,h-1,h,h+1,m-1,m-2,m-3,
                    sum(1<<i for i in range(0,w,2)),sum(1<<i for i in range(1,w,2))}
        if w>8:
            values.update(1<<i for i in range(w))
            values.update((m-1)^(1<<i) for i in range(w))
            values.update(rng.getrandbits(w) for _ in range(32))
        values=sorted(values)
        lines += [f'import N{w}=Std.Arithmetic.IntegerBitwiseNativeSignedShifts{w}']
        helpers += [f'method Test{w}(xs:seq<bv{w}>) {{',
                    f' for j:=0 to |xs| {{ for k:=0 to {w+1} {{',
                    f'  var r:=N{w}.Right(xs[j],k);',
                    f'  print "{w}:",xs[j],":",k,":",r,"\\n";', ' } }','}']
        main += [f' var xs{w}:seq<bv{w}>:=['+','.join(map(str,values))+'];',f' Test{w}(xs{w});']
        for x in values:
            signed=x if x<h else x-m
            for k in range(w+1):
                # Independent signed integer arithmetic: Python >> is floor division.
                expected += [f'{w}:{x}:{k}:{(signed>>k)%m}']
    return lines+helpers+main+['}'],expected


def invoke(command, timeout, sample=None):
    started = time.monotonic()
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, stdin=subprocess.DEVNULL, start_new_session=True)
    killed = False
    try:
        stdout, stderr = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        killed = True
        if sample is not None:
            # Preserve a diagnostic before stopping only this owned process group.
            sampler = shutil.which('sample')
            diagnostic = [sampler, str(process.pid), '2'] if sampler else ['ps', '-p', str(process.pid), '-o', 'pid,ppid,state,etime,command']
            with sample.open('w') as stream:
                stream.write(f'PID {process.pid}; timeout {timeout}; UTC '+time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())+'\n')
                stream.flush()
                try:
                    subprocess.run(diagnostic, stdout=stream, stderr=subprocess.STDOUT, timeout=10)
                except (OSError, subprocess.TimeoutExpired) as error:
                    stream.write(str(error)+'\n')
        # The verifier and its solver children own this new process group.
        os.killpg(process.pid, signal.SIGTERM)
        try:
            stdout, stderr = process.communicate(timeout=5)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            stdout, stderr = process.communicate()
    return process.returncode, stdout + stderr, time.monotonic()-started, killed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', type=Path, required=True)
    parser.add_argument('--z3', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--mutations-only', action='store_true')
    parser.add_argument('--resolver', choices=('legacy', 'refreshed', 'both'), default='both')
    parser.add_argument('--additional-axioms', action='store_true')
    parser.add_argument('--mutation-filter', default='')
    parser.add_argument('--runtime', action='store_true')
    parser.add_argument('--node-timeout', type=float, default=4)
    args = parser.parse_args()
    assert args.node_timeout > 0
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
    def verify(name, path, options=(), errors=(), mutation=False, limit=2000000, timeout=110,
               expected_scopes=None):
        csv_path = out / (name+'.csv')
        command = exe + ['verify', str(path), *common,
                         '--resource-limit', str(limit), '--log-format',
                         'csv;LogFileName='+str(csv_path), *options]
        code, text, elapsed, killed = invoke(command, timeout)
        (out / (name+'.log')).write_text(text)
        assert not killed or mutation, (name, 'baseline/control timed out', text)
        if killed:
            return dict(name=name, exit=code, outcome='killed', timeout=timeout, elapsed=elapsed)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception|Prover error', text, re.I), (name, text)
        assert code == (4 if errors or mutation else 0), (name, code, text)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:', text)}
        if not mutation:
            assert actual == set(errors), (name, actual, errors, text)
        else:
            assert re.search(r'postcondition could not be proved|precondition.*could not be proved|value to be converted might not fit|invariant could not be proved|might not decrease|value does not satisfy|must be non-negative|index out of range|possible division by zero', text), (name, text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        assert (failed>=1 if mutation else failed==len(errors)), (name,failed,errors)
        if expected_scopes is not None:
            assert len(rows) == expected_scopes, (name, len(rows), expected_scopes)
        return dict(name=name, exit=code, outcome='failed' if failed else 'passed',
                    scopes=len(rows), failed=failed, elapsed=elapsed)


    modes=('legacy','refreshed') if args.resolver=='both' else (args.resolver,)
    if not args.mutations_only:
        code,text,_,killed=invoke(exe+['verify',str(LIBRARY),*common,'--integer-bitwise:false'],110)
        (out/'option-off.log').write_text(text)
        assert not killed and code==2 and 'unresolved identifier: IntegerBitwiseModel' in text,text
        records.append(dict(name='option-off',exit=2,scopes=0,failed=0))
        for mode in modes:
            options=['--type-system-refresh:'+str(mode=='refreshed').lower()]
            records.append(verify(mode+'-dependencies',DEPENDENCY,[*options,'--verify-included-files'],expected_scopes=1176))
            records.append(verify(mode+'-signed',SIGNED,options,expected_scopes=92))
            records.append(verify(mode+'-bodies',LIBRARY,options,expected_scopes=141))
            for kind,lines in zip(('clients','negative'),fixtures()):
                path=out/(mode+'-'+kind+'.dfy');path.write_text('\n'.join(lines)+'\n')
                errors={i for i,line in enumerate(lines,1) if '// ERROR' in line}
                assert len(errors)==(100 if kind=='negative' else 0)
                records.append(verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000,expected_scopes=100 if errors else 66))
            if args.runtime:
                lines,expected=runtime_fixture()
                path=out/'runtime.dfy';path.write_text('\n'.join(lines)+'\n')
                (out/'runtime.expected').write_text('\n'.join(expected)+'\n')
                records.append(verify(mode+'-runtime-input',path,options,expected_scopes=5))
                for target in ('cs','js'):
                    name=mode+'-runtime-'+target
                    if target == 'js':
                        # The CLI run path drains stderr before stdout. Emit a file
                        # and drain both streams concurrently for this output oracle.
                        code,text,_,killed=invoke(exe+['translate','js',str(path),*common,*options,
                            '--no-verify','--include-runtime','--output',str(out/name)],110)
                        (out/(name+'-translate.log')).write_text(text)
                        assert not killed and code==0,(name,'translation failed',code)
                        code,text,elapsed,killed=invoke(['node',str(out/(name+'.js'))],
                            args.node_timeout,sample=out/(name+'-sample.log'))
                    else:
                        code,text,elapsed,killed=invoke(exe+['run',str(path),*common,*options,
                            '--no-verify','--target',target,'--output',str(out/name)],110)
                    (out/(name+'.log')).write_text(text)
                    assert not killed and code==0,(name,code,'see runtime log',killed)
                    actual=re.findall(r'^\d+:\d+:\d+:\d+$',text,re.M)
                    assert actual==expected,(name,'runtime output differs',len(actual),len(expected))
                    records.append(dict(name=name,exit=code,cases=len(expected),elapsed=elapsed))
    else:
        original=LIBRARY.read_text().replace('include "IntegerBitwiseNativeSigned.dfy"',
                                             'include "'+SIGNED.as_posix()+'"')
        sites=[]
        for module in ('IntegerBitwiseSignedShiftArithmetic',*(f'IntegerBitwiseNativeSignedShifts{w}' for w in WIDTHS)):
            start=original.index('module Std.Arithmetic.'+module)
            end=original.find('\nmodule ',start+1)
            end=end if end>=0 else len(original)
            section=original[start:end]
            headers=list(re.finditer(r'^  (?:lemma |opaque function )(?:\{:[^}]+\}\s*)*(\w+)\(',section,re.M))
            for i,header in enumerate(headers):
                last=headers[i+1].start() if i+1<len(headers) else len(section)
                body=section[header.start():last]
                for j,contract in enumerate(re.finditer(r'^    (requires|ensures) (.+)$',body,re.M)):
                    at=start+header.start()+contract.start();stop=start+header.start()+contract.end()
                    replacement='    ensures !('+contract[2]+')' if contract[1]=='ensures' else ''
                    sites.append((f'{module}.{header[1]}.{contract[1]}{j}',original,
                                  original[:at]+replacement+original[stop:],
                                  ['--filter-symbol',f'{module}.{header[1]}.']))
            if module.endswith('Arithmetic'):
                continue
            w=int(re.search(r'(\d+)$',module)[1]);h=2**(w-1);m=2**w
            header=next(x for x in headers if x[1]=='Right')
            region=section[header.start():]
            # Constrain each changed runtime expression to an inhabited witness.
            for name,old,new,x in [('condition',f'x<{h}',f'x>={h}',4),
                                   ('boundary',f'x<{h}',f'x<={h}',h),
                                   ('positive-direction','then x>>k','then x<<k',4),
                                   ('sign-extension','!((!x)>>k)','x>>k',m-3),
                                   ('inner-complement','!((!x)>>k)','!(x>>k)',m-3),
                                   ('count','>>k','>>0',4)]:
                assert old in region,(w,name)
                pos=start+header.start();at=original.index('\n',pos)
                guard=f'\n    requires x=={x} && k==1'
                baseline=original[:at]+guard+original[at:]
                altered=region.replace(old,new)
                mutant=original[:pos]+altered+original[end:]
                mutant=mutant[:at]+guard+mutant[at:]
                sites.append((f'{module}.Right.{name}',baseline,mutant,
                              ['--filter-symbol',f'{module}.Right.']))
        assert len(sites)==54,len(sites)
        sites=[s for s in sites if args.mutation_filter in s[0]];assert sites
        for mode in modes:
            for site,baseline,mutant,options in sites:
                options=['--type-system-refresh:'+str(mode=='refreshed').lower(),*options]
                path=out/'baseline.dfy';path.write_text(baseline)
                clean=verify(mode+'.'+site+'.baseline',path,options)
                path=out/'mutation.dfy';path.write_text(mutant)
                result=verify(mode+'.'+site,path,options,mutation=True,timeout=max(5,3*clean['elapsed']))
                result['baseline']=clean;records.append(result)
                (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native signed right-shift checks passed')


if __name__=='__main__':
    main()
