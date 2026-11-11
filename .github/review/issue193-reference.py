#!/usr/bin/env python3
"""Verify the optional reference model and inventory live false controls.

No expected failure is green unless it is exactly a designated assertion error,
with every logged proof scope present and no warning, timeout or exhaustion.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy'
DIGITS = MODEL.with_name('IntegerBitwiseDigits.dfy')
FIXTURE = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-reference-laws.dfy'
# Every declared lemma has an inhabited call, including arithmetic helpers.
# n is one of the measured widths; s preserves leading zeros.
CALLS = {
    'PowerZero': '', 'NormId': 'n,0', 'PowerSum': 'n,0',
    'Quotient': '5,2,2,1', 'ProductBounds': '1,2,1,4',
    'DivProduct': '5,2,4', 'NormStep': 'n,5', 'ValueBound': 's',
    'BitsValue': 'n,-1', 'ValueBits': 's', 'ValueInjective': 's,s',
    'NormalizeBits': 'n,-1', 'BitAt': 'n,-1,0', 'AndRange': 'n,5,3',
    'AndValue': 'n,5,3', 'FlipValue': 's', 'AndView': 'n,5,3',
    'NotView': 'n,5', 'ZipAt': 's,s,0', 'BooleanBits': 'n,5,3,0',
    'ConcatValue': 's,s', 'SplitValue': 's,n/2', 'ZeroBits': 'n',
    'MulMod': '5,2,4', 'ShiftViews': 'n,5,n/2', 'ShiftBits': 'n,5,n/2,0',
    'RotationViews': 'n,5,-1', 'RotationIndices': 'n,-1,0',
    'RotationBits': 'n,5,-1,0', 'PopulationView': 'n,-1',
    'BooleanRange': 'n,5,3', 'EqualByBits': 'n,0,0', 'AndZero': 'n,5',
    'AndMaskLow': 'n,5,n/2', 'AndBound': 'n,5,3', 'OrBound': 'n,0,3',
    'XorSelf': 'n,5', 'AndLowBit': 'n,5', 'BitTest': 'n,5,0',
    'MaskMergeBit': 'n,5,3,1,0', 'ShiftRoundDown': 'n,0,n/2',
    'ShiftFits': 'n,0,n/2', 'PopZero': 'n', 'PopPower': 'n,0',
    'RotateEndpoints': 'n,5', 'ReferenceEndpoints': 'n,-1,-1',
    'ModShift': '-5,3,n', 'RotationAlgebra': 'n,5,3,-1',
    'RotationComposeBit':'n,5,3,-1,0', 'RotationRange':'n,5,-1',
    'RotationInverseBit':'n,5,-1,0', 'IndexRoundTrip':'n,-1,0',
    'CarrierRotationCounterexample': '',
}
DIGIT_CALLS = {
    'PowerAgreement': 'n', 'DigitInverses': 's,D.toDigits(s)',
    'DigitsValue': 's', 'BaseTwoAdapter': 'n,-1',
}
FIXTURE_CALLS = {'SequenceWitnesses': '', 'WidthMismatch': '',
                 'TenClients': '8,0,3,1,4,0'}
LEMMA = re.compile(r'\blemma\s+(?:\{:[^}]*\}\s*)*(\w+)\s*\(')


def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def inventory():
    for path, calls in ((MODEL, CALLS), (DIGITS, DIGIT_CALLS), (FIXTURE, FIXTURE_CALLS)):
        names = LEMMA.findall(path.read_text())
        if len(names) != len(set(names)) or set(names) != set(calls):
            raise ValueError(f'{path.name}: lemma/control inventory differs: {set(names)^set(calls)}')
        if re.search(r'\b(assume|expect)\b|\{:\s*(axiom|verify|extern)',
                     re.sub(r'//[^\n]*', '', path.read_text())):
            raise ValueError(f'{path.name}: forbidden trust escape')


def materialize(path, calls, n, negative, digits=False, fixture=False):
    include = DIGITS if digits else FIXTURE if fixture else MODEL
    lines = [f'include "{os.path.relpath(include,path.parent)}"']
    if not fixture:
        lines += ['import B = Std.Arithmetic.IntegerBitwise']
    if digits:
        lines += ['import D = Std.Arithmetic.IntegerBitwiseDigits']
    bad = []
    for name, args in calls.items():
        prefix = '' if fixture else 'D.' if digits else 'B.'
        lines += [f'lemma {{:induction false}} {name}_{n}()', '{']
        if not fixture:
            lines += [f'  var n := {n};', '  var s := B.bitsNat(n,5);']
        lines += [f'  {prefix}{name}({args});']
        if negative:
            lines += ['  assert false;']
            bad.append(len(lines))
        lines += ['}', '']
    path.write_text('\n'.join(lines)+'\n')
    return bad


def symbolic_clients(path, source, n, digits=False):
    """Instantiate each checked contract at a width, retaining symbolic inputs.

    Each client proves the original postconditions with one scalar lemma call.
    This measures consumers separately from generic body construction.
    """
    include = DIGITS if digits else MODEL
    lines = [f'include "{os.path.relpath(include,path.parent)}"',
             'import opened B = Std.Arithmetic.IntegerBitwise']
    if digits:
        lines += ['import opened A = Std.Arithmetic.IntegerBitwiseDigits',
                  'import D = Std.Arithmetic.IntegerBitwiseBase2',
                  'import Power = Std.Arithmetic.Power']
    text = source.read_text()
    for found in LEMMA.finditer(text):
        name = found.group(1)
        start = found.end()
        end = text.index(')',start)
        parameters = [p.strip() for p in text[start:end].split(',') if p.strip()]
        names = [p.split(':')[0].strip() for p in parameters]
        args = [str(n) if x=='n' else x for x in names]
        parameters = [p for p in parameters if p.split(':')[0].strip()!='n']
        body = text.index('{',end)
        spec = text[end+1:body]
        spec = re.sub(r'\n\s*decreases[^\n]*', '',spec)
        spec = re.sub(r'\bn\b',str(n),spec)
        lines += [f'lemma {{:induction false}} {name}_{n}('+','.join(parameters)+')']
        for parameter in parameters:
            if 'seq<' in parameter:
                lines += [f'  requires |{parameter.split(":")[0].strip()}| == {n}']
        lines += [spec, '{', f'  {"A" if digits else "B"}.{name}('+','.join(args)+');', '}', '']
    path.write_text('\n'.join(lines)+'\n')


def run(dafny, solver, path, out, negative_lines=(), libraries=False, resolver=False,
        included=False):
    out.mkdir(parents=True, exist_ok=True)
    resources = out / 'resources.csv'
    argv = [str(dafny), 'verify', str(path), '--solver-path', str(solver),
            '--cores', '1', '--additional-axioms:false', '--allow-warnings:false',
            '--resource-limit', '200000' if negative_lines else '2000000',
            '--verification-time-limit', '60', '--type-system-refresh:'+str(resolver).lower(),
            '--log-format', 'csv;LogFileName='+str(resources)]
    if libraries:
        # Embedded libraries require their recorded empty Boogie option list.
        argv += ['--standard-libraries:true']
    else:
        argv += ['--standard-libraries:false', '--boogie', '/normalizeDeclarationOrder:0',
                 '--boogie', '/restartProver']
    if included:
        argv += ['--verify-included-files']
    with (out/'stdout.txt').open('wb') as stdout, (out/'stderr.txt').open('wb') as stderr:
        proc = subprocess.Popen(argv, stdout=stdout, stderr=stderr, start_new_session=True)
        try:
            code = proc.wait(timeout=600)
        except subprocess.TimeoutExpired:
            os.killpg(proc.pid, signal.SIGKILL)
            proc.wait()
            code = 124
    text = (out/'stdout.txt').read_text()+(out/'stderr.txt').read_text()
    rows, errors = [], []
    try:
        with resources.open(encoding='utf-8-sig', newline='') as stream:
            reader = csv.DictReader(stream)
            if reader.fieldnames != ['TestResult.DisplayName','TestResult.Outcome',
                                      'TestResult.Duration','TestResult.ResourceCount','RandomSeed']:
                raise ValueError('unexpected CSV schema')
            rows = list(reader)
        if not rows:
            errors.append('empty proof log')
        if any(r['TestResult.Outcome'] not in ('Passed','Failed') for r in rows):
            errors.append('inconclusive logged outcome')
        if any(int(r['TestResult.ResourceCount']) < 0 for r in rows):
            errors.append('invalid resource count')
    except (OSError, ValueError, KeyError) as e:
        errors.append(str(e))
    diagnostics = [(int(line), message) for line,message in re.findall(
        r'\((\d+),\d+\): Error: ([^\n]+)', text)]
    if re.search(r'Warning:|out of resource|timed? out|time out|inconclusive|unknown|resolution/type errors|parse errors', text, re.I):
        errors.append('warning or inconclusive diagnostic')
    if negative_lines:
        if code != 4 or sorted(line for line,_ in diagnostics) != sorted(negative_lines):
            errors.append('not exactly the designated false-assertion failures')
        if any(message != 'assertion might not hold' for _,message in diagnostics):
            errors.append('unexpected error kind')
        if sum(r['TestResult.Outcome']=='Failed' for r in rows) != len(negative_lines):
            errors.append('false-control proof log does not account for every failure')
    elif code != 0 or diagnostics or any(r['TestResult.Outcome']!='Passed' for r in rows):
        errors.append('positive proof failed')
    result = {'input': str(path.relative_to(ROOT)), 'input_sha256': digest(path),
              'command': argv, 'exit_code': code, 'proof_scopes': len(rows),
              'expected_assertion_errors': len(negative_lines), 'errors': errors,
              'resource_units': sum(int(r['TestResult.ResourceCount']) for r in rows),
              'maximum_scope_ru': max([int(r['TestResult.ResourceCount']) for r in rows],default=0)}
    (out/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    print(f'{out.name}: {"PASS" if not errors else "FAIL"} ({result["proof_scopes"]} scopes)',flush=True)
    if errors:
        print(text,flush=True)
    return result, rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', type=Path, required=True)
    parser.add_argument('--z3', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--resolver', choices=['legacy','refreshed'],required=True)
    args = parser.parse_args()
    inventory()
    out = args.output.resolve()
    out.mkdir(parents=True,exist_ok=True)
    dafny, solver = args.dafny.resolve(), args.z3.resolve()
    checks, costs = [], []
    def check(path, name, **kw):
        result, rows = run(dafny,solver,path,out/name,resolver=args.resolver=='refreshed',**kw)
        checks.append(result)
        return rows
    check(MODEL,'model')
    check(DIGITS,'digits',libraries=True)
    check(FIXTURE,'regression')
    for n in (8,16,32,64):
        for label, calls, kw in (('model',CALLS,{}), ('digits',DIGIT_CALLS,{'digits':True})):
            client_path = out/f'{label}-{n}-clients.dfy'
            symbolic_clients(client_path,DIGITS if label=='digits' else MODEL,n,digits=label=='digits')
            for row in check(client_path,f'{label}-{n}-clients',libraries=label=='digits'):
                costs.append([label+'-symbolic-client',n,row['TestResult.DisplayName'],row['TestResult.ResourceCount']])
            for negative in (False, True):
                name = f'{label}-{n}-'+('false' if negative else 'calls')
                path = out/(name+'.dfy')
                lines = materialize(path,calls,n,negative,**kw)
                rows = check(path,name,negative_lines=lines,libraries=label=='digits')
                if not negative:
                    for row in rows:
                        costs.append([label,n,row['TestResult.DisplayName'],row['TestResult.ResourceCount']])
    path = out/'regression-false.dfy'
    lines = materialize(path,FIXTURE_CALLS,8,True,fixture=True)
    check(path,'regression-false',negative_lines=lines)
    with (out/'costs.tsv').open('w',newline='') as stream:
        writer=csv.writer(stream,delimiter='\t')
        writer.writerow(['family','width','proof_scope','resource_units'])
        writer.writerows(costs)
    # Rows above measure live scalar calls. Construction costs are retained
    # separately in model/resources.csv and digits/resources.csv.
    summary={'source':os.environ.get('GITHUB_SHA',''), 'solver_sha256':digest(solver),
             'dafny_version':subprocess.check_output([str(dafny),'--version'],text=True).strip(),
             'solver_version':subprocess.check_output([str(solver),'--version'],text=True).strip(),
             'inputs':{p.name:digest(p) for p in (MODEL,DIGITS,FIXTURE)},
             'resolver':args.resolver,'checks':checks,'passed':all(not c['errors'] for c in checks)}
    (out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    md=['## Integer bitwise reference model', '',
        f'Solver: `{summary["solver_version"]}`; resolver: {args.resolver}.',
        'Additional axioms OFF. No integer-bitwise language option exists in M1.', '',
        '| Check | Scopes | Expected errors | Total RU | Max scope RU | Result |',
        '|---|---:|---:|---:|---:|---|']
    for c in checks:
        md.append(f'| {Path(c["command"][2]).stem} | {c["proof_scopes"]} | {c["expected_assertion_errors"]} | {c["resource_units"]} | {c["maximum_scope_ru"]} | {"PASS" if not c["errors"] else "FAIL"} |')
    md+=['','Per-lemma call costs by width are in `costs.tsv`; complete construction',
         'costs, inputs, commands, hashes and raw logs are retained in this artifact.',
         'Digits use the embedded standard library and its required empty Boogie',
         'option list; core model/call/control runs use normalizeDeclarationOrder:0',
         'and restartProver. These two cost populations are kept separate.']
    (out/'summary.md').write_text('\n'.join(md)+'\n')
    return 0 if summary['passed'] else 1

if __name__=='__main__':
    sys.exit(main())
