#!/usr/bin/env python3
"""Qualify checked native population counts, runtime oracles and serial mutations."""
import argparse
import csv
import json
import os
from pathlib import Path
import re
import random
import signal
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativePopulation.dfy'
DEPENDENCY = LIBRARY.with_name('IntegerBitwiseNative.dfy')
WIDTHS = (8, 16, 32, 64)


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"',
               'import R=Std.Arithmetic.IntegerBitwise',
               'import F=Std.Arithmetic.IntegerBitwiseCompilerFoundation']
    positive, negative = prelude.copy(), prelude.copy()
    for w in WIDTHS:
        m, h = 2**w, 2**(w-1)
        for lines in (positive, negative):
            lines += [f'import N{w}=Std.Arithmetic.IntegerBitwiseNativePopulation{w}',
                      f'import B{w}=Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype U{w}=a:int | 0<=a<{m}',
                     f'method Native{w}(x:bv{w}) returns(c:bv8)',
                     f' ensures c as int==R.popNat({w},x as int)',
                     f' ensures c as int<={w}',
                     f'{{ c:=N{w}.Count(x); F.Population({w},x as int); }}',
                     f'method Composed{w}(a:U{w}) returns(c:bv8)',
                     f' ensures c as int==IntegerBitwiseModel.popNat({w},a as int)',
                     f'{{ B{w}.RoundTrip(a as int); c:=N{w}.Count(a as bv{w}); }}',
                     f'method Zero{w}() {{ var c:=N{w}.Count(0);',
                     f' F.Population({w},0); R.PopZero({w}); assert c as int==0; }}',
                     f'method One{w}() {{ var c:=N{w}.Count(1);',
                     f' B{w}.RoundTrip(1); F.Population({w},1);',
                     f' R.PowerZero(); R.PopPower({w},0); assert c as int==1; }}',
                     f'method High{w}() {{ var c:=N{w}.Count({h});',
                     f' B{w}.RoundTrip({h}); F.Population({w},{h});',
                     f' B{w}.Power(); R.PowerSum({w-1},1); reveal R.P();',
                     f' R.PopPower({w},{w-1}); assert c as int==1; }}',
                     f'lemma Step{w}(n:nat,x:bv{w})',
                     ' requires n>0',
                     f' ensures R.popNat(n,x as int)==(x&1) as int+R.popNat(n-1,(x>>1) as int)',
                     f'{{ N{w}.Step(n,x); }}']
        negative += [f'lemma FalseStep{w}() {{ N{w}.Step(1,0);', ' assert false; // ERROR', '}',
                     f'method FalseCount{w}() {{ var c:=N{w}.Count(0);', ' assert false; // ERROR', '}',
                     f'method FalseTogether{w}() {{ N{w}.Step(1,0); var c:=N{w}.Count(0);', ' assert false; // ERROR', '}',
                     f'lemma ZeroStep{w}() {{ N{w}.Step(0,0); // ERROR', '}',
                     f'method WrongZero{w}() {{ var c:=N{w}.Count(0);',
                     f' F.Population({w},0); R.PopZero({w});', ' assert c as int==1; // ERROR', '}',
                     f'method WrongHigh{w}() {{ var c:=N{w}.Count({h});',
                     f' assert c as int==1 by {{ B{w}.RoundTrip({h}); F.Population({w},{h});',
                     f' B{w}.Power(); R.PowerSum({w-1},1); reveal R.P(); R.PopPower({w},{w-1}); }}',
                     ' assert c as int==0; // ERROR', '}',
                     f'method WrongBound{w}(x:bv{w}) {{ var c:=N{w}.Count(x);',
                     f' assert c as int=={w+1}; // ERROR', '}',
                     f'method BadUpper{w}() {{ var c:=N{w}.Count({m} as bv{w}); // ERROR', '}',
                     f'method BadLower{w}() {{ var c:=N{w}.Count((-1) as bv{w}); // ERROR', '}',
                     f'lemma WrongStep{w}() {{ N{w}.Step(1,1);',
                     ' reveal R.popNat(); assert R.popNat(1,1)==R.popNat(0,0); // ERROR', '}']
    return positive, negative


