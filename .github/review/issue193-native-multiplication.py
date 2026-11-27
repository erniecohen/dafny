#!/usr/bin/env python3
"""Check modular multiplication bodies, clients, and live controls at four widths."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeMultiplication.dfy'
WIDTHS = (8, 16, 32, 64)


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"',
               'import A = Std.Arithmetic.IntegerBitwiseNativeMultiplicationArithmetic']
    positive, negative = prelude.copy(), prelude.copy()
    for w in WIDTHS:
        m = 2**w
        for lines in (positive, negative):
            lines += [f'import N{w} = Std.Arithmetic.IntegerBitwiseNativeMultiplication{w}',
                      f'import B{w} = Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype U{w} = a:int | 0<=a<{m}',
                     f'lemma {{:induction false}} Symbolic{w}(x:bv{w},y:bv{w})',
                     f' ensures (x*y) as int==(x as int*y as int)%{m}',
                     f'{{ N{w}.Times(x,y); }}',
                     f'lemma {{:induction false}} {{:isolate_assertions}} Composed{w}(a:U{w},b:U{w})',
                     f' ensures ((a as bv{w})*(b as bv{w})) as int==(a as int*b as int)%{m}',
                     f'{{ B{w}.RoundTrip(a as int); B{w}.RoundTrip(b as int);',
                     f' N{w}.Times(a as bv{w},b as bv{w}); }}',
                     f'lemma Boundaries{w}(x:bv{w}) {{',
                     f' N{w}.Times(x,0); assert (x*0) as int==0;',
                     f' N{w}.Times(x,1); assert (x*1) as int==x as int;']
        for x, y in ((5,3), (3,5), (0,m-1), (m-1,0), (m-1,2), (m-1,m-1), (m//2,2)):
            positive += [f' N{w}.Times({x},{y});',
                         f' assert (({x} as bv{w})*({y} as bv{w})) as int=={(x*y)%m};']
        positive += ['}', f'lemma Recurrence{w}(x:bv{w},y:bv{w})',
                     f' ensures x*y==x*(y-1)+x {{ N{w}.Step(x,y); }}']
        negative += [f'lemma FalseStep{w}() {{ N{w}.Step(3,5); assert false; // ERROR', '}',
                     f'lemma FalseWitness{w}() {{ N{w}.TimesWitness(3,5,15); assert false; // ERROR', '}',
                     f'lemma BadWitness{w}() {{ N{w}.TimesWitness(3,5,7); // ERROR', '}',
                     f'lemma False{w}() {{ N{w}.Times(3,5); assert false; // ERROR', '}',
                     f'lemma WrongWrap{w}() {{ N{w}.Times({m-1},2);',
                     f' assert (({m-1} as bv{w})*2) as int == {2*(m-1)}; // ERROR', '}',
                     f'lemma WrongResult{w}() {{ N{w}.Times(5,3);',
                     f' assert ((5 as bv{w})*3) as int == 14; // ERROR', '}',
                     f'lemma BadBound{w}() {{ B{w}.RoundTrip({m}); // ERROR', '}',
                     f'lemma BadNegative{w}() {{ B{w}.RoundTrip(-1); // ERROR', '}',
                     f'lemma BadCast{w}() {{ var x:= {m} as bv{w}; // ERROR', '}']
    positive += ['lemma Arithmetic() { A.Reduce(3,5,12,15,256); A.Reduce(3,0,253,0,256); A.Reduce(3,5,0,0,1); }']
    negative += ['lemma FalseArithmetic() { A.Reduce(3,5,12,15,256); assert false; // ERROR', '}',
                 'lemma BadPartial() { A.Reduce(3,5,7,10,256); // ERROR', '}',
                 'lemma BadResult() { A.Reduce(3,5,12,7,256); // ERROR', '}',
                 'lemma BadModulus() { A.Reduce(3,5,0,0,0); // ERROR', '}']
    return positive, negative


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

    def verify(name, path, options=(), errors=(), mutation=False, lost=False, wellformed=False, limit=2000000, intblast=False):
        csv_path = out / (name + '.csv')
        result = subprocess.run(exe + ['verify', str(path), *common, *(['--solver-option','O:smt.bv.solver=2'] if intblast else []), '--resource-limit', str(limit),
                                      '--log-format', 'csv;LogFileName='+str(csv_path), *options],
                                capture_output=True, text=True, timeout=110)
        text = result.stdout + result.stderr
        (out / (name+'.log')).write_text(text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name,text)
        assert result.returncode == (4 if errors or mutation or wellformed else 0), (name,result.returncode,text)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:',text)}
        if lost:
            assert actual == set(errors)-set(lost), (name,actual,errors,lost,text)
        elif mutation:
            assert 'could not be proved' in text, (name,text)
        elif not wellformed:
            assert actual == set(errors), (name,actual,errors,text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed','Failed') for r in rows), (name,rows)
        if wellformed:
            assert any('well-formedness' in r['TestResult.DisplayName'] and r['TestResult.Outcome']=='Failed' for r in rows), (name,rows)
        records.append(dict(name=name,exit=result.returncode,scopes=len(rows),
                            failed=sum(r['TestResult.Outcome']=='Failed' for r in rows)))

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
            # The existing bitwise/shift closure uses the default BV engine.
            # Only the new arithmetic bodies use the integer-based BV engine.
            verify(mode+'-included-bodies', LIBRARY.parent/'IntegerBitwiseNativeShifts.dfy',
                   [*options,'--verify-included-files'])
            verify(mode+'-bodies', LIBRARY, options, intblast=True)
            for kind, lines in zip(('clients','negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i,line in enumerate(lines,1) if '// ERROR' in line}
                verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000)
                if errors:
                    verify(mode+'-negative-intblast',path,options,errors,limit=200000,intblast=True)
    else:
        original = LIBRARY.read_text().replace('include "IntegerBitwiseNativeShifts.dfy"',
                     'include "'+(LIBRARY.parent/'IntegerBitwiseNativeShifts.dfy').as_posix()+'"')
        headers = list(re.finditer(r'^  lemma (?:\{:[^}]+\}\s*)*(\w+)\((.*)\)',original,re.M))
        for i, header in enumerate(headers):
            module = re.findall(r'module Std.Arithmetic.(\w+) \{',original[:header.start()])[-1]
            name = header[1]
            end = headers[i+1].start() if i+1<len(headers) else len(original)
            section = original[header.end():end]
            for j, contract in enumerate(re.finditer(r'^    (ensures|requires) (.+)$',section,re.M)):
                start, finish = header.end()+contract.start(), header.end()+contract.end()
                fact = contract[1]=='ensures'
                replacement = '    ensures !('+contract[2]+')' if fact else ''
                mutated = original[:start]+replacement+original[finish:]
                if fact:
                    if name=='Reduce':
                        inhabited = 'a==3 && b==5 && p==12 && r==15 && m==256'
                    else:
                        inhabited = 'x==3 && y==0 && r==0' if name=='TimesWitness' else 'x==3 && y==5'
                    at = mutated.index('\n',header.start())
                    mutated = mutated[:at]+'\n    requires '+inhabited+mutated[at:]
                path = out / 'mutation.dfy'
                path.write_text(mutated)
                site = f'{module}.{name}.{contract[1]}.{j}'
                options = ['--type-system-refresh:true','--filter-symbol',f'{module}.{name}.']
                if fact:
                    verify(site,path,options,mutation=True,intblast=True)
                elif name=='Reduce' and j==0:
                    verify(site,path,[*options,'--boogie','/proc:*well-formedness*'],
                           wellformed=True,intblast=True)
                else:
                    lines = fixtures()[1]
                    lines[0] = f'include "{path.as_posix()}"'
                    caller = out/'mutation-callers.dfy'
                    caller.write_text('\n'.join(lines)+'\n')
                    errors = {i for i,line in enumerate(lines,1) if '// ERROR' in line}
                    if name=='Reduce':
                        target = 'BadPartial' if j==1 else 'BadResult'
                    else:
                        target = 'BadWitness'+re.search(r'(\d+)$',module)[1]
                    at = next(k for k,line in enumerate(lines) if 'lemma '+target+'()' in line)
                    stop = next((k for k in range(at+1,len(lines)) if 'lemma ' in lines[k]),len(lines))
                    lost = {k+1 for k in range(at,stop) if '// ERROR' in lines[k]}
                    assert len(lost)==1, (site,lost)
                    verify(site,caller,['--type-system-refresh:true'],errors,lost=lost,limit=200000)
        assert len(records)==20
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native multiplication checks passed')


if __name__ == '__main__':
    main()
