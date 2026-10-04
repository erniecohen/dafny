#!/usr/bin/env python3
"""Focused diagnostic; strict receipt booleans, never a substitute for full acceptance."""
import argparse
import csv
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import tarfile
import time

ROOT = Path(__file__).resolve().parents[2]
SOURCE = 'a6ecb742096ddf2f4a6dbcfefb847fdb17ff1fbda7bacf0fafd445f60d843976'
LIBRARY = '9461fe3bb77dfabf981af767b586025e76bda574534f2829b59bf5c462955536'
SOLVER = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
PRIOR_HEAD = 'ee32faedf6968ffef9baf5e3ab18caa91180a670'
ORIGINAL_WORKER = '785fdda10ac925f7b14cc5556831843eb6d8a46f0f7b51054bbe84fc3f07d822'
UPSTREAM = 'ea6e8a18dfe9e317d313de769291f989957dc5f2'
PATCH = 'd0bd9db8270cc47c1988c65698026fcbe314ca98b1a31ed5ebdefd57f11bcf6d'
CORPUS_CASE_NAMES = {
    'true', 'false', 'integer', 'contract', 'bad-postcondition', 'bad-precondition', 'branch', 'bad-branch',
    'real', 'bitvector', 'division', 'visibility', 'loop', 'bad-loop-initialization', 'bad-loop-preservation',
    'break-continuation', 'loop-continue', 'division-signs', 'division-negative-quotient', 'division-wrong-floor',
    'modulo-wrong-remainder', 'division-zero', 'modulo-zero', 'division-false', 'real-exact', 'real-universal',
    'real-floor', 'real-conversion', 'real-state-contract', 'real-fractional-conversion', 'real-zero',
    'real-wrong-floor', 'real-irrational', 'real-false',
}
CORPUS_FIXED = {
    'time-limit': (4, 'requires --verification-time-limit'), 'isolation': (4, 'unsupported by B3'),
    'filter-position': (1, 'does not currently support --filter-position'),
    'missing-worker': (4, 'worker package is unavailable or invalid'), 'solver-help': (4, 'unsupported by B3'),
    'passive-print': (4, 'unsupported by B3'), 'split-print': (4, 'unsupported by B3'),
    'empty-selection-failure': (4, 'unsupported by B3'), 'symbol-filter': (0, None),
    'symbol-filter-negative': (4, 'assertion'), 'empty-selection': (0, None), 'parse-error': (2, 'parse errors detected'),
}
PINS = {
    'summary.json': '9d3d758b82b64174fb57e6f5748a7f90166c0a4959e7e07bde2d08afde867339',
    'worker-bootstrap.txt': 'de609e2a65f62a2fb56319dfad431719971eaf386f35f139329b8cac657c90e0',
    'worker/library/resources.csv': 'fbc8aa720f7e82076a53c1892a8aedde5c4713978f502f65ed9daae874a6efa8',
    'worker/library/B3Library.dll': LIBRARY,
    'inputs/bootstrap/dafny.tar.gz': 'ba06f4d5048ecf0cc40f79281230eb44b429fd73a8bbed5c4377a3d0ff010331',
    'worker/package/b3-worker-manifest.json': ORIGINAL_WORKER,
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path, maximum=512 * 1024 * 1024):
    info = path.lstat()
    require(stat.S_ISREG(info.st_mode) and 0 < info.st_size <= maximum, 'Expected bounded regular file: ' + path.name)
    hasher = hashlib.sha256()
    with path.open('rb') as stream:
        while block := stream.read(65536):
            hasher.update(block)
    return hasher.hexdigest()


def direct_children():
    children = set()
    for task in Path('/proc/self/task').iterdir():
        children.update(int(pid) for pid in (task / 'children').read_text().split())
    return children