def runtime_fixture():
    lines=[f'include "{LIBRARY.as_posix()}"']
    for w in WIDTHS:
        lines += [f'import N{w}=Std.Arithmetic.IntegerBitwiseNativePopulation{w}']
    helpers=[]
    main=['method Main() {']
    expected=[]
    for w in WIDTHS:
        rng=random.Random(w)
        values=set(range(256)) if w==8 else {0, 1, 2, 3, 2**w-1, 2**(w-1), 2**(w-1)-1,
                                             sum(1<<i for i in range(0,w,2)), sum(1<<i for i in range(1,w,2))}
        if w>8:
            values.update(1<<i for i in range(w))
            values.update((2**w-1)^(1<<i) for i in range(w))
            values.update(rng.getrandbits(w) for _ in range(32))
        values=sorted(values)
        helpers += [f'method Test{w}(xs:seq<bv{w}>) {{',
                    f' for j:=0 to |xs| {{ var c:=N{w}.Count(xs[j]);',
                    f'  print "{w}:",xs[j],":",c,"\\n";', ' }', '}']
        main += [f' var xs{w}:seq<bv{w}>:=['+','.join(str(v) for v in values)+'];',f' Test{w}(xs{w});']
        expected += [str(w)+':'+str(v)+':'+str(bin(v).count('1')) for v in values]
    main += ['}']
    return lines+helpers+main, expected


