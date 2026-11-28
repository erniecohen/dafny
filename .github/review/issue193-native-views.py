#!/usr/bin/env python3
"""Check native bit-sequence views, live controls and serial mutations."""
import argparse
import csv
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeViews.dfy'
WIDTHS = (8, 16, 32, 64)


def fixtures():
    positive, negative = [], []
    for lines in (positive, negative):
        lines += [f'include "{LIBRARY.as_posix()}"',
                  'import R=Std.Arithmetic.IntegerBitwise']
    for w in WIDTHS:
        for lines in (positive, negative):
            lines += [f'import V{w}=Std.Arithmetic.IntegerBitwiseNativeViews{w}',
                      f'import B{w}=Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype U{w}=a:int | 0<=a<{2**w}',
                     f'lemma Native{w}(x:bv{w},y:bv{w},i:nat,k:nat)',
                     f' requires i<{w} && k<={w}', '{',
                     f' V{w}.Observe(x); V{w}.WordRoundTrip(x); V{w}.Equality(x,y);',
                     f' V{w}.Digit(x,i); V{w}.Boolean(x,y,i); V{w}.Shifts(x,k,i);',
                     f' assert R.value(V{w}.Bits(x))==x as int;',
                     f' assert V{w}.Word(V{w}.Bits(x))==x;',
                     f' assert (x==y)==(V{w}.Bits(x)==V{w}.Bits(y));',
                     f' assert V{w}.Bits(x)[i]==((x as int/R.P(i))%2==1);',
                     f' assert V{w}.Bits(x&y)[i]==(V{w}.Bits(x)[i] && V{w}.Bits(y)[i]);',
                     f' assert V{w}.Bits(!x)[i]==!V{w}.Bits(x)[i];',
                     f' assert V{w}.Bits(x|y)[i]==(V{w}.Bits(x)[i] || V{w}.Bits(y)[i]);',
                     f' assert V{w}.Bits(x^y)[i]==(V{w}.Bits(x)[i] != V{w}.Bits(y)[i]);',
                     f' assert V{w}.Bits(x&!y)[i]==(V{w}.Bits(x)[i] && !V{w}.Bits(y)[i]);',
                     f' assert V{w}.Bits(x<<k)[i]==(i>=k && V{w}.Bits(x)[i-k]);',
                     f' assert V{w}.Bits(x>>k)[i]==(i+k<{w} && V{w}.Bits(x)[i+k]);', '}',
                     f'lemma Sequence{w}(s:seq<bool>) requires |s|=={w}',
                     f' ensures V{w}.Bits(V{w}.Word(s))==s',
                     f' ensures V{w}.Word(s) as int==R.value(s)',
                     f'{{ V{w}.Build(s); V{w}.SequenceRoundTrip(s); }}',
                     f'lemma Composed{w}(a:U{w})',
                     f' ensures R.value(V{w}.Bits(a as bv{w}))==a as int',
                     f'{{ B{w}.RoundTrip(a as int); V{w}.Observe(a as bv{w}); }}',
                     f'lemma Concrete{w}() {{',
                     f' V{w}.Digit(0,0); V{w}.Digit(1,0); V{w}.Digit(2,0);',
                     f' V{w}.Digit({2**w-1},0); R.PowerZero();',
                     f' assert !V{w}.Bits(0)[0] && V{w}.Bits(1)[0];',
                     f' assert !V{w}.Bits(2)[0] && V{w}.Bits({2**w-1})[0];',
                     f' V{w}.Shifts(1,{w-1},{w-1});',
                     f' assert V{w}.Bits({2**(w-1)})[{w-1}];',
                     f' V{w}.Shifts({2**w-1},0,0); V{w}.Shifts({2**w-1},{w},0);',
                     f' assert V{w}.Bits(({2**w-1} as bv{w})<<0)[0];',
                     f' assert V{w}.Bits(({2**w-1} as bv{w})>>0)[0];',
                     f' assert !V{w}.Bits(({2**w-1} as bv{w})<<{w})[0];',
                     f' assert !V{w}.Bits(({2**w-1} as bv{w})>>{w})[0];',
                     f' V{w}.WordRoundTrip({2**(w-1)}); V{w}.WordRoundTrip({2**w-1});',
                     f' var s:=seq({w},i=>i%2==0); V{w}.Build(s); V{w}.SequenceRoundTrip(s);',
                     f' assert V{w}.Bits(V{w}.Word(s))==s;', '}']
        calls={'Observe':'1', 'Build':f'V{w}.Bits(1)', 'WordRoundTrip':'1',
               'SequenceRoundTrip':f'V{w}.Bits(1)', 'Equality':'1,2',
               'Digit':'1,0', 'Boolean':'1,2,0', 'Shifts':'1,1,0'}
        for name,args in calls.items():
            negative += [f'lemma False{name}{w}() {{ V{w}.{name}({args});',
                         ' assert false; // ERROR','}']
        negative += [f'lemma FalseFunctions{w}() {{',
                     f' var s:=V{w}.Bits(1); var x:=V{w}.Word(s);',
                     ' assert false; // ERROR','}',
                     f'lemma FalseTogether{w}() {{']
        negative += [f' V{w}.{name}({args});' for name,args in calls.items()]
        negative += [' assert false; // ERROR','}']
        bad={'WordShort':f'var x:=V{w}.Word([])',
             'WordLong':f'var x:=V{w}.Word(seq({w+1},i=>false))',
             'BuildShort':f'V{w}.Build([])',
             'SequenceLong':f'V{w}.SequenceRoundTrip(seq({w+1},i=>false))',
             'DigitIndex':f'V{w}.Digit(1,{w})',
             'DigitNegative':f'V{w}.Digit(1,-1)',
             'BooleanIndex':f'V{w}.Boolean(1,2,{w})',
             'ShiftCount':f'V{w}.Shifts(1,{w+1},0)',
             'ShiftIndex':f'V{w}.Shifts(1,1,{w})',
             'ShiftNegative':f'V{w}.Shifts(1,-1,0)',
             'BadCast':f'var x:=V{w}.Bits({2**w} as bv{w})'}
        for name,call in bad.items():
            negative += [f'lemma {name}{w}() {{ {call}; // ERROR','}']
        negative += [f'lemma WrongBitOrder{w}() {{ V{w}.Digit(1,0); R.PowerZero();',
                     f' assert !V{w}.Bits(1)[0]; // ERROR','}',
                     f'lemma WrongValue{w}() {{ V{w}.Observe(1);',
                     f' assert R.value(V{w}.Bits(1))==2; // ERROR','}',
                     f'lemma WrongInverse{w}() {{ V{w}.WordRoundTrip(1);',
                     f' assert V{w}.Word(V{w}.Bits(1))==0; // ERROR','}',
                     f'lemma WrongEquality{w}() {{ V{w}.Equality(1,2);',
                     f' assert V{w}.Bits(1)==V{w}.Bits(2); // ERROR','}',
                     f'lemma WrongBoolean{w}() {{ V{w}.Boolean(1,0,0); V{w}.Digit(1,0); R.PowerZero();',
                     f' assert V{w}.Bits(1&0)[0]; // ERROR','}',
                     f'lemma WrongShift{w}() {{ V{w}.Shifts(1,{w},0);',
                     f' assert V{w}.Bits((1 as bv{w})<<{w})[0]; // ERROR','}']
    return positive,negative


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
            assert re.search(r'postcondition could not be proved|precondition.*could not be proved|value to be converted might not fit|invariant could not be proved|might not decrease|value does not satisfy|must be non-negative|index out of range', text), (name, text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        assert (failed>=1 if mutation else failed==len(errors)), (name,failed,errors)
        if expected_scopes is not None:
            assert len(rows) == expected_scopes, (name, len(rows), expected_scopes)
        return dict(name=name, exit=code, outcome='failed' if failed else 'passed',
                    scopes=len(rows), failed=failed, elapsed=elapsed)

    modes = ('legacy','refreshed') if args.resolver=='both' else (args.resolver,)
    if not args.mutations_only:
        code,text,_,killed=invoke(exe+['verify',str(LIBRARY),*common,'--integer-bitwise:false'],110)
        (out/'option-off.log').write_text(text)
        assert not killed and code==2 and 'unresolved identifier: IntegerBitwiseModel' in text,text
        records.append(dict(name='option-off',exit=2,scopes=0,failed=0))
        for mode in modes:
            options=['--type-system-refresh:'+str(mode=='refreshed').lower()]
            records.append(verify(mode+'-bodies',LIBRARY,[*options,'--verify-included-files'],expected_scopes=1396))
            for kind,lines in zip(('clients','negative'),fixtures()):
                path=out/(mode+'-'+kind+'.dfy');path.write_text('\n'.join(lines)+'\n')
                errors={i for i,line in enumerate(lines,1) if '// ERROR' in line}
                assert len(errors)==(108 if kind=='negative' else 0)
                records.append(verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000,expected_scopes=108 if errors else 28))
    else:
        original=LIBRARY.read_text().replace('include "IntegerBitwiseNativeShifts.dfy"',
                   'include "'+LIBRARY.with_name('IntegerBitwiseNativeShifts.dfy').as_posix()+'"')
        sites=[]
        for w in WIDTHS:
            module=f'IntegerBitwiseNativeViews{w}'
            start=original.index('module Std.Arithmetic.'+module)
            end=original.find('\nmodule ',start+1)
            end=end if end>=0 else len(original)
            section=original[start:end]
            headers=list(re.finditer(r'^  (?:lemma |opaque ghost function )(?:\{:[^}]+\}\s*)*(\w+)\(',section,re.M))
            for i,header in enumerate(headers):
                last=headers[i+1].start() if i+1<len(headers) else len(section)
                body=section[header.start():last]
                for j,contract in enumerate(re.finditer(r'^    (requires|ensures) (.+)$',body,re.M)):
                    at=start+header.start()+contract.start();stop=start+header.start()+contract.end()
                    replacement='    ensures !('+contract[2]+')' if contract[1]=='ensures' else ''
                    sites.append((f'{module}.{header[1]}.{contract[1]}{j}',original[:at]+replacement+original[stop:],
                                  ['--filter-symbol',f'{module}.{header[1]}.']))
            for name,old,new in [('bits-definition',f'R.bitsNat({w},x as int)',f'R.bitsNat({w},0)'),
                                 ('word-definition',f'R.value(s) as bv{w}',f'0 as bv{w}')]:
                assert section.count(old)==1,(w,name)
                mutant=original[:start]+section.replace(old,new)+original[end:]
                sites.append((f'{module}.{name}',mutant,['--filter-symbol',module]))
        assert len(sites)==92,len(sites)
        sites=[s for s in sites if args.mutation_filter in s[0]]
        assert sites
        for mode in modes:
            for site,mutant,options in sites:
                options=['--type-system-refresh:'+str(mode=='refreshed').lower(),*options]
                path=out/'baseline.dfy';path.write_text(original)
                clean=verify(mode+'.'+site+'.baseline',path,options)
                path=out/'mutation.dfy';path.write_text(mutant)
                result=verify(mode+'.'+site,path,options,mutation=True,timeout=max(5,3*clean['elapsed']))
                result['baseline']=clean
                records.append(result)
                (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native bit-sequence view checks passed')


if __name__=='__main__':
    main()