def cleanup_children():
    # Same dedicated child-subreaper/pidfd ownership helper as check-b3-package.py.
    # Adoption retains orphaned workers even when they have a different Unix session.
    signalled = 0
    for sig in [signal.SIGTERM, signal.SIGKILL]:
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            while True:
                try:
                    if os.waitpid(-1, os.WNOHANG)[0] == 0:
                        break
                except ChildProcessError:
                    break
            children = direct_children()
            if not children:
                return signalled
            for pid in children:
                try:
                    fd = os.pidfd_open(pid)
                    try:
                        if pid in direct_children():
                            signal.pidfd_send_signal(fd, sig)
                            signalled += 1
                    finally:
                        os.close(fd)
                except ProcessLookupError:
                    pass
            time.sleep(0.01)
    raise RuntimeError('Owned CLI descendants remained after bounded cleanup')


def corpus_expectations():
    path = ROOT / 'Source/IntegrationTests/TestFiles/B3/cases.json'
    manifest = json.loads(path.read_text())
    cases = manifest['cases']
    require(manifest['schemaVersion'] == 1 and len(cases) == 34 and
            {case['name'] for case in cases} == CORPUS_CASE_NAMES and
            not CORPUS_CASE_NAMES.intersection(CORPUS_FIXED), 'Current corpus differs from the exact 46-control scope')
    expected = {case['name']: (case['exitCode'], case['diagnostic']) for case in cases}
    expected.update(CORPUS_FIXED)
    require(len(expected) == 46, 'Incorrect current corpus denominator')
    return expected, {'casesSha256': digest(path), 'runnerSha256': digest(ROOT / 'Scripts/check-b3-integration.py'),
                      'expectedNames': sorted(expected), 'expectedCount': 46}


