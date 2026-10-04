"""Focused universal BV contracts; expected failures are strict receipt data.

Each invocation uses one literal --filter-symbol substring, never a regex union.
The selected-module prefix check also rejects accidental substring matches in
other modules. Function body/postcondition proofs are logged as
"(well-formedness)"; executable method body proofs as "(correctness)" (see
BoogieGenerator.Functions.Wellformedness.cs and BoogieGenerator.cs).
This probe verifies source only: it does not build a B3 library or worker.
"""
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess


ROOT = Path.cwd()
OUTPUT = ROOT / 'out/b3-native-compile'
INPUTS = OUTPUT / 'inputs'
PROOFS = OUTPUT / 'bitvector-proof'
BOOTSTRAP_RUN = '37157932239'
BOOTSTRAP_SHA = 'ba06f4d5048ecf0cc40f79281230eb44b429fd73a8bbed5c4377a3d0ff010331'
COMPILER_VERSION = '4.11.0+fcb2042d.review.a171069d'
SOLVER_NAME = 'z3-5.1.0-x64-glibc-2.39'
SOLVER_URL = 'https://github.com/Z3Prover/z3/releases/download/z3-5.1.0/' + SOLVER_NAME + '.zip'
SOLVER_ARCHIVE_SHA = 'f47be8d27d3230e823bf1eeede2fe0abaca55bb78d0b59974370e6689a92284a'
SOLVER_SHA = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
UPSTREAM = 'ea6e8a18dfe9e317d313de769291f989957dc5f2'
MODULES = (
    ('Types', ('Types.WordBound (well-formedness)',
               'Types.ParseBitvectorWidth (well-formedness)')),
    ('TypeChecker', ('TypeChecker.CheckExpr (correctness)',
                     'TypeChecker.ExpectSameNumericOperands (correctness)')),
    ('BitvectorResources', ('BitvectorResources.ProgramCost (well-formedness)',)),
)
RESOURCE_LIMIT = 100000000
PROOF_TIMEOUT = 120
PROCESS_TIMEOUT = 900


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def source_pin(path, pattern):
    matches = re.findall(pattern, path.read_text())
    require(len(matches) == 1, 'Missing or ambiguous source pin: ' + str(path.relative_to(ROOT)))
    return matches[0]


