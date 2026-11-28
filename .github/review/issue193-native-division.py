#!/usr/bin/env python3
"""Check native unsigned division/remainder bodies, clients and live controls."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess
import os
import signal
import time

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeDivision.dfy'
WIDTHS = (8, 16, 32, 64)
OPERATIONS = (('Divide', '/'), ('Remainder', '%'))


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"']
    positive, negative = prelude.copy(), prelude.copy()
    for lines in (positive, negative):
        lines += ['lemma {:induction false} IntegerCoherence(a:int,d:int)',
                  ' requires 0<=a && 0<d',
                  ' ensures a==(a/d)*d+a%d && 0<=a%d<d', '{}']
    for w in WIDTHS:
        m = 2**w
        for lines in (positive, negative):
            lines += [f'import N{w} = Std.Arithmetic.IntegerBitwiseNativeDivision{w}',
                      f'import B{w} = Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype U{w} = a:nat | a<{m}']
        for name, op in OPERATIONS:
            positive += [f'lemma {{:induction false}} Symbolic{name}{w}(x:bv{w},y:bv{w})',
                         ' requires y!=0',
                         f' ensures (x{op}y) as int==(x as int){op}(y as int)',
                         f'{{ N{w}.{name}(x,y); }}']
        positive += [f'lemma {{:induction false}} {{:isolate_assertions}} Composed{w}(a:U{w},b:U{w})',
                     ' requires b!=0', f' ensures b as bv{w}!=0',
                     f' ensures ((a as bv{w})/(b as bv{w})) as int==(a as int)/(b as int)',
                     f' ensures ((a as bv{w})%(b as bv{w})) as int==(a as int)%(b as int)',
                     '{', f' B{w}.RoundTrip(a as int); B{w}.RoundTrip(b as int);',
                     f' assert b as bv{w}!=0;',
                     f' N{w}.Divide(a as bv{w},b as bv{w}); N{w}.Remainder(a as bv{w},b as bv{w});',
                     f' ghost var q:U{w}:=(((a as bv{w})/(b as bv{w})) as int) as U{w};',
                     f' ghost var r:U{w}:=(((a as bv{w})%(b as bv{w})) as int) as U{w};',
                     ' assert q as int==(a as int)/(b as int);',
                     ' assert r as int==(a as int)%(b as int);', '}',
                     f'lemma {{:induction false}} {{:isolate_assertions}} Coherence{w}(x:bv{w},y:bv{w})',
                     ' requires y!=0',
                     f' ensures x as int==((x/y) as int)*(y as int)+(x%y) as int',
                     f' ensures 0<=(x%y) as int<y as int',
                     f'{{ N{w}.Divide(x,y); N{w}.Remainder(x,y);',
                     *([' B64.Zero64(y);'] if w==64 else []),
                     ' IntegerCoherence(x as int,y as int);',
                     '}',
                     f'lemma {{:isolate_assertions}} Boundaries{w}() {{']
        for x, y in ((13,5), (5,13), (6,3), (0,m-1), (m-1,1), (m-1,m-1),
                     (1,m-1), (m-1,2), (m-1,m//2+1), (m//2,2)):
            positive += [f' assert (({x} as bv{w})/({y} as bv{w})) as int=={x//y} by {{ N{w}.Divide({x},{y}); }}',
                         f' assert (({x} as bv{w})%({y} as bv{w})) as int=={x%y} by {{ N{w}.Remainder({x},{y}); }}']
        positive += ['}']
        negative += [f'newtype Positive{w} = n:nat | 0<n<{m} witness 1']
        for name, op in OPERATIONS:
            negative += [f'lemma False{name}{w}() {{ N{w}.{name}(13,5);',
                         ' assert false; // ERROR', '}',
                         f'lemma Zero{name}{w}() {{ N{w}.{name}(13,0); // ERROR', '}',
                         f'lemma RawZero{name}{w}() {{ var q:=(13 as bv{w}){op}(0 as bv{w}); // ERROR', '}',
                         f'lemma Strict{name}{w}() {{ N{w}.{name}(1,{2 if op=="/" else 1});',
                         f' var q:Positive{w}:=(((1 as bv{w}){op}{2 if op=="/" else 1}) as int) as Positive{w}; // ERROR', '}']
        negative += [f'lemma CombinedFalse{w}() {{ IntegerCoherence(0,1); N{w}.Divide(0,1); N{w}.Remainder(0,1);',
                     ' assert false; // ERROR', '}',
                     f'lemma WrongQuotient{w}() {{ N{w}.Divide(13,5);',
                     f' assert ((13 as bv{w})/5) as int==3; // ERROR', '}',
                     f'lemma WrongRemainder{w}() {{ N{w}.Remainder(13,5);',
                     f' assert ((13 as bv{w})%5) as int==2; // ERROR', '}',
                     f'lemma WrongBound{w}() {{ N{w}.Remainder(13,5);',
                     f' assert ((13 as bv{w})%5) as int>=5; // ERROR', '}',
                     f'lemma WrongReconstruction{w}() {{ N{w}.Divide(13,5); N{w}.Remainder(13,5);',
                     f' assert 13==(((13 as bv{w})/5) as int)*5; // ERROR', '}',
                     f'lemma Signed{w}() {{ N{w}.Divide({m-1},2);',
                     f' assert (({m-1} as bv{w})/2) as int==-1; // ERROR', '}',
                     f'lemma BadBound{w}() {{ B{w}.RoundTrip({m}); // ERROR', '}',
                     f'lemma BadNegative{w}() {{ B{w}.RoundTrip(-1); // ERROR', '}',
                     f'lemma BadCast{w}() {{ var x:={m} as bv{w}; // ERROR', '}']
    for index,(module,header,_,_) in enumerate(declarations(LIBRARY.read_text())):
        alias = 'C'+str(index)
        name = header[1]
        parameters = [x.split(':',1)[0].strip() for x in header[2].split(',')]
        values = witness(name)
        arguments = ','.join(str(values[key]) for key in parameters)
        for lines in (positive,negative):
            lines.append('import '+alias+'='+module)
        setup = f'reveal {alias}.Q(),{alias}.R(); ' if name=='StepWitness' else ''
        positive += [f'lemma {{:induction false}} Witness{index}() {{ {setup}{alias}.{name}({arguments}); }}']
        negative += [f'lemma {{:induction false}} FalseHelper{index}() {{ {setup}{alias}.{name}({arguments});',
                     ' assert false; // ERROR', '}']
    return positive, negative


def kill_group(pgid, sig):
    try:
        os.killpg(pgid,sig)
    except ProcessLookupError:
        pass
    except PermissionError:
        # Some platforms retain a group containing only zombies briefly.
        snapshot = subprocess.run(['ps','-axo','pgid=,stat='],capture_output=True,
                                  text=True,check=True,timeout=10)
        live = [row for line in snapshot.stdout.splitlines()
                if len(row:=line.split())==2 and int(row[0])==pgid
                and not row[1].startswith('Z')]
        if live:
            raise


def invoke(command, timeout):
    started = time.monotonic()
    def interrupted(signum, frame):
        raise SystemExit(128+signum)
    previous = {sig:signal.signal(sig,interrupted) for sig in (signal.SIGTERM,signal.SIGINT)}
    child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                             text=True, start_new_session=True)
    killed = False
    try:
        stdout, stderr = child.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        killed = True
        kill_group(child.pid, signal.SIGTERM)
        try:
            stdout, stderr = child.communicate(timeout=5)
        except subprocess.TimeoutExpired:
            kill_group(child.pid, signal.SIGKILL)
            stdout, stderr = child.communicate()
    finally:
        # Reap any solver/compiler children remaining in this run's group.
        try:
            kill_group(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        for sig,handler in previous.items():
            signal.signal(sig,handler)
    return child.returncode, stdout + stderr, time.monotonic()-started, killed


def declarations(source):
    headers = list(re.finditer(r'^ +lemma (?:\{:[^}]+\}\s*)*(\w+)\((.*)\)', source, re.M))
    result = []
    for header in headers:
        module = re.findall(r'^module (\S+) \{', source[:header.start()], re.M)[-1]
        body = re.search(r'^ +\{', source[header.end():], re.M)
        assert body, header[1]
        contract_end = header.end()+body.start()
        contracts = list(re.finditer(r'^ +ensures (.+)$', source[header.end():contract_end], re.M))
        assert contracts, header[1]
        result.append((module, header, contract_end, contracts))
    return result


def witness(name):
    if name=='Zero': return dict(a=0,d=5,q=0,r=0,y=5)
    if name=='Exact': return dict(a=13,b=5,m=256,r=18)
    if name=='NonNegative': return dict(a=13,b=5)
    if name=='Bounds': return dict(a=13,d=5,q=2,r=3)
    if name=='Double': return dict(a=27,d=5,q=2,r=3)
    if name=='Finish': return dict(a=27,d=5,q=2,r=3,t=7,v=5,z=2)
    if name.startswith('Leading'): return dict(x=13,d=0)
    if name.startswith('Dispatch'): return dict(x=13,y=1)
    return dict(x=13,y=5,z=18 if name=='ExactAdd' else 8,q=1,r=1,t=3,u=2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', type=Path, required=True)
    parser.add_argument('--z3', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--mutations-only', action='store_true')
    parser.add_argument('--resolver', choices=('legacy','refreshed','both'), default='both')
    parser.add_argument('--additional-axioms', action='store_true')
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

    def verify(name, path, options=(), errors=(), mutation=False, wellformed=False, limit=2000000, body_search=False, expected_scopes=None, timeout=300):
        csv_path = out / (name + '.csv')
        search_common = common
        if body_search:
            # Use Dafny's normal incremental prover for the many isolated native
            # scopes. Dependencies and client controls retain their fresh prover.
            restart = common.index('/restartProver')
            search_common = common[:restart-1]+common[restart+1:]
        code, text, elapsed, killed = invoke(
            exe + ['verify', str(path), *search_common,
                   *(['--solver-option','O:smt.case_split=1'] if body_search else []),
                   '--resource-limit', str(limit), '--log-format',
                   'csv;LogFileName='+str(csv_path), *options], timeout)
        (out / (name+'.log')).write_text(text)
        if mutation and killed:
            records.append(dict(name=name, exit=code, outcome='killed (timeout)', elapsed=elapsed, timeout=timeout))
            return
        assert not killed, (name,elapsed,timeout)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name,text)
        assert code == (4 if errors or mutation or wellformed else 0), (name,code,text)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:',text)}
        if mutation:
            assert 'Error:' in text, (name,text)
        elif not wellformed:
            assert actual == set(errors), (name,actual,errors,text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed','Failed') for r in rows), (name,rows)
        if wellformed:
            assert 'possible division by zero' in text, (name,text)
            assert any('well-formedness' in r['TestResult.DisplayName'] and r['TestResult.Outcome']=='Failed' for r in rows), (name,rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        if mutation:
            assert failed>0,(name,rows)
        if expected_scopes is not None:
            assert len(rows)==expected_scopes, (name,len(rows),expected_scopes)
        if not mutation and not wellformed:
            assert failed==len(errors), (name,failed,len(errors))
        records.append(dict(name=name,exit=code,scopes=len(rows),failed=failed,elapsed=elapsed))

    if not args.mutations_only:
        disabled = subprocess.run(exe+['verify',str(LIBRARY),*common,'--integer-bitwise:false'],
                                  capture_output=True,text=True,timeout=110)
        disabled_text = disabled.stdout+disabled.stderr
        (out/'option-off.log').write_text(disabled_text)
        assert disabled.returncode==2 and 'unresolved identifier: IntegerBitwiseModel' in disabled_text, disabled_text
        records.append(dict(name='option-off',exit=2,scopes=0,failed=0))
        modes = ('legacy','refreshed') if args.resolver == 'both' else (args.resolver,)
        for mode in modes:
            options = ['--type-system-refresh:'+str(mode=='refreshed').lower()]
            # Qualify dependencies under their existing search; new bodies use
            # the default bitvector engine with its native case split.
            verify(mode+'-included-bodies', LIBRARY.parent/'IntegerBitwiseNativeShifts.dfy',
                   [*options,'--verify-included-files'])
            verify(mode+'-bodies', LIBRARY, options, body_search=True)
            body_rows = list(csv.DictReader((out/(mode+'-bodies.csv')).open(encoding='utf-8-sig')))
            checked = {r['TestResult.DisplayName'].split(' (',1)[0] for r in body_rows}
            for module,header,_,_ in declarations(LIBRARY.read_text()):
                assert module+'.'+header[1] in checked, (mode,module,header[1])
            for kind, lines in zip(('clients','negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i,line in enumerate(lines,1) if '// ERROR' in line}
                verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000)
                if errors:
                    verify(mode+'-negative-bodies',path,options,errors,limit=200000,body_search=True)
    else:
        original = LIBRARY.read_text().replace('include "IntegerBitwiseNativeShifts.dfy"',
                     'include "'+(LIBRARY.parent/'IntegerBitwiseNativeShifts.dfy').as_posix()+'"')
        # One clean body baseline fixes the per-mutant wall limit at three times
        # its duration. Mutants run serially, each with its own process group.
        verify('mutation-baseline', LIBRARY, ['--type-system-refresh:true'], body_search=True)
        clean_elapsed = records[-1]['elapsed']
        mutant_timeout = max(3.0,3*clean_elapsed)
        seen = set()
        for module, header, end, contracts in declarations(original):
            name = header[1]
            values = witness(name)
            parameters = [x.split(':',1)[0].strip() for x in header[2].split(',')]
            inhabited = ' && '.join(f'{key}=={values[key]}' for key in parameters)
            contract = contracts[0]
            start, finish = header.end()+contract.start(), header.end()+contract.end()
            mutated = original[:start]+'  ensures !('+contract[1]+')'+original[finish:]
            if name!='Observe':
                # Keep a recursive lemma's domain unchanged: restricting it to
                # a witness would itself invalidate its recursive call.
                at = mutated.index('\n',header.start())
                mutated = mutated[:at]+'\n  requires '+inhabited+mutated[at:]
            path = out/'mutation.dfy'
            path.write_text(mutated)
            site = module+'.'+name
            assert site not in seen
            seen.add(site)
            verify(site,path,['--type-system-refresh:true','--filter-symbol',site+'.'],
                   mutation=True,body_search=True,timeout=mutant_timeout)
        assert len(seen)==290, len(seen)
        for w in WIDTHS:
            module = f'Std.Arithmetic.IntegerBitwiseNativeDivision{w}'
            for function,operation in (('Q','/'),('R','%')):
                pattern = rf'(opaque function {function}\(x:bv{w},y:bv{w}\):bv{w}\n +)\{{ if y==0 then 0 else x{re.escape(operation)}y \}}'
                mutated,count = re.subn(pattern,rf'\1{{ if y==0 then 0 else x{operation}y+1 }}',original)
                assert count==1,(w,function,count)
                path=out/'mutation.dfy';path.write_text(mutated)
                verify(f'{module}.{function}.definition',path,
                       ['--type-system-refresh:true','--filter-symbol',module+'.Zero.'],
                       mutation=True,body_search=True,timeout=mutant_timeout)
            for name in ('Divide','Remainder'):
                pattern = rf'(lemma (?:\{{:[^}}]+\}}\s*)*{name}\(x:bv{w},y:bv{w}\)\n) +requires y!=0\n'
                mutated, count = re.subn(pattern,r'\1',original)
                assert count==1,(w,name,count)
                path=out/'mutation.dfy';path.write_text(mutated)
                verify(f'{module}.{name}.guard',path,
                       ['--type-system-refresh:true','--filter-symbol',module+'.'+name+'.',
                        '--boogie','/proc:*well-formedness*'],wellformed=True,
                       body_search=True,timeout=mutant_timeout)
        (out/'mutation-baseline.json').write_text(json.dumps(dict(cleanSeconds=clean_elapsed,
                                                               mutantTimeout=mutant_timeout),indent=2)+'\n')
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native division checks passed')


if __name__ == '__main__':
    main()