def invoke(command, timeout):
    started = time.monotonic()
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               text=True, start_new_session=True)
    killed = False
    try:
        stdout, stderr = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        killed = True
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

    def verify(name, path, options=(), errors=(), mutation=False, limit=2000000,
               timeout=110, expected_scopes=None):
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
            assert re.search(r'postcondition could not be proved|precondition.*could not be proved|value to be converted might not fit|invariant could not be proved|might not decrease|value does not satisfy|must be non-negative', text), (name, text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        assert (failed>=1 if mutation else failed==len(errors)), (name,failed,errors)
        if expected_scopes is not None:
            assert len(rows) == expected_scopes, (name, len(rows), expected_scopes)
        return dict(name=name, exit=code, outcome='failed' if failed else 'passed',
                    scopes=len(rows), failed=failed, elapsed=elapsed)

    modes = ('legacy', 'refreshed') if args.resolver=='both' else (args.resolver,)
    if not args.mutations_only:
        code, text, _, killed = invoke(exe+['verify', str(LIBRARY), *common, '--integer-bitwise:false'], 110)
        (out / 'option-off.log').write_text(text)
        assert not killed and code==2 and 'unresolved identifier: IntegerBitwiseModel' in text, text
        records.append(dict(name='option-off', exit=2, scopes=0, failed=0))
        for mode in modes:
            options = ['--type-system-refresh:'+str(mode=='refreshed').lower()]
            records.append(verify(mode+'-bodies', LIBRARY, [*options,'--verify-included-files'],expected_scopes=748))
            for kind, lines in zip(('clients', 'negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i, line in enumerate(lines, 1) if '// ERROR' in line}
                assert len(errors)==(40 if kind=='negative' else 0), (kind, errors)
                records.append(verify(mode+'-'+kind, path, options, errors, limit=200000 if errors else 2000000,expected_scopes=40))
        if args.runtime:
            lines, expected = runtime_fixture()
            path=out/'runtime.dfy'
            path.write_text('\n'.join(lines)+'\n')
            (out/'runtime.expected').write_text('\n'.join(expected)+'\n')
            for mode in modes:
                options=['--type-system-refresh:'+str(mode=='refreshed').lower()]
                records.append(verify(mode+'-runtime-input',path,options,expected_scopes=5))
                for target in ('cs','js'):
                    name=mode+'-runtime-'+target
                    code,text,elapsed,killed=invoke(exe+['run',str(path),*common,*options,
                        '--no-verify','--target',target,'--output',str(out/name)],110)
                    (out/(name+'.log')).write_text(text)
                    assert not killed and code==0,(name,code,'see runtime log',killed)
                    actual=re.findall(r'^\d+:\d+:\d+$',text,re.M)
                    assert actual==expected,(name,'runtime output differs; see log',len(actual),len(expected))
                    records.append(dict(name=name,exit=code,cases=len(expected),elapsed=elapsed))
    else:
        original=LIBRARY.read_text().replace('include "IntegerBitwiseNative.dfy"',
                                           'include "'+DEPENDENCY.as_posix()+'"')
        sites=[]
        for w in WIDTHS:
            module=f'IntegerBitwiseNativePopulation{w}'
            start=original.index('module Std.Arithmetic.'+module)
            end=original.find('\nmodule ',start+1)
            end=end if end>=0 else len(original)
            section=original[start:end]
            headers=list(re.finditer(r'^  (?:lemma |method )(?:\{:[^}]+\}\s*)*(\w+)\(',section,re.M))
            for i,header in enumerate(headers):
                last=headers[i+1].start() if i+1<len(headers) else len(section)
                body=section[header.start():last]
                name=header[1]
                for j,contract in enumerate(re.finditer(r'^    (requires|ensures) (.+)$',body,re.M)):
                    offset=start+header.start()+contract.start()
                    stop=start+header.start()+contract.end()
                    replacement='    ensures !('+contract[2]+')' if contract[1]=='ensures' else ''
                    mutant=original[:offset]+replacement+original[stop:]
                    baseline=original
                    options=['--filter-symbol',f'{module}.{name}.']
                    if contract[1]=='ensures':
                        at=original.index('\n',start+header.start())
                        guard='\n    requires '+('n==1 && x==0' if name=='Step' else 'x==0')
                        baseline=baseline[:at]+guard+baseline[at:]
                        mutant=mutant[:at]+guard+mutant[at:]
                    else:
                        options += ['--boogie','/proc:*well-formedness*']
                    sites.append((f'{module}.{name}.{contract[1]}{j}',baseline,mutant,options))
            header=next(h for h in headers if h[1]=='Count')
            at=original.index('\n',start+header.start())
            for name,old,new,x in [
                ('initial-rest','var rest:=x;','var rest:=x>>1;',1),
                ('initial-count','c:=0;','c:=1;',0),
                ('low-bit-test','if rest&1==1','if rest&1==0',0),
                ('count-increment','c:=c+1;','c:=c+0;',1),
                ('word-shift','rest:=rest>>1;','rest:=rest>>2;',3),
                ('counter-increment','i:=i+1;','i:=i+2;',2**(w-1)),
                ('counter-progress','i:=i+1;','i:=i+0;',0)]:
                baseline=original[:at]+f'\n    requires x=={x}'+original[at:]
                adjusted_end=end+len(f'\n    requires x=={x}')
                region=baseline[start:adjusted_end]
                assert region.count(old)==1,(w,name,old)
                mutant=baseline[:start]+region.replace(old,new)+baseline[adjusted_end:]
                sites.append((f'{module}.Count.{name}',baseline,mutant,
                              ['--filter-symbol',f'{module}.Count.']))
        assert len(sites)==48,len(sites)
        for mode in modes:
            for site,baseline,mutant,options in sites:
                options=['--type-system-refresh:'+str(mode=='refreshed').lower(),*options]
                path=out/'baseline.dfy'; path.write_text(baseline)
                clean=verify(mode+'.'+site+'.baseline',path,options)
                path=out/'mutation.dfy'; path.write_text(mutant)
                result=verify(mode+'.'+site,path,options,mutation=True,timeout=max(5,3*clean['elapsed']))
                result['baseline']=clean
                records.append(result)
        assert len(records)==48*len(modes)
    (out / 'summary.json').write_text(json.dumps(records, indent=2)+'\n')
    print('Native population count checks passed')


if __name__ == '__main__':
    main()