def prerequisite(prior, solver):
    for relative, expected in PINS.items():
        require(digest(prior / relative) == expected, 'Public prerequisite digest differs: ' + relative)
    vendor = ROOT / 'ThirdParty/B3'
    require(digest(vendor / 'source-manifest.json') == SOURCE, 'Vendored source changed; cached library cannot be reused')
    source_manifest = json.loads((vendor / 'source-manifest.json').read_text())
    require(source_manifest['schemaVersion'] == 1 and source_manifest['upstreamCommit'] == UPSTREAM and
            len(source_manifest['files']) == 58 and
            source_manifest['patches'] == [{'file': 'integration.patch', 'sha256': PATCH}] and
            source_manifest['files']['integration.patch'] == PATCH, 'Incorrect 58-file vendor/patch identity')
    actual = set()
    for relative in source_manifest['vendoredPaths']:
        path = vendor / relative
        if path.is_dir():
            actual.update(str(file.relative_to(vendor)) for file in path.rglob('*') if file.is_file())
        else:
            actual.add(relative)
    actual.update(patch['file'] for patch in source_manifest['patches'])
    require(actual == set(source_manifest['files']), 'Unlisted or missing vendored source file')
    for relative, sha in source_manifest['files'].items():
        require(digest(vendor / relative) == sha, 'Vendored source bytes changed: ' + relative)
    consumer_patterns = {
        'Scripts/build-b3-worker.sh': r"source_manifest\.read_bytes\(\)\)\.hexdigest\(\) == '([0-9a-f]{64})'",
        'Source/DafnyB3Protocol/WorkerPackage.cs': r'public const string SourceFingerprint = "([0-9a-f]{64})";',
        'Scripts/package-b3-experimental.py': r"^SOURCE_SHA = '([0-9a-f]{64})'$",
    }
    consumers = {}
    for relative, pattern in consumer_patterns.items():
        path = ROOT / relative
        require(re.findall(pattern, path.read_text(), re.MULTILINE) == [SOURCE], 'Source consumer pin differs: ' + relative)
        consumers[relative] = {'sha256': digest(path), 'sourceFingerprint': SOURCE}
    require(digest(solver) == SOLVER, 'Original solver-file pin differs')
    receipt = json.loads((prior / 'summary.json').read_text())
    require(receipt['head'] == PRIOR_HEAD and receipt['fullGate'] is True and receipt['passed'] is False,
            'Incorrect prior full-gate boundary')
    proof = receipt['libraryProof']
    require(proof == {'sourceManifestSha256': SOURCE, 'librarySha256': LIBRARY, 'batchCount': 560,
                     'resourceCount': 159556822, 'maximumBatchResources': 20078307}, 'Library receipt differs')
    rows = list(csv.DictReader((prior / 'worker/library/resources.csv').open()))
    require(len(rows) == 560 and all(row['TestResult.Outcome'] == 'Passed' for row in rows), 'Incomplete 560-obligation library receipt')
    resources = [int(row['TestResult.ResourceCount']) for row in rows]
    require(sum(resources) == 159556822 and max(resources) == 20078307, 'Library resource receipt differs')
    totals = re.findall(r'Dafny program verifier finished with (\d+) verified, 0 errors', (prior / 'worker-bootstrap.txt').read_text())
    require(totals == ['560'], 'Bootstrap log does not bind the complete receipt')
    manifest = json.loads((prior / 'worker/package/b3-worker-manifest.json').read_text())
    require(manifest['sourceFingerprint'] == SOURCE and manifest['files']['B3Library.dll'] == LIBRARY, 'Worker/library receipt differs')
    for name, sha in manifest['files'].items():
        require(name == Path(name).name and '\\' not in name and digest(prior / 'worker/package' / name) == sha, 'Original worker file changed')
    # Preserve the original bootstrap compiler for the independent Java compilation.
    # It is checked against the same hash-pinned archive, without extracting or executing it here.
    with tarfile.open(prior / 'inputs/bootstrap/dafny.tar.gz') as archive:
        members = archive.getmembers()
        require(len(members) <= 20000, 'Bootstrap archive inventory exceeds bound')
        checked_names = set()
        for member in members:
            parts = Path(member.name).parts
            require(parts and parts[0] == 'dafny' and all(part not in {'.', '..'} for part in parts), 'Invalid bootstrap member path')
            if not member.isfile():
                continue
            if Path(member.name).suffix not in {'.dll', '.json', '.bpl'}:
                continue
            require(member.name not in checked_names, 'Duplicate bootstrap compiler member')
            require(0 < member.size <= 256 * 1024 * 1024, 'Invalid bootstrap file size')
            hasher = hashlib.sha256()
            with archive.extractfile(member) as stream:
                while block := stream.read(65536):
                    hasher.update(block)
            require(digest(prior / 'worker/compiler' / member.name) == hasher.hexdigest(), 'Captured bootstrap compiler changed')
            checked_names.add(member.name)
        actual_compiler = {path.relative_to(prior / 'worker/compiler').as_posix()
                           for path in (prior / 'worker/compiler/dafny').rglob('*')
                           if path.is_file() and path.suffix in {'.dll', '.json', '.bpl'}}
        require(checked_names and actual_compiler == checked_names, 'Bootstrap compiler reference inventory differs')
        checked = len(checked_names)
    return {'publicPrerequisiteFileHashes': PINS, 'originalFullGatePassed': False,
            'libraryVerificationPassed': True, 'proof': proof, 'solverSha256': SOLVER,
            'originalWorkerFingerprint': ORIGINAL_WORKER, 'bootstrapCompilerFilesChecked': checked,
            'vendorFileCount': 58, 'integrationPatchSha256': PATCH, 'upstreamCommit': UPSTREAM,
            'sourceConsumerPins': consumers}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prior-artifacts', type=Path, required=True, help='Public prior out/b3-native-compile directory')
    parser.add_argument('--solver', type=Path, required=True)
    parser.add_argument('--dotnet', type=Path, required=True, help='Resolved absolute .NET 8 executable')
    parser.add_argument('--compiler-version', required=True, help='Exact fresh CLI informational version')
    parser.add_argument('--compiler-source', required=True, help='Exact current source revision declared by the outer build receipt')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--exploratory-rlimits', default='', help='Explicit comma-separated higher limits; maximum three')
    args = parser.parse_args()
    args.prior_artifacts = args.prior_artifacts.resolve(); args.solver = args.solver.resolve(); args.dotnet = args.dotnet.resolve()
    args.output = args.output.resolve()
    require(not args.output.exists(), 'Diagnostic output must be a fresh directory')
    args.output.mkdir(parents=True)
    stages = []; errors = []; receipt = None; original_cli = []; corpus_scope = None; expected_corpus = {}
    environment = {**os.environ, 'PATH': str(args.dotnet.parent) + os.pathsep + os.environ.get('PATH', ''),
                   'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1'}
    cleanup_poisoned = False

    def run(name, command, timeout=1800, cwd=ROOT):
        nonlocal cleanup_poisoned
        require(not cleanup_poisoned and not direct_children(), 'Require empty owned child scope before each stage')
        log = args.output / (name + '.txt')
        actual_code = None; failure = None; process = None; residual = 0; cleanup_count = 0
        try:
            with log.open('wb') as stream:
                process = subprocess.Popen([str(value) for value in command], cwd=cwd, stdout=stream,
                                           stderr=subprocess.STDOUT, start_new_session=True, env=environment)
                deadline = time.monotonic() + timeout
                while process.poll() is None:
                    if time.monotonic() > deadline or log.stat().st_size > 32 * 1024 * 1024:
                        raise TimeoutError('Stage safety deadline or output byte bound exceeded')
                    time.sleep(0.01)
                actual_code = process.wait()
                residual = len(direct_children())
                if residual:
                    failure = 'Completed stage left adopted descendants'
        except Exception as error:
            failure = type(error).__name__ + ': ' + str(error)
        finally:
            try:
                cleanup_count = cleanup_children()
                if process is not None:
                    process.wait(timeout=5)
                require(not direct_children(), 'Owned stage descendants remained after cleanup')
            except Exception as error:
                cleanup_poisoned = True
                failure = (failure + '; ' if failure else '') + 'Cleanup failed: ' + str(error)
        if log.exists() and log.stat().st_size > 32 * 1024 * 1024:
            failure = 'Output byte bound exceeded'
        if cleanup_count and failure is None:
            failure = 'Stage required forced descendant cleanup'
        code = actual_code if failure is None else 124 if failure.startswith('TimeoutError:') else 2
        stages.append({'stage': name, 'exitCode': code, 'actualProcessExitCode': actual_code,
                       'command': [str(value) for value in command], 'failure': failure,
                       'residualChildrenAtCompletion': residual, 'cleanupSignals': cleanup_count,
                       'cleanupPoisoned': cleanup_poisoned, 'logSha256': digest(log) if log.exists() and log.stat().st_size else None,
                       'remainingAdoptedChildren': len(direct_children())})
        print(name, code, flush=True)
        require(not cleanup_poisoned, 'Failed owned-scope cleanup prevents subsequent stages')
        return code

    try:
        require(Path('/proc/self/task').exists() and hasattr(os, 'pidfd_open') and hasattr(signal, 'pidfd_send_signal'),
                'Requires Linux dedicated child-subreaper/pidfd cleanup')
        require(not direct_children() and ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) == 0,
                'Require an empty dedicated child-subreaper scope')
        require(re.fullmatch('[0-9a-f]{40}', args.compiler_source) and args.compiler_version.startswith('4.11.0+'), 'Invalid compiler source/version identity')
        extras = args.exploratory_rlimits.split(',') if args.exploratory_rlimits else []
        require(len(extras) <= 3 and all(re.fullmatch('[0-9]+', value) and 200000 < int(value) <= 4294967295 for value in extras) and
                len(set(int(value) for value in extras)) == len(extras), 'Invalid explicit exploratory budgets')
        actual_head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True, timeout=10).strip()
        require(actual_head == args.compiler_source, 'Compiler source differs from current checkout')
        receipt = prerequisite(args.prior_artifacts, args.solver)
        expected_corpus, corpus_scope = corpus_expectations()
        require(run('solver-version', [args.solver, '-version'], 10) == 0 and
                (args.output / 'solver-version.txt').read_text().strip() == 'Z3 version 5.1.0 - 64 bit', 'Exact solver version failed')
        require(run('dotnet-version', [args.dotnet, '--version'], 10) == 0 and
                (args.output / 'dotnet-version.txt').read_text().strip().startswith('8.'), 'Requires .NET 8')
        require(run('packages', ['sh', 'Scripts/fetch-boogie-packages.sh']) == 0, 'Pinned Boogie package fetch failed')
        require(run('compiler', [args.dotnet, 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release', '-m:1',
                                 '-p:UseSharedCompilation=false', '--nologo']) == 0, 'Fresh compiler build failed')
        compiler = ROOT / 'Binaries/net8.0'
        require(run('compiler-version', [args.dotnet, compiler / 'Dafny.dll', '--version'], 15) == 0 and
                (args.output / 'compiler-version.txt').read_text().strip() == args.compiler_version, 'Exact compiler version failed')
        (args.output / 'compiler-files.json').write_text(json.dumps({path.name: digest(path) for path in sorted(compiler.glob('*.dll'))}, indent=2) + '\n')
        project = ROOT / '.github/review/real-triage/RealTriage.csproj'
        runner = args.output / 'runner'
        require(run('triage-build', [args.dotnet, 'build', project, '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false',
                                     '-p:DafnyRealTriageReferenceDirectory=' + str(compiler), '-o', runner, '--nologo']) == 0, 'Triage build failed')
        worker = args.prior_artifacts / 'worker/package/DafnyB3Host.dll'
        command = [args.dotnet, runner / 'RealTriage.dll', '--compiler', compiler, '--compiler-version', args.compiler_version,
                   '--compiler-source', args.compiler_source, '--fixtures', ROOT / '.github/review/real-triage',
                   '--worker', worker, '--worker-fingerprint', ORIGINAL_WORKER, '--solver', args.solver, '--dotnet', args.dotnet,
                   '--output', args.output / 'original-requests']
        if args.exploratory_rlimits:
            command += ['--exploratory-rlimits', args.exploratory_rlimits]
        run('real-triage', command)
        fixtures = json.loads((ROOT / '.github/review/real-triage/cases.json').read_text())['cases']
        for fixture in fixtures:
            name = fixture['name']; stage_name = 'original-cli-' + name
            code = run(stage_name, [args.dotnet, compiler / 'Dafny.dll', 'verify',
                                   ROOT / '.github/review/real-triage/original' / fixture['file'],
                                   '--verification-backend', 'b3', '--b3-worker', worker, '--solver-path', args.solver,
                                   '--cores', '1', '--resource-limit', '200000', '--verification-time-limit', '20',
                                   '--show-snippets:false', '--use-basename-for-filename', '--progress', 'Batch'], timeout=120)
            text = (args.output / (stage_name + '.txt')).read_text()
            expected_code = 4 if fixture['expected'] == 'Failed' else 0
            expected_negative = fixture['expected'] == 'Failed'
            if expected_negative:
                matched = code == expected_code and re.search(re.escape(fixture['file']) +
                    r'\(' + str(fixture['requiredFailureLine']) + r',\d+\): Error: B3 failed:.*assertion', text) is not None
            else:
                matched = (code == expected_code and 'verified successfully' in text and 'resource count: unavailable' in text and
                           re.findall(r'Dafny program verifier finished with (\d+) verified, 0 errors', text) == [str(fixture['expectedUnits'])])
            matched = matched and 'internal compilation exception' not in text.lower() and 'internal error occurred' not in text.lower()
            original_cli.append({'fixture': name, 'sourceSha256': fixture['sha256'], 'expectedExitCode': expected_code,
                                 'actualExitCode': code, 'strictMatched': matched, 'originalBudget': 200000,
                                 'warningsAllowed': False, 'requiredNegativeOutcome': 'Failed' if expected_negative else None})
        # These are independent focused acceptance phases on the fresh current source.
        # Neither reuses a prior corpus/Java success or rebuilds/verifies the unchanged library.
        run('current-corpus', ['python3', ROOT / 'Scripts/check-b3-integration.py', compiler / 'Dafny.dll', worker,
                              args.solver, '--output', args.output / 'current-corpus'])
        bootstrap = args.prior_artifacts / 'worker/compiler/dafny/Dafny.dll'
        require(run('bootstrap-version', [args.dotnet, bootstrap, '--version'], 15) == 0 and
                (args.output / 'bootstrap-version.txt').read_text().strip() == '4.11.0+fcb2042d.review.a171069d', 'Exact bootstrap version failed')
        run('java', [args.dotnet, bootstrap, 'build', '--no-verify', '--target=java', 'target/java/src/dfyconfig.toml',
                     '--output', args.output / 'java/b3'], cwd=ROOT / 'ThirdParty/B3')
    except Exception as error:
        errors.append(type(error).__name__ + ': ' + str(error))
    corpus_passed = java_passed = False; java_receipt = None
    try:
        corpus = json.loads((args.output / 'current-corpus/summary.json').read_text())['results']
        corpus_passed = (len(corpus) == 46 and {row['name'] for row in corpus} == set(expected_corpus) and
                         all(row['passed'] is True and (row['expectedExitCode'], row['diagnostic']) == expected_corpus[row['name']]
                             and row['exitCode'] == row['expectedExitCode'] for row in corpus) and
                         any(stage['stage'] == 'current-corpus' and stage['exitCode'] == 0 for stage in stages))
    except Exception as error:
        errors.append('current corpus receipt incomplete: ' + str(error))
    try:
        jar = args.output / 'java/b3.jar'
        java_receipt = {'file': 'java/b3.jar', 'sha256': digest(jar), 'bytes': jar.stat().st_size,
                        'compilerVersion': '4.11.0+fcb2042d.review.a171069d', 'sourceFingerprint': SOURCE}
        java_passed = any(stage['stage'] == 'java' and stage['exitCode'] == 0 for stage in stages)
    except Exception as error:
        errors.append('Java receipt incomplete: ' + str(error))
    triage = None
    summary_path = args.output / 'original-requests/summary.json'
    if summary_path.is_file():
        try:
            triage = json.loads(summary_path.read_text())
        except Exception as error:
            errors.append('original triage receipt invalid: ' + str(error))
    summary = {'diagnosticOnly': True, 'originalFullGatePassed': False, 'prerequisite': receipt,
               'compilerSourceDeclaredByBuildReceipt': args.compiler_source, 'compilerVersion': args.compiler_version,
               'stages': stages, 'errors': errors, 'currentCorpusMatched': corpus_passed, 'currentJavaCompiled': java_passed,
               'currentCorpusScope': corpus_scope, 'currentJavaReceipt': java_receipt,
               'cleanupScope': 'Dedicated Linux child-subreaper; pidfd-signalled owned adopted descendants; no cgroup containment claim',
               'cleanupPoisoned': cleanup_poisoned,
               'originalTriage': triage, 'originalCliControls': original_cli,
               'originalCliAllMatched': len(original_cli) == 3 and all(row['strictMatched'] for row in original_cli),
               'originalBaselineAllMatched': triage is not None and triage.get('baselineAllMatched') is True,
               'fullGateAcceptanceClaimed': False, 'libraryReverified': False}
    (args.output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('Real diagnostic receipt only; inspect strict booleans in summary.json.', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