def run_stage(receipt, name, command, *, cwd=ROOT, timeout=PROCESS_TIMEOUT):
    stage = {'stage': name, 'command': command, 'timeoutSeconds': timeout,
             'exitCode': None, 'timedOut': False}
    receipt['stages'].append(stage)
    log_path = PROOFS / (name + '.txt')
    try:
        with log_path.open('w') as log:
            with subprocess.Popen(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT,
                                  start_new_session=True) as process:
                try:
                    stage['exitCode'] = process.wait(timeout=timeout)
                except subprocess.TimeoutExpired:
                    stage['timedOut'] = True
                    # Stop only this invocation and its solver descendants.
                    try:
                        os.killpg(process.pid, signal.SIGTERM)
                    except ProcessLookupError:
                        pass
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        pass
                    try:
                        os.killpg(process.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    stage['exitCode'] = process.wait(timeout=10)
    except Exception as error:
        stage['failure'] = type(error).__name__ + ': ' + str(error)
    return stage, log_path


def checked_stage(receipt, name, command, **kwargs):
    stage, log = run_stage(receipt, name, command, **kwargs)
    require(stage['exitCode'] == 0 and not stage['timedOut'] and 'failure' not in stage,
            name + ' failed: ' + json.dumps(stage))
    return log


def validate_source(receipt):
    source = ROOT / 'ThirdParty/B3'
    manifest_path = source / 'source-manifest.json'
    fingerprint = digest(manifest_path)
    receipt['sourceManifestSha256'] = fingerprint
    receipt['sourceValidated'] = False
    pins = [
        source_pin(ROOT / 'Scripts/build-b3-worker.sh',
                   r"source_manifest.read_bytes\(\)\).hexdigest\(\) == '([0-9a-f]{64})'"),
        source_pin(ROOT / 'Source/DafnyB3Protocol/WorkerPackage.cs',
                   r'SourceFingerprint = "([0-9a-f]{64})";'),
        source_pin(ROOT / 'Scripts/package-b3-experimental.py',
                   r"SOURCE_SHA = '([0-9a-f]{64})'"),
    ]
    receipt['sourceConsumerPins'] = pins
    require(all(pin == fingerprint for pin in pins), 'Source consumer pin mismatch')
    manifest = json.loads(manifest_path.read_text())
    receipt['sourceInventoryCount'] = len(manifest['files'])
    receipt['b3UpstreamCommit'] = manifest['upstreamCommit']
    require(manifest['upstreamCommit'] == UPSTREAM, 'Unexpected B3 source provenance')
    require(len(manifest['files']) == 60, 'Expected exact 60-file BV source inventory')
    inventory = set()
    for name in manifest['vendoredPaths']:
        path = source / name
        if path.is_dir():
            inventory.update(p.relative_to(source).as_posix() for p in path.rglob('*') if p.is_file())
        else:
            inventory.add(name)
    inventory.update(patch['file'] for patch in manifest['patches'])
    require(inventory == set(manifest['files']), 'Missing or unlisted B3 source file')
    for name, expected in manifest['files'].items():
        path = source / name
        require(not path.is_symlink() and path.is_file() and path.resolve().is_relative_to(source.resolve()),
                'Invalid source entry: ' + name)
        require(digest(path) == expected, 'Changed source entry: ' + name)
    for patch in manifest['patches']:
        require(manifest['files'][patch['file']] == patch['sha256'], 'Patch pin mismatch')
    receipt['sourceValidated'] = True


def prepare_inputs(receipt):
    receipt['inputs'] = {'bootstrapPublicRun':
        'https://github.com/erniecohen/dafny/actions/runs/' + BOOTSTRAP_RUN,
        'expectedBootstrapArchiveSha256': BOOTSTRAP_SHA,
        'expectedSolverArchiveSha256': SOLVER_ARCHIVE_SHA,
        'expectedSolverExecutableSha256': SOLVER_SHA}
    bootstrap = INPUTS / 'bootstrap'
    checked_stage(receipt, 'fetch-bootstrap',
                  ['gh', 'run', 'download', BOOTSTRAP_RUN, '-R', 'erniecohen/dafny',
                   '-n', 'dafny', '-D', str(bootstrap)], timeout=180)
    archive = bootstrap / 'dafny.tar.gz'
    receipt['inputs']['bootstrapArchiveSha256'] = digest(archive)
    require(receipt['inputs']['bootstrapArchiveSha256'] == BOOTSTRAP_SHA, 'Bootstrap archive byte pin mismatch')
    checked_stage(receipt, 'fetch-solver',
                  ['curl', '--fail', '--location', '--silent', '--show-error', '--max-time', '120',
                   '--output', str(INPUTS / 'z3.zip'), SOLVER_URL], timeout=150)
    receipt['inputs']['solverArchiveSha256'] = digest(INPUTS / 'z3.zip')
    require(receipt['inputs']['solverArchiveSha256'] == SOLVER_ARCHIVE_SHA, 'Solver archive byte pin mismatch')
    compiler_dir = INPUTS / 'compiler'
    compiler_dir.mkdir()
    # Extraction is allowed only after the immutable public archives match.
    checked_stage(receipt, 'unpack-compiler',
                  ['tar', '-xzf', str(archive), '-C', str(compiler_dir)], timeout=120)
    checked_stage(receipt, 'unpack-solver',
                  ['unzip', '-q', str(INPUTS / 'z3.zip'), '-d', str(INPUTS)], timeout=120)
    compiler = compiler_dir / 'dafny/Dafny.dll'
    solver = INPUTS / SOLVER_NAME / 'bin/z3'
    solver.chmod(0o755)
    receipt['inputs']['compilerDllSha256'] = digest(compiler)
    receipt['inputs']['solverExecutableSha256'] = digest(solver)
    require(receipt['inputs']['solverExecutableSha256'] == SOLVER_SHA, 'Solver executable byte pin mismatch')
    compiler_log = checked_stage(receipt, 'compiler-version', ['dotnet', str(compiler), '--version'], timeout=20)
    receipt['inputs']['compilerVersion'] = compiler_log.read_text().strip()
    require(receipt['inputs']['compilerVersion'] == COMPILER_VERSION, 'Unexpected bootstrap version')
    solver_log = checked_stage(receipt, 'solver-version', [str(solver), '-version'], timeout=10)
    receipt['inputs']['solverVersion'] = solver_log.read_text().strip()
    require(receipt['inputs']['solverVersion'] == 'Z3 version 5.1.0 - 64 bit', 'Unexpected solver version')
    return compiler, solver


def prove_module(receipt, compiler, solver, selector, required):
    result = {'selector': selector, 'selectorKind': 'literal-substring', 'passed': False,
              'requiredVerifiedBatches': list(required)}
    receipt['modules'].append(result)
    csv_path = PROOFS / (selector + '.csv')
    stage, log = run_stage(receipt, selector,
        ['env', 'DOTNET_GCHeapHardLimit=C0000000', 'dotnet', str(compiler),
         'verify', 'library/dfyconfig.toml', '--filter-symbol', selector,
         '--solver-path', str(solver), '--cores', '2', '--resource-limit', str(RESOURCE_LIMIT),
         '--verification-time-limit', str(PROOF_TIMEOUT),
         '--log-format', 'csv;LogFileName=' + str(csv_path)], cwd=ROOT / 'ThirdParty/B3')
    try:
        with csv_path.open(newline='') as stream:
            reader = csv.DictReader(stream)
            columns = {'TestResult.DisplayName', 'TestResult.Outcome', 'TestResult.ResourceCount', 'RandomSeed'}
            require(columns <= set(reader.fieldnames or ()), 'Incomplete CSV columns')
            rows = list(reader)
        require(rows, 'Empty proof denominator')
        names = [row['TestResult.DisplayName'] for row in rows]
        resources = [int(row['TestResult.ResourceCount']) for row in rows]
        result.update(batchCount=len(rows), observedBatches=sorted(names),
                      verifiedBatches=sorted(row['TestResult.DisplayName'] for row in rows
                                            if row['TestResult.Outcome'] == 'Passed'),
                      resourceCount=sum(resources), maximumBatchResources=max(resources),
                      failedBatches=[row for row in rows if row['TestResult.Outcome'] != 'Passed'],
                      randomSeeds=sorted({row['RandomSeed'] for row in rows}))
        summaries = re.findall(r'Dafny program verifier finished with (\d+) verified, (\d+) errors?', log.read_text())
        result['outputSummaries'] = [{'verified': int(n), 'errors': int(e)} for n, e in summaries]
        require(stage['exitCode'] == 0 and not stage['timedOut'] and 'failure' not in stage,
                'Verifier invocation did not complete successfully')
        require(all(row['TestResult.Outcome'] == 'Passed' for row in rows), 'Unsuccessful proof batches')
        require(all(name.startswith(selector + '.') for name in names), 'Unexpected selected module')
        require(set(required) <= set(names), 'Required verified routine missing')
        require(all(0 <= resource <= RESOURCE_LIMIT for resource in resources), 'Invalid or excessive proof resources')
        require(result['randomSeeds'] == ['0'], 'Unexpected proof seed')
        require(len(summaries) == 1 and int(summaries[0][1]) == 0 and int(summaries[0][0]) == len(rows),
                'Output and CSV proof counts disagree')
        result['passed'] = True
    except Exception as error:
        result['failure'] = type(error).__name__ + ': ' + str(error)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    receipt = {'scope': 'focused B3 bitvector universal contracts', 'passed': False,
               'completeLibraryVerified': False, 'libraryBinaryProduced': False,
               'workerBinaryProduced': False,
               'resourceLimit': RESOURCE_LIMIT, 'verificationTimeLimitSeconds': PROOF_TIMEOUT,
               'processSafetyTimeoutSeconds': PROCESS_TIMEOUT, 'modules': [], 'stages': []}
    try:
        require(not INPUTS.exists() and not PROOFS.exists(), 'Refusing stale input or proof output')
        INPUTS.mkdir()
        PROOFS.mkdir()
        receipt['head'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        validate_source(receipt)
        compiler, solver = prepare_inputs(receipt)
        # Continue after individual proof failures to record all three scopes.
        for selector, required in MODULES:
            prove_module(receipt, compiler, solver, selector, required)
        receipt['passed'] = len(receipt['modules']) == len(MODULES) and all(m['passed'] for m in receipt['modules'])
    except Exception as error:
        receipt['failure'] = type(error).__name__ + ': ' + str(error)
    finally:
        (OUTPUT / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
        summary = ['Focused B3 bitvector proofs: ' + ('PASS' if receipt['passed'] else 'NOT GREEN'), '',
                   'Selected contracts only. No whole-library proof, worker build or backend runtime acceptance. '
                   'Expected failures exit zero; inspect summary.json and each CSV.']
        if receipt['modules']:
            summary.append('')
        for module in receipt['modules']:
            summary.append('- ' + module['selector'] + ': ' + ('PASS' if module['passed'] else 'NOT GREEN') +
                           ', batches=' + str(module.get('batchCount', 'unavailable')) +
                           ', RU=' + str(module.get('resourceCount', 'unavailable')) +
                           ', max RU=' + str(module.get('maximumBatchResources', 'unavailable')))
        if 'GITHUB_STEP_SUMMARY' in os.environ:
            with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as stream:
                stream.write('\n'.join(summary) + '\n')
        print('\n'.join(summary))
    # This is an expected-failure probe. The strict receipt is the verdict.
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
