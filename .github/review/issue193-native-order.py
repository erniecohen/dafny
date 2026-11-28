#!/usr/bin/env python3
"""Check native unsigned comparison bodies, clients and live controls."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeOrder.dfy'

# Body proofs, and the controls run beside them, replace Boogie's relevancy-based
# case split with Z3's default one, which completes the bit-blasted identities.
# This changes only the search order; the bitvector engine and every theory
# solver stay at their defaults (https://github.com/erniecohen/dafny/issues/618).
BODY_SEARCH = ['--solver-option', 'O:smt.case_split=1']
WIDTHS = (8, 16, 32, 64)
OPERATIONS = (('Equal', '=='), ('Less', '<'), ('LessEqual', '<='))


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"']
    positive, negative = prelude.copy(), prelude.copy()
    for w in WIDTHS:
        m = 2**w
        for lines in (positive, negative):
            lines += [f'import N{w} = Std.Arithmetic.IntegerBitwiseNativeOrder{w}',
                      f'import B{w} = Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype U{w} = a:nat | a<{m}']
        for name, op in OPERATIONS:
            positive += [f'lemma {{:induction false}} Symbolic{name}{w}(x:bv{w},y:bv{w})',
                         f' ensures (x{op}y)==((x as int){op}(y as int))',
                         f'{{ N{w}.{name}(x,y); }}']
        positive += [f'lemma {{:induction false}} {{:isolate_assertions}} Composed{w}(a:U{w},b:U{w})',
                     f' ensures (a as bv{w}==b as bv{w})==(a==b)',
                     f' ensures (a as bv{w}<b as bv{w})==(a<b)',
                     f' ensures (a as bv{w}<=b as bv{w})==(a<=b)',
                     '{', f' B{w}.RoundTrip(a as int); B{w}.RoundTrip(b as int);',
                     f' N{w}.Equal(a as bv{w},b as bv{w});',
                     f' N{w}.Less(a as bv{w},b as bv{w}); N{w}.LessEqual(a as bv{w},b as bv{w});', '}',
                     f'method {{:isolate_assertions}} SortPair{w}(a:U{w},b:U{w}) returns(lo:U{w},hi:U{w})',
                     ' ensures lo<=hi',
                     ' ensures (lo==a && hi==b) || (lo==b && hi==a)',
                     '{', f' Composed{w}(a,b);',
                     f' if a as bv{w}<=b as bv{w} {{ lo,hi:=a,b; }} else {{ lo,hi:=b,a; }}', '}',
                     f'lemma {{:isolate_assertions}} Boundaries{w}() {{']
        for x,y in ((0,0),(0,1),(1,0),(13,13),(13,5),(5,13),(m-1,0),
                    (0,m-1),(m-1,m-1),(m//2-1,m//2),(m//2,m//2-1),(m//2,m-1)):
            for name, op in OPERATIONS:
                value = {'==':x==y,'<':x<y,'<=':x<=y}[op]
                positive += [f' assert (({x} as bv{w}){op}({y} as bv{w}))=={str(value).lower()} by {{ N{w}.{name}({x},{y}); }}']
        positive += ['}']
        for name,op in OPERATIONS:
            y = 13 if name!='Less' else 5
            negative += [f'lemma False{name}{w}() {{ N{w}.{name}(13,{y});',
                         ' assert false; // ERROR', '}']
            x,y = (13,13) if name=='Less' else (13,5)
            negative += [f'lemma Wrong{name}{w}() {{ N{w}.{name}({x},{y});',
                         f' assert ({x} as bv{w}){op}({y} as bv{w}); // ERROR', '}']
        negative += [f'lemma CombinedFalse{w}() {{ N{w}.Equal(0,0); N{w}.Less(0,0); N{w}.LessEqual(0,0);',
                     ' assert false; // ERROR', '}',
                     f'lemma Signed{w}() {{ N{w}.Less({m//2},{m//2-1});',
                     f' assert ({m//2} as bv{w})<({m//2-1} as bv{w}); // ERROR', '}',
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

    def verify(name, path, options=(), errors=(), mutation=False, limit=2000000, bodies=False, expected_scopes=None):
        csv_path = out / (name + '.csv')
        result = subprocess.run(exe + ['verify', str(path), *common, *(BODY_SEARCH if bodies else []), '--resource-limit', str(limit),
                                      '--log-format', 'csv;LogFileName='+str(csv_path), *options],
                                capture_output=True, text=True, timeout=110)
        text = result.stdout + result.stderr
        (out / (name+'.log')).write_text(text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:|internal error|Unhandled exception', text, re.I), (name,text)
        assert result.returncode == (4 if errors or mutation else 0), (name,result.returncode,text)
        actual = {int(i) for i in re.findall(r'\((\d+),\d+\): Error:',text)}
        if mutation:
            assert 'a postcondition could not be proved' in text, (name,text)
        else:
            assert actual == set(errors), (name,actual,errors,text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed','Failed') for r in rows), (name,rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        if expected_scopes is not None:
            assert len(rows)==expected_scopes, (name,len(rows),expected_scopes)
        if not mutation:
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
            # The existing Boolean/conversion closure uses Boogie's search settings.
            # Only the new comparison bodies use BODY_SEARCH.
            verify(mode+'-included-bodies', LIBRARY.parent/'IntegerBitwiseNative.dfy',
                   [*options,'--verify-included-files'])
            verify(mode+'-bodies', LIBRARY, options, bodies=True, expected_scopes=118)
            for kind, lines in zip(('clients','negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i,line in enumerate(lines,1) if '// ERROR' in line}
                assert len(errors)==(44 if kind=='negative' else 0), (kind,errors)
                verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000)
                if errors:
                    verify(mode+'-negative-bodies',path,options,errors,limit=200000,bodies=True)
    else:
        original = LIBRARY.read_text().replace('include "IntegerBitwiseNative.dfy"',
                     'include "'+(LIBRARY.parent/'IntegerBitwiseNative.dfy').as_posix()+'"')
        headers = list(re.finditer(r'^  lemma (?:\{:[^}]+\}\s*)*(\w+)\((.*)\)',original,re.M))
        for i, header in enumerate(headers):
            module = re.findall(r'module Std.Arithmetic.(\w+) \{',original[:header.start()])[-1]
            name = header[1]
            end = headers[i+1].start() if i+1<len(headers) else len(original)
            section = original[header.end():end]
            contracts = list(re.finditer(r'^    (ensures|requires) (.+)$',section,re.M))
            assert contracts, (module,name)
            for j, contract in enumerate(contracts):
                start, finish = header.end()+contract.start(), header.end()+contract.end()
                fact = contract[1]=='ensures'
                mutated = original[:start]+('    ensures !('+contract[2]+')' if fact else '')+original[finish:]
                if fact:
                    # Concrete operands inhabit every contract, so a negated
                    # postcondition is refuted rather than vacuously proved. The
                    # recursive order lemma takes its base case, so an
                    # inconsistent induction hypothesis cannot obscure the rejection.
                    if module.endswith('Arithmetic'):
                        inhabited = 'a==5 && b==13 && ha==2 && hb==6 && la==1 && lb==1'
                    else:
                        inhabited = 'x==0 && y==13' if name=='Less' else 'x==5 && y==13'
                    at = mutated.index('\n',header.start())
                    mutated = mutated[:at]+'\n    requires '+inhabited+mutated[at:]
                # A dropped digit hypothesis leaves the body's own postcondition unproved.
                path = out / 'mutation.dfy'
                path.write_text(mutated)
                site = f'{module}.{name}.{contract[1]}.{j}'
                options = ['--type-system-refresh:true','--filter-symbol',f'{module}.{name}.']
                verify(site,path,options,mutation=True,bodies=True)
        assert len(records)==25
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native comparison checks passed')


if __name__ == '__main__':
    main()
