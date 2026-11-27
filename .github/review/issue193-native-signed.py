#!/usr/bin/env python3
"""Qualify checked signed-word conversions, sign tests and serial mutations."""
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
LIBRARY = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwiseNativeSigned.dfy'
DEPENDENCY = LIBRARY.with_name('IntegerBitwiseNativeShifts.dfy')
WIDTHS = (8, 16, 32, 64)


def fixtures():
    prelude = [f'include "{LIBRARY.as_posix()}"']
    positive, negative = prelude.copy(), prelude.copy()
    for w in WIDTHS:
        m, h = 2**w, 2**(w-1)
        for lines in (positive, negative):
            lines += [f'import N{w} = Std.Arithmetic.IntegerBitwiseNativeSigned{w}',
                      f'import B{w} = Std.Arithmetic.IntegerBitwiseNative{w}']
        positive += [f'newtype I{w} = a:int | -{h}<=a<{h}',
                     f'lemma SignedRoundTrip{w}(a:int)', f' requires -{h}<=a<{h}',
                     f' ensures N{w}.Decode(N{w}.Encode(a))==a',
                     f'{{ N{w}.RoundTrip(a); }}',
                     f'lemma NativeInverse{w}(x:bv{w})',
                     f' ensures N{w}.Encode(N{w}.Decode(x))==x',
                     f'{{ N{w}.Range(x); N{w}.Inverse(x); }}',
                     f'lemma Composed{w}(a:I{w})',
                     f' ensures N{w}.Decode(N{w}.Encode(a as int))==a as int',
                     f' ensures (((N{w}.Encode(a as int)>>{w-1})&1)==1)==(a<0)',
                     f'{{ N{w}.RoundTrip(a as int); N{w}.SignBit(N{w}.Encode(a as int)); }}',
                     f'method TestSign{w}(x:bv{w}) returns(isNegative:bool)',
                     f' ensures isNegative==(N{w}.Decode(x)<0)',
                     f'{{ N{w}.SignBit(x); isNegative:=((x>>{w-1})&1)==1; }}',
                     f'lemma {{:isolate_assertions}} Boundaries{w}() {{']
        for a in (-h, -h+1, -2, -1, 0, 1, h-2, h-1):
            u = a % m
            positive += [f' assert N{w}.Decode(N{w}.Encode({a}))=={a} by {{ N{w}.RoundTrip({a}); }}',
                         f' assert N{w}.Encode({a}) as int=={u} by {{ B{w}.RoundTrip({u}); }}',
                         f' assert N{w}.Decode({u})=={a} by {{ B{w}.RoundTrip({u}); }}',
                         f' assert ((({u} as bv{w})>>{w-1})&1)=={int(a<0)} by {{ N{w}.SignBit({u}); }}']
        positive += ['}']
        calls = [f'N{w}.Range(0);', f'N{w}.RoundTrip(0);',
                 f'N{w}.Inverse(0);', f'N{w}.SignBit(0);']
        for i, call in enumerate(calls + [' '.join(calls)]):
            negative += [f'lemma False{w}_{i}() {{ {call}', ' assert false; // ERROR', '}']
        for name, expression in [('EncodeLower', f'N{w}.Encode(-{h+1})'),
                                 ('EncodeUpper', f'N{w}.Encode({h})')]:
            negative += [f'lemma {name}{w}() {{ var x:={expression}; // ERROR', '}']
        for name, a in [('RoundTripLower', -h-1), ('RoundTripUpper', h)]:
            negative += [f'lemma {name}{w}() {{ N{w}.RoundTrip({a}); // ERROR', '}']
        for name, call, assertion in [
            ('UnsignedHigh', f'B{w}.RoundTrip({h});', f'N{w}.Decode({h})=={h}'),
            ('WrongEncoding', f'B{w}.RoundTrip({m-1});', f'N{w}.Encode(-1)==1'),
            ('MinimumSign', f'N{w}.SignBit({h});', f'N{w}.Decode({h})>=0'),
            ('OneSign', f'N{w}.SignBit(1);', f'N{w}.Decode(1)<0')]:
            negative += [f'lemma {name}{w}() {{ {call}', f' assert {assertion}; // ERROR', '}']
        negative += [f'lemma BadUnsignedBound{w}() {{ B{w}.RoundTrip({m}); // ERROR', '}',
                     f'lemma BadUnsignedNegative{w}() {{ B{w}.RoundTrip(-1); // ERROR', '}',
                     f'lemma BadCastUpper{w}() {{ var x:={m} as bv{w}; // ERROR', '}',
                     f'lemma BadCastNegative{w}() {{ var x:=(-1) as bv{w}; // ERROR', '}']
    return positive, negative


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
            assert re.search(r'postcondition could not be proved|precondition could not be proved|value to be converted might not fit', text), (name, text)
        with csv_path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        failed = sum(r['TestResult.Outcome']=='Failed' for r in rows)
        assert failed == (1 if mutation else len(errors)), (name, failed, errors)
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
            records.append(verify(mode+'-dependencies', DEPENDENCY, [*options, '--verify-included-files'], expected_scopes=1176))
            records.append(verify(mode+'-bodies', LIBRARY, options, expected_scopes=92))
            for kind, lines in zip(('clients', 'negative'), fixtures()):
                path = out / (mode+'-'+kind+'.dfy')
                path.write_text('\n'.join(lines)+'\n')
                errors = {i for i, line in enumerate(lines, 1) if '// ERROR' in line}
                assert len(errors)==(68 if kind=='negative' else 0), (kind, errors)
                records.append(verify(mode+'-'+kind, path, options, errors, limit=200000 if errors else 2000000))
    else:
        original = LIBRARY.read_text().replace('include "IntegerBitwiseNativeShifts.dfy"',
                                              'include "'+DEPENDENCY.as_posix()+'"')
        sites = []
        for w in WIDTHS:
            module = f'IntegerBitwiseNativeSigned{w}'
            start = original.index('module Std.Arithmetic.'+module)
            end = original.find('\nmodule ', start+1)
            end = end if end>=0 else len(original)
            section = original[start:end]
            headers = list(re.finditer(r'^ (?:lemma (?:\{:[^}]+\}\s*)*|ghost function )(\w+)\(', section, re.M))
            for i, header in enumerate(headers):
                last = headers[i+1].start() if i+1<len(headers) else len(section)
                body = section[header.start():last]
                name = header[1]
                for j, contract in enumerate(re.finditer(r'^ (requires|ensures) (.+)$', body, re.M)):
                    offset = start+header.start()+contract.start()
                    stop = start+header.start()+contract.end()
                    baseline = original
                    fact = contract[1]=='ensures'
                    replacement = ' ensures !('+contract[2]+')' if fact else ''
                    mutant = original[:offset]+replacement+original[stop:]
                    options = ['--filter-symbol', f'{module}.{name}.']
                    if fact:
                        at = original.index('\n', start+header.start())
                        guard = '\n requires '+{'RoundTrip':'a==0','HighQuotient':'u==0'}.get(name,'x==0')
                        baseline = baseline[:at]+guard+baseline[at:]
                        mutant = mutant[:at]+guard+mutant[at:]
                    else:
                        options += ['--boogie', '/proc:*well-formedness*']
                    sites.append((f'{module}.{name}.{contract[1]}{j}', baseline, mutant, options))
            at = original.index('\n', start+section.index('lemma {:induction false} RoundTrip'))
            baseline = original[:at]+'\n requires a==-1'+original[at:]
            for name, old, new in [('Encode', f'(if a<0 then a+{2**w} else a) as bv{w}', f'(if a<0 then a+{2**w}-1 else a) as bv{w}'),
                                   ('Decode', f'else u-{2**w}', f'else u-{2**w}+1')]:
                assert original.count(old)==1, (w, old)
                mutant = baseline.replace(old, new)
                sites.append((f'{module}.{name}.definition', baseline, mutant,
                              ['--filter-symbol', f'{module}.RoundTrip.']))
        assert len(sites)==40, len(sites)
        for mode in modes:
            for site, baseline, mutant, options in sites:
                options = ['--type-system-refresh:'+str(mode=='refreshed').lower(), *options]
                path = out / 'baseline.dfy'
                path.write_text(baseline)
                clean = verify(mode+'.'+site+'.baseline', path, options)
                path = out / 'mutation.dfy'
                path.write_text(mutant)
                result = verify(mode+'.'+site, path, options, mutation=True,
                                timeout=max(5, 3*clean['elapsed']))
                result['baseline'] = clean
                records.append(result)
        assert len(records)==40*len(modes)
    (out / 'summary.json').write_text(json.dumps(records, indent=2)+'\n')
    print('Native signed bridge checks passed')


if __name__ == '__main__':
    main()
