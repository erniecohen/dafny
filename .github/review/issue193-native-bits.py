#!/usr/bin/env python3
"""Check native single-bit bodies, clients, live controls and serial mutations."""
import argparse
import csv
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeBits.dfy'
WIDTHS = (8, 16, 32, 64)
OPERATIONS = ('NativeBit','BitTest')


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"',
               'import C = Std.Arithmetic.IntegerBitwiseCompiler',
               'import R = Std.Arithmetic.IntegerBitwise']
    positive, negative = prelude.copy(), prelude.copy()
    for w in WIDTHS:
        m = 2**w
        for lines in (positive, negative):
            lines += [f'import N{w} = Std.Arithmetic.IntegerBitwiseNativeBits{w}',
                      f'import B{w} = Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype U{w} = a:nat | a<{m}',
                     f'lemma {{:induction false}} SymbolicNativeBit{w}(x:bv{w},i:nat)',
                     f' requires i<{w}',
                     ' ensures ((x>>i)&1) as int==((x as int)/R.P(i))%2',
                     f'{{ N{w}.NativeBit(x,i); }}',
                     f'lemma {{:induction false}} SymbolicBitTest{w}(x:bv{w},i:nat)',
                     f' requires i<{w}',
                     f' ensures (((x>>i)&1)==1)==C.bit({w},x as int,i)',
                     f'{{ N{w}.BitTest(x,i); }}',
                     f'lemma {{:induction false}} {{:isolate_assertions}} Composed{w}(a:U{w},i:nat)',
                     f' requires i<{w}',
                     f' ensures ((((a as bv{w})>>i)&1)==1)==C.bit({w},a as int,i)',
                     f' ensures (((a as bv{w})>>i)&1) as int==((a as int)/R.P(i))%2',
                     f' ensures 0<=(((a as bv{w})>>i)&1) as int<2',
                     '{', f' B{w}.RoundTrip(a as int);',
                     f' N{w}.NativeBit(a as bv{w},i); N{w}.BitTest(a as bv{w},i);',
                     f' assert (((a as bv{w})>>i)&1)==0 || (((a as bv{w})>>i)&1)==1;', '}',
                     f'method {{:isolate_assertions}} TestBit{w}(a:U{w},i:nat) returns(isSet:bool)',
                     f' requires i<{w}', f' ensures isSet==C.bit({w},a as int,i)',
                     '{', f' Composed{w}(a,i);',f' isSet:=((((a as bv{w})>>i)&1)==1);','}',
                     f'lemma {{:isolate_assertions}} Boundaries{w}() {{']
        for x,i in ((0,0),(0,w-1),(1,0),(1,w-1),(2,0),(2,1),(m-1,0),
                    (m-1,w-1),(m//2,w-1),(m//2,0),(85,0),(85,1),(170,0),(170,1)):
            bit = (x>>i)&1
            positive += [f' assert ((({x} as bv{w})>>{i})&1) as int=={bit} by {{ N{w}.NativeBit({x},{i}); }}',
                         f' assert C.bit({w},{x},{i})=={str(bit==1).lower()} by {{ B{w}.RoundTrip({x}); N{w}.BitTest({x},{i}); }}']
        positive += ['}']
        for name in OPERATIONS:
            negative += [f'lemma False{name}{w}() {{ N{w}.{name}(0,0);',
                         ' assert false; // ERROR', '}',
                         f'lemma End{name}{w}() {{ N{w}.{name}(1,{w}); // ERROR', '}',
                         f'lemma Past{name}{w}() {{ N{w}.{name}(1,{w+1}); // ERROR', '}',
                         f'lemma Negative{name}{w}() {{ N{w}.{name}(1,-1); // ERROR', '}']
        negative += [f'lemma CombinedFalse{w}() {{ N{w}.NativeBit(0,0); N{w}.BitTest(0,0);',
                     ' assert false; // ERROR', '}',
                     f'lemma WrongSet{w}() {{ B{w}.RoundTrip(1); N{w}.BitTest(1,0);',
                     f' assert !C.bit({w},1,0); // ERROR', '}',
                     f'lemma WrongClear{w}() {{ B{w}.RoundTrip(2); N{w}.BitTest(2,0);',
                     f' assert C.bit({w},2,0); // ERROR', '}',
                     f'lemma WrongEndian{w}() {{ B{w}.RoundTrip(1); N{w}.BitTest(1,{w-1});',
                     f' assert C.bit({w},1,{w-1}); // ERROR', '}',
                     f'lemma WrongDigit{w}() {{ N{w}.NativeBit(2,1);',
                     f' assert (((2 as bv{w})>>1)&1) as int==0; // ERROR', '}',
                     f'lemma RawShift{w}() {{ var x:=(1 as bv{w})>>{w+1}; // ERROR', '}',
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
            assert re.search(r'shift amount must not exceed|function precondition could not be proved',text), (name,text)
            assert any('well-formedness' in r['TestResult.DisplayName'] and r['TestResult.Outcome']=='Failed' for r in rows), (name,rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        if expected_scopes is not None:
            assert len(rows)==expected_scopes, (name,len(rows),expected_scopes)
        if not mutation and not wellformed:
            assert failed==len(errors), (name,failed,len(errors))
        records.append(dict(name=name,exit=result.returncode,scopes=len(rows),failed=failed))

    modes = ('legacy','refreshed') if args.resolver == 'both' else (args.resolver,)
    if not args.mutations_only:
        disabled = subprocess.run(exe+['verify',str(LIBRARY),*common,'--integer-bitwise:false'],
                                  capture_output=True,text=True,timeout=110)
        disabled_text = disabled.stdout+disabled.stderr
        (out/'option-off.log').write_text(disabled_text)
        assert disabled.returncode==2 and 'unresolved identifier: IntegerBitwiseModel' in disabled_text, disabled_text
        records.append(dict(name='option-off',exit=2,scopes=0,failed=0))
        for mode in modes:
            options = ['--type-system-refresh:'+str(mode=='refreshed').lower()]
            verify(mode+'-bodies', LIBRARY, [*options,'--verify-included-files'], expected_scopes=1348)
            for kind, lines in zip(('clients','negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i,line in enumerate(lines,1) if '// ERROR' in line}
                assert len(errors)==(68 if kind=='negative' else 0), (kind,errors)
                verify(mode+'-'+kind,path,options,errors,limit=200000 if errors else 2000000)
                if errors:
                    verify(mode+'-negative-intblast',path,options,errors,limit=200000,intblast=True)
    else:
        original = LIBRARY.read_text()
        for dependency in ('IntegerBitwiseNativeShifts.dfy','IntegerBitwiseCompiler.dfy'):
            original = original.replace('include "'+dependency+'"',
                         'include "'+(LIBRARY.parent/dependency).as_posix()+'"')
        headers = list(re.finditer(r'^  lemma (?:\{:[^}]+\}\s*)*(\w+)\((.*)\)',original,re.M))
        for mode in modes:
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
                        # The zero word at index zero inhabits every contract.
                        at = mutated.index('\n',header.start())
                        mutated = mutated[:at]+'\n    requires x==0 && i==0'+mutated[at:]
                    path = out / 'mutation.dfy'
                    path.write_text(mutated)
                    site = f'{mode}.{module}.{name}.{contract[1]}'
                    options = ['--type-system-refresh:'+str(mode=='refreshed').lower(),'--filter-symbol',f'{module}.{name}.']
                    if fact:
                        verify(site,path,options,mutation=True)
                    else:
                        # Removing the index guard makes the contract ill-formed.
                        # Check that boundary without exploring the ill-formed body.
                        verify(site,path,[*options,'--boogie','/proc:*well-formedness*'],
                               wellformed=True)
        assert len(records)==16*len(modes)
    (out/'summary.json').write_text(json.dumps(records,indent=2)+'\n')
    print('Native bit-test checks passed')


if __name__ == '__main__':
    main()
