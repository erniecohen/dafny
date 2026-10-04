"""Fixed native rotation diagnostic; workflow success is never verification acceptance."""
import csv
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path.cwd()
OUTPUT = ROOT / 'out/rotation-probe'
FIXTURES = ROOT / '.github/review/rotation-fixtures'
SPEC = importlib.util.spec_from_file_location('pinned_inputs', ROOT / '.github/review/b3-bitvector-proof.py')
INPUTS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(INPUTS)


def require(value, reason):
    if not value:
        raise RuntimeError(reason)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run_case(receipt, case, compiler, solver):
    row = dict(case)
    row.update(evidenceComplete=False, matchesMathematicalTarget=False)
    receipt['cases'].append(row)
    directory = OUTPUT / case['name']
    directory.mkdir()
    fixture = FIXTURES / case['file']
    row['fixtureSha256'] = digest(fixture)
    if case['kind'] == 'smt':
        command = [str(solver), '-smt2', str(fixture)]
    else:
        command = ['dotnet', str(compiler), 'verify', str(fixture), '--cores', '1',
                   '--solver-path', str(solver), '--resource-limit', '200000',
                   '--verification-time-limit', '20', '--use-basename-for-filename',
                   '--log-format', 'csv;LogFileName=' + str(directory / 'resources.csv'),
                   '--bprint', str(directory / 'translated.bpl'),
                   '--solver-log', str(directory / 'native.smt2')]
    stage, log = INPUTS.run_stage(receipt, case['name'], command, cwd=directory, timeout=90)
    text = log.read_text()
    (directory / 'stdout-and-stderr.txt').write_text(text)
    try:
        require(not stage['timedOut'] and 'failure' not in stage, 'Process did not complete')
        if case['kind'] == 'smt':
            require(stage['exitCode'] == 0 and '(error' not in text, 'Solver invocation failed')
            answers = re.findall(r'^(sat|unsat|unknown)\s*$', text, re.M)
            require(len(answers) == 1 and answers[0] != 'unknown', 'Missing or inconclusive solver verdict')
            row['actualVerdict'] = answers[0].capitalize()
            row['solverResourceCounts'] = [int(n) for n in re.findall(r':rlimit-count\s+(\d+)', text)]
            require(len(row['solverResourceCounts']) == 1, 'Missing raw solver resource count')
            row['matchesMathematicalTarget'] = row['actualVerdict'] == case['expected']
        else:
            require(stage['exitCode'] in (0, 4), 'Frontend or tool failure')
            require('internal compilation exception' not in text.lower() and 'internal error' not in text.lower(),
                    'Internal verifier error')
            source = fixture.read_text()
            names = re.findall(r'^(?:lemma|method)\s+(\w+)\(', source, re.M)
            require(len(names) == 1, 'Unexpected fixed routine scope')
            routine = names[0]
            csv_path = directory / 'resources.csv'
            with csv_path.open(newline='') as stream:
                reader = csv.DictReader(stream)
                require({'TestResult.DisplayName', 'TestResult.Outcome', 'TestResult.ResourceCount', 'RandomSeed'} <=
                        set(reader.fieldnames or ()), 'Incomplete proof CSV')
                rows = list(reader)
            expected_names = {routine + ' (well-formedness)', routine + ' (correctness)'}
            require(len(rows) == 2 and {r['TestResult.DisplayName'] for r in rows} == expected_names,
                    'Incomplete or duplicate native proof denominator')
            require(all(r['RandomSeed'] == '0' and 0 <= int(r['TestResult.ResourceCount']) <= 200000 for r in rows),
                    'Unexpected seed or resource count')
            outcomes = {r['TestResult.DisplayName']: r['TestResult.Outcome'] for r in rows}
            require(outcomes[routine + ' (well-formedness)'] == 'Passed', 'Input/rotation guards did not verify')
            proof_outcome = outcomes[routine + ' (correctness)']
            require(proof_outcome in ('Passed', 'Failed'), 'Inconclusive native correctness verdict')
            verified = 2 if proof_outcome == 'Passed' else 1
            errors = 0 if proof_outcome == 'Passed' else 1
            require(re.findall(r'Dafny program verifier finished with (\d+) verified, (\d+) errors?', text) ==
                    [(str(verified), str(errors))], 'CSV and output proof counts disagree')
            require(stage['exitCode'] == (0 if proof_outcome == 'Passed' else 4), 'Exit and CSV verdicts disagree')
            bpl = directory / 'translated.bpl'
            smt = directory / 'native.smt2'
            require(bpl.is_file() and bpl.stat().st_size > 0 and smt.is_file() and smt.stat().st_size > 0,
                    'Actual translated BPL/SMT missing')
            smt_text = smt.read_text()
            row['nativeSolverOptions'] = re.findall(r'^\(set-option[^\n]*', smt_text, re.M)
            require('(set-option :smt.arith.solver 2)' in row['nativeSolverOptions'] and
                    '(set-option :smt.mbqi false)' in row['nativeSolverOptions'] and
                    '(set-option :rlimit 200000)' in row['nativeSolverOptions'], 'Unexpected native solver defaults/budget')
            if case['role'] == 'native-accepted-composition':
                require('ext_rotate_left (ext_rotate_right' in smt_text, 'Actual mixed rotation expression missing')
            row.update(actualVerdict='Verified' if proof_outcome == 'Passed' else 'Failed', proofBatches=rows,
                       resourceCount=sum(int(r['TestResult.ResourceCount']) for r in rows))
            row['matchesMathematicalTarget'] = row['actualVerdict'] == ('Verified' if case['expected'] == 'GoodVerified' else 'Failed')
        row['evidenceComplete'] = True
    except Exception as error:
        row['failure'] = type(error).__name__ + ': ' + str(error)
    row['artifacts'] = [{'file': p.relative_to(OUTPUT).as_posix(), 'sha256': digest(p), 'bytes': p.stat().st_size}
                        for p in sorted(directory.rglob('*')) if p.is_file()]


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    receipt = {'schemaVersion': 1, 'scope': 'fixed native rotation correspondence diagnostic',
               'diagnosticOnly': True, 'backendAcceptanceEstablished': False,
               'proofCostParityEstablished': False, 'evidenceComplete': False,
               'allMathematicalTargetsMatched': False, 'cases': [], 'stages': []}
    try:
        receipt['head'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        manifest = json.loads((FIXTURES / 'manifest.json').read_text())
        receipt['fixtureManifestSha256'] = digest(FIXTURES / 'manifest.json')
        require(manifest['schemaVersion'] == 1 and len(manifest['fixtures']) == 20, 'Wrong fixed fixture denominator')
        require(len({r['name'] for r in manifest['fixtures']}) == 20, 'Duplicate fixed case name')
        listed = set(manifest['files'])
        require(listed == {r['file'] for r in manifest['fixtures']}, 'Wrong manifest fixture set')
        actual = {p.relative_to(FIXTURES).as_posix() for p in FIXTURES.rglob('*') if p.is_file()}
        require(actual == listed | {'manifest.json'}, 'Missing or extra fixed fixture')
        for file, value in manifest['files'].items():
            p = FIXTURES / file
            require(not p.is_symlink() and p.resolve().is_relative_to(FIXTURES.resolve()) and digest(p) == value,
                    'Fixed fixture byte pin mismatch')
        require(not INPUTS.INPUTS.exists(), 'Refusing stale toolchain inputs')
        INPUTS.OUTPUT.mkdir(parents=True, exist_ok=True)
        INPUTS.INPUTS.mkdir()
        compiler, solver = INPUTS.prepare_inputs(receipt)
        for case in manifest['fixtures']:
            run_case(receipt, case, compiler, solver)
        receipt['evidenceComplete'] = len(receipt['cases']) == 20 and all(r['evidenceComplete'] for r in receipt['cases'])
        receipt['allMathematicalTargetsMatched'] = receipt['evidenceComplete'] and all(r['matchesMathematicalTarget'] for r in receipt['cases'])
        indexed = {r['name']: r for r in receipt['cases']}
        controls = ['primitive-left-all', 'primitive-right-all', 'portable-leftleft-good', 'portable-leftleft-bad',
                    'portable-mixed-good', 'portable-mixed-bad', 'dafny-primitive-left-good',
                    'dafny-primitive-right-good', 'dafny-same-direction-good', 'dafny-satisfiable-inputs-false']
        receipt['independentControlsMatched'] = all(indexed[n]['evidenceComplete'] and indexed[n]['matchesMathematicalTarget'] for n in controls)
        receipt['nativeFalsePostconditionVerified'] = indexed['dafny-mixed-bad'].get('actualVerdict') == 'Verified'
        receipt['nativeTruePostconditionFailed'] = indexed['dafny-mixed-good'].get('actualVerdict') == 'Failed'
    except Exception as error:
        receipt['failure'] = type(error).__name__ + ': ' + str(error)
    finally:
        (OUTPUT / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
        text = ('Native rotation diagnostic: ' + ('COMPLETE' if receipt['evidenceComplete'] else 'INCOMPLETE') +
                '\n\nNo backend or parity acceptance. Expected discrepancies exit zero; inspect exact CSV/BPL/SMT and summary.json.\n')
        if os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as stream:
                stream.write(text)
        print(text)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
