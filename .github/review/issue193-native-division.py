#!/usr/bin/env python3
"""Check native unsigned division/remainder bodies, clients and live controls."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

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

    def verify(name, path, options=(), errors=(), mutation=False, wellformed=False, limit=2000000, intblast=False, expected_scopes=None):
        csv_path = out / (name + '.csv')
        result = subprocess.run(exe + ['verify', str(path), *common, *(['--solver-option','O:smt.bv.solver=2'] if intblast else []), '--resource-limit', str(limit),
                                      '--log-format', 'csv;LogFileName='+str(csv_path), *options],
                                capture_output=True, text=True, timeout=110)
        text = result.stdout + result.stderr
        (out / (name+'.log')).write_text(text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name,text)
        assert result.returncode == (4 if errors or mutation or wellformed else 0), (name,result.returncode,text)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:',text)}
        if mutation:
            assert 'a postcondition could not be proved' in text, (name,text)
        elif not wellformed:
            assert actual == set(errors), (name,actual,errors,text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed','Failed') for r in rows), (name,rows)
        if wellformed:
            assert 'possible division by zero' in text, (name,text)
            assert any('well-formedness' in r['TestResult.DisplayName'] and r['TestResult.Outcome']=='Failed' for r in rows), (name,rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        if expected_scopes is not None:
            assert len(rows)==expected_scopes, (name,len(rows),expected_scopes)
        if not mutation and not wellformed:
            assert failed==len(errors), (name,failed,len(errors))
        records.append(dict(name=name,exit=result.returncode,scopes=len(rows),failed=failed))

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
            # The existing Boolean/conversion closure uses the default BV engine.
            # Only the new division/remainder bodies use the integer-based BV engine.
            verify(mode+'-included-bodies', LIBRARY.parent/'IntegerBitwiseNative.dfy',
                   [*options,'--verify-included-files'])
            verify(mode+'-bodies', LIBRARY, options, intblast=True, expected_scopes=16)
            for kind, lines in zip(('clients','negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i,line in enumerate(lines,1) if '// ERROR' in line}
                verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000)
                if errors:
                    verify(mode+'-negative-intblast',path,options,errors,limit=200000,intblast=True)
    else:
        original = LIBRARY.read_text().replace('include "IntegerBitwiseNative.dfy"',
                     'include "'+(LIBRARY.parent/'IntegerBitwiseNative.dfy').as_posix()+'"')
        headers = list(re.finditer(r'^  lemma (?:\{:[^}]+\}\s*)*(\w+)\((.*)\)',original,re.M))
        for i, header in enumerate(headers):
            module = re.findall(r'module Std.Arithmetic.(\w+) \{',original[:header.start()])[-1]
            name = header[1]
            end = headers[i+1].start() if i+1<len(headers) else len(original)
            section = original[header.end():end]
            for contract in re.finditer(r'^    (ensures|requires) (.+)$',section,re.M):
                start, finish = header.end()+contract.start(), header.end()+contract.end()
                fact = contract[1]=='ensures'
                replacement = '    ensures !('+contract[2]+')' if fact else ''
                mutated = original[:start]+replacement+original[finish:]
                if fact:
                    # This concrete nonzero divisor inhabits the contract.
                    at = mutated.index('\n',header.start())
                    mutated = mutated[:at]+'\n    requires x==13 && y==5'+mutated[at:]
                path = out / 'mutation.dfy'
                path.write_text(mutated)
                site = f'{module}.{name}.{contract[1]}'
                options = ['--type-system-refresh:true','--filter-symbol',f'{module}.{name}.']
                if fact:
                    verify(site,path,options,mutation=True,intblast=True)
                else:
                    # Removing the guard makes the contract's division undefined.
                    # Check that boundary without exploring the ill-formed body.
                    verify(site,path,[*options,'--boogie','/proc:*well-formedness*'],
                           wellformed=True,intblast=True)
        assert len(records)==16
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native division checks passed')


if __name__ == '__main__':
    main()
