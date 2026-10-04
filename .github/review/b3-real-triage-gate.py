#!/usr/bin/env python3
"""Scratch-only Real diagnosis; a zero job exit delivers evidence, never acceptance."""
import ctypes
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import subprocess
import time
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PUBLIC_RUN = 37221479537
PUBLIC_HEAD = 'ee32faedf6968ffef9baf5e3ab18caa91180a670'
PUBLIC_ARTIFACT = 11311016429
PUBLIC_ARTIFACT_NAME = 'b3-native-compile'
PUBLIC_ARCHIVE_SHA = '76be6dadc245884a15c52b0b94d6eef8f16c96caed2d12ed23b3956c91e617fe'
PUBLIC_ARCHIVE_BYTES = 145457636
INNER_SEAL = '9dbfe0efdb8f96bb932ff6029f59c8275fa85e8ba4c1c9a2f59fa70ef5cf90ca'
SOURCE = 'a6ecb742096ddf2f4a6dbcfefb847fdb17ff1fbda7bacf0fafd445f60d843976'
SOLVER = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
SEAL_PATH = '.github/review/real-triage/source-manifest.json'
SEALED_FILES = {
    '.github/review/b3-real-triage.py', '.github/review/real-triage/Program.cs',
    '.github/review/real-triage/RealTriage.csproj', '.github/review/real-triage/cases.json',
    '.github/review/real-triage/original/real-conversion.dfy',
    '.github/review/real-triage/original/real-irrational.dfy',
    '.github/review/real-triage/original/real-universal.dfy', 'docs/dev/b3/real-corpus-triage.md',
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path, maximum=512 * 1024 * 1024):
    info = path.lstat()
    require(stat.S_ISREG(info.st_mode) and 0 < info.st_size <= maximum, 'Expected bounded regular file: ' + path.name)
    hasher = hashlib.sha256()
    size = 0
    with path.open('rb') as stream:
        while block := stream.read(65536):
            size += len(block)
            require(size <= maximum, 'Input exceeds bound while reading: ' + path.name)
            hasher.update(block)
    require(size == info.st_size, 'Input changed length while reading: ' + path.name)
    return hasher.hexdigest()


def sealed_inner():
    seal = ROOT / SEAL_PATH
    require(digest(seal, 65536) == INNER_SEAL, 'Diagnostic source seal changed')
    manifest = json.loads(seal.read_text())
    require(manifest['schemaVersion'] == 1 and set(manifest['files']) == SEALED_FILES and
            manifest['sourceOnly'] is True and manifest['sourceManifestSha256'] == SOURCE,
            'Diagnostic source inventory differs')
    for name, entry in manifest['files'].items():
        path = ROOT / name
        require(path.stat().st_size == entry['bytes'] and digest(path, 1024 * 1024) == entry['sha256'],
                'Diagnostic source bytes changed: ' + name)
    spec = importlib.util.spec_from_file_location('b3_real_triage', ROOT / '.github/review/b3-real-triage.py')
    inner = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inner)
    return inner, manifest


def extract_prerequisite(archive_path, destination):
    require(not destination.exists(), 'Public download destination must be fresh')
    destination.mkdir()
    prefix = 'out/b3-native-compile/'
    exact = {prefix + name for name in [
        'summary.json', 'worker-bootstrap.txt', 'worker/library/B3Library.dll',
        'worker/library/resources.csv', 'inputs/bootstrap/dafny.tar.gz',
        'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3',
    ]}
    prefixes = [prefix + 'worker/package/', prefix + 'worker/compiler/']
    captured = {}
    with zipfile.ZipFile(archive_path) as archive:
        members = archive.infolist()
        require(len(members) <= 20000, 'Public artifact file-count bound exceeded')
        names = set()
        declared_total = written_total = 0
        for member in members:
            name = member.filename
            path = PurePosixPath(name)
            require(name not in names and '\\' not in name and not path.is_absolute() and
                    '..' not in path.parts and name == path.as_posix() + ('/' if member.is_dir() else ''),
                    'Invalid public artifact path')
            names.add(name)
            mode = member.external_attr >> 16
            require(stat.S_IFMT(mode) in {0, stat.S_IFREG, stat.S_IFDIR} and
                    0 <= member.file_size <= 512 * 1024 * 1024, 'Invalid public artifact file type/size')
            declared_total += member.file_size
            require(declared_total <= 2 * 1024 * 1024 * 1024, 'Public artifact expanded-byte bound exceeded')
            if member.is_dir() or not (name in exact or any(name.startswith(value) for value in prefixes)):
                continue
            target = destination / name
            require(target.resolve().is_relative_to(destination.resolve()), 'Public artifact extraction escapes destination')
            target.parent.mkdir(parents=True, exist_ok=True)
            hasher = hashlib.sha256()
            size = 0
            with archive.open(member) as source, target.open('xb') as output:
                while block := source.read(65536):
                    size += len(block)
                    written_total += len(block)
                    require(size <= member.file_size and written_total <= 1024 * 1024 * 1024,
                            'Selected public input expanded-byte bound exceeded')
                    hasher.update(block)
                    output.write(block)
            require(size == member.file_size, 'Public artifact extraction length differs')
            target.chmod(0o644)
            captured[name] = {'bytes': size, 'sha256': hasher.hexdigest()}
    require(exact <= set(captured) and any(name.startswith(prefixes[0]) for name in captured) and
            any(name.startswith(prefixes[1]) for name in captured), 'Required public inputs missing')
    return destination / 'out/b3-native-compile', captured


def main():
    output = ROOT / 'out/b3-native-compile'
    receipt = {'schemaVersion': 1, 'scope': 'focused Real corpus diagnostic', 'diagnosticOnly': True,
               'head': None, 'passed': False, 'fullGateAcceptanceClaimed': False, 'libraryReverified': False,
               'sourceSealSha256': INNER_SEAL, 'stages': [], 'cleanupPoisoned': False,
               'publicPrerequisite': {'run': PUBLIC_RUN, 'head': PUBLIC_HEAD, 'artifactId': PUBLIC_ARTIFACT,
                                      'artifactName': PUBLIC_ARTIFACT_NAME, 'archiveSha256': PUBLIC_ARCHIVE_SHA,
                                      'archiveBytes': PUBLIC_ARCHIVE_BYTES, 'originalFullGatePassed': False}}
    inner = None
    try:
        require(not output.exists(), 'Outer diagnostic output must be fresh')
        output.mkdir(parents=True)
        require(os.environ.get('B3_FOCUS_GATE') == 'real-triage' and
                os.environ.get('GITHUB_EVENT_NAME') == 'workflow_dispatch' and
                os.environ.get('GITHUB_REF', '').startswith('refs/heads/scratch/') and
                os.environ.get('B3_FULL_GATE', '').lower() in {'', 'false'},
                'Real diagnosis requires scratch workflow_dispatch, real-triage focus, and no full bootstrap')
        inner, source_manifest = sealed_inner()
        require(Path('/proc/self/task').exists() and hasattr(os, 'pidfd_open') and
                hasattr(inner.signal, 'pidfd_send_signal'), 'Requires Linux child-subreaper/pidfd ownership cleanup')
        require(not inner.direct_children() and ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) == 0,
                'Require an empty dedicated child-subreaper scope')
        receipt['diagnosticSourceManifest'] = source_manifest
        receipt['coordinatorSourceSha256'] = digest(Path(__file__), 1024 * 1024)
        receipt['workflowSourceSha256'] = digest(ROOT / '.github/workflows/review.yml', 1024 * 1024)
        receipt['productLedger'] = (ROOT / '.github/review/base').read_text().strip()
        deadline = time.monotonic() + 2400

        def stage(name, command, timeout=180, captured_stdout=None, maximum=32 * 1024 * 1024,
                  environment=None):
            require(not receipt['cleanupPoisoned'] and not inner.direct_children(), 'Require empty owned scope before stage')
            log_path = output / (name + '.txt')
            process = None
            code = None
            failure = None
            residual = signals = 0
            capture = captured_stdout or log_path
            capture_stream = None
            at_completion = None; before_cleanup = None; natural_exit = None

            def check_output():
                if capture.stat().st_size > maximum or log_path.stat().st_size > 32 * 1024 * 1024:
                    raise TimeoutError('Outer stage output byte bound exceeded')

            try:
                with log_path.open('wb') as log:
                    if captured_stdout is not None:
                        capture_stream = captured_stdout.open('xb')
                    process = subprocess.Popen([str(value) for value in command], cwd=ROOT, stdout=capture_stream or log,
                                               stderr=log if captured_stdout is not None else subprocess.STDOUT,
                                               start_new_session=True, env=environment)
                    stop = min(deadline, time.monotonic() + timeout)
                    while process.poll() is None:
                        if time.monotonic() >= stop or capture.stat().st_size > maximum or log_path.stat().st_size > 32 * 1024 * 1024:
                            raise TimeoutError('Outer stage deadline or output byte bound exceeded')
                        time.sleep(0.01)
                    code = process.wait()
                    residual = len(inner.direct_children())
                    at_completion = inner.child_diagnostics()
                    natural_exit = inner.natural_child_exit(stop, check_output)
                    if natural_exit['remainingChildren']:
                        failure = 'Completed outer stage left adopted descendants after natural-exit grace'
            except Exception as error:
                failure = type(error).__name__ + ': ' + str(error)
            finally:
                if capture_stream is not None:
                    capture_stream.close()
                before_cleanup = inner.child_diagnostics()
                try:
                    signals = inner.cleanup_children()
                    if process is not None:
                        process.wait(timeout=5)
                    require(not inner.direct_children(), 'Outer descendants remained after bounded cleanup')
                except Exception as error:
                    receipt['cleanupPoisoned'] = True
                    failure = (failure + '; ' if failure else '') + 'Cleanup failed: ' + str(error)
            if signals and failure is None:
                failure = 'Outer stage required forced descendant cleanup'
            if capture.exists() and capture.stat().st_size > maximum:
                failure = 'Outer captured output byte bound exceeded'
            if log_path.exists() and log_path.stat().st_size > 32 * 1024 * 1024:
                failure = 'Outer log byte bound exceeded'
            receipt['stages'].append({'stage': name, 'command': [str(value) for value in command], 'exitCode': code,
                                      'failure': failure, 'residualChildrenAtCompletion': residual,
                                      'naturalExitGrace': natural_exit, 'childDiagnosticsAtCompletion': at_completion,
                                      'childDiagnosticsBeforeCleanup': before_cleanup,
                                      'cleanupSignals': signals, 'remainingAdoptedChildren': len(inner.direct_children()),
                                      'logSha256': digest(log_path) if log_path.exists() and log_path.stat().st_size else None,
                                      'capturedStdoutSha256': digest(capture) if capture.exists() and capture.stat().st_size else None})
            print(name, code, failure or '', flush=True)
            require(not receipt['cleanupPoisoned'] and failure is None and code == 0, 'Outer stage failed: ' + name)

        records = output / 'public-input-records'
        records.mkdir()
        stage('source-head', ['git', 'rev-parse', 'HEAD'], captured_stdout=records / 'current-head.txt', maximum=4096)
        head = (records / 'current-head.txt').read_text().strip()
        require(len(head) == 40 and all(value in '0123456789abcdef' for value in head), 'Invalid current source revision')
        stage('source-clean', ['git', 'diff', '--exit-code', 'HEAD', '--', '.'], timeout=30)
        receipt['head'] = head
        version = '4.11.0+' + head
        receipt['freshCompilerVersionExpected'] = version
        stage('public-run-metadata', ['gh', 'api', 'repos/erniecohen/dafny/actions/runs/' + str(PUBLIC_RUN)],
              captured_stdout=records / 'run.json', maximum=262144)
        run = json.loads((records / 'run.json').read_text())
        require(run['id'] == PUBLIC_RUN and run['head_sha'] == PUBLIC_HEAD and run['status'] == 'completed' and
                run['event'] == 'workflow_dispatch' and run['path'] == '.github/workflows/review.yml' and run['conclusion'] == 'success',
                'Public run metadata differs; job success does not establish full acceptance')
        stage('public-artifact-metadata', ['gh', 'api', 'repos/erniecohen/dafny/actions/runs/' + str(PUBLIC_RUN) + '/artifacts'],
              captured_stdout=records / 'artifacts.json', maximum=262144)
        listing = json.loads((records / 'artifacts.json').read_text())
        require(listing['total_count'] == 1 and len(listing['artifacts']) == 1, 'Public artifact inventory differs')
        artifact = listing['artifacts'][0]
        require(artifact['id'] == PUBLIC_ARTIFACT and artifact['name'] == PUBLIC_ARTIFACT_NAME and
                artifact['size_in_bytes'] == PUBLIC_ARCHIVE_BYTES and artifact['digest'] == 'sha256:' + PUBLIC_ARCHIVE_SHA and
                artifact['expired'] is False and artifact['workflow_run']['id'] == PUBLIC_RUN and
                artifact['workflow_run']['head_sha'] == PUBLIC_HEAD, 'Public artifact identity differs')
        archive_path = output / 'public-prerequisite.zip'
        stage('public-artifact-download', ['gh', 'api', 'repos/erniecohen/dafny/actions/artifacts/' + str(PUBLIC_ARTIFACT) + '/zip'],
              timeout=240, captured_stdout=archive_path, maximum=256 * 1024 * 1024)
        require(archive_path.stat().st_size == PUBLIC_ARCHIVE_BYTES and digest(archive_path) == PUBLIC_ARCHIVE_SHA,
                'Downloaded public archive differs from the frozen digest/size')
        prior, files = extract_prerequisite(archive_path, output / 'public-prerequisite')
        (records / 'extracted-files.json').write_text(json.dumps(files, indent=2) + '\n')
        solver = prior / 'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
        require(digest(solver) == SOLVER, 'Public prerequisite solver bytes differ')
        solver.chmod(0o755)
        receipt['prerequisiteValidation'] = inner.prerequisite(prior, solver)
        receipt['extractedInputInventorySha256'] = digest(records / 'extracted-files.json')
        dotnet_name = shutil.which('dotnet')
        require(dotnet_name is not None, 'No resolved .NET executable')
        dotnet = Path(dotnet_name).resolve(strict=True)
        receipt['dotnetExecutableSha256'] = digest(dotnet)
        receipt['dotnetExecutable'] = str(dotnet)
        receipt['solverSha256'] = digest(solver)
        inner_environment = {name: value for name, value in os.environ.items() if name not in {'GH_TOKEN', 'GITHUB_TOKEN'}}
        command = ['python3', ROOT / '.github/review/b3-real-triage.py', '--prior-artifacts', prior,
                   '--solver', solver, '--dotnet', dotnet, '--compiler-source', head, '--compiler-version', version,
                   '--output', output / 'real-triage', '--exploratory-rlimits', '1000000,10000000']
        stage('real-triage', command, timeout=2300, environment=inner_environment)
        sealed_inner()  # Neither the download nor build may change the reviewed diagnostic source.
        diagnostic_path = output / 'real-triage/summary.json'
        diagnostic = json.loads(diagnostic_path.read_text())
        require(diagnostic['diagnosticOnly'] is True and diagnostic['fullGateAcceptanceClaimed'] is False and
                diagnostic['libraryReverified'] is False and diagnostic['compilerSourceDeclaredByBuildReceipt'] == head and
                diagnostic['compilerVersion'] == version and diagnostic['cleanupPoisoned'] is False,
                'Incorrect diagnostic receipt boundary')
        receipt['diagnosticReceiptSha256'] = digest(diagnostic_path)
        receipt['diagnostic'] = diagnostic
        receipt['currentCorpusMatched'] = diagnostic['currentCorpusMatched']
        receipt['currentJavaCompiled'] = diagnostic['currentJavaCompiled']
        receipt['originalBaselineAllMatched'] = diagnostic['originalBaselineAllMatched']
        receipt['originalCliAllMatched'] = diagnostic['originalCliAllMatched']
        receipt['diagnosticReceiptProduced'] = True
        # The original warning and inconclusive controls remain strict mismatches.
        # This focus deliberately has no aggregate full-acceptance PASS boolean.
    except Exception as error:
        receipt['failure'] = type(error).__name__ + ': ' + str(error)
        print(receipt['failure'], flush=True)
    finally:
        if inner is not None:
            try:
                remaining = inner.direct_children()
                receipt['remainingAdoptedChildren'] = len(remaining)
                if remaining:
                    receipt['cleanupPoisoned'] = True
                    receipt['finalCleanupSignals'] = inner.cleanup_children()
                    receipt['remainingAdoptedChildren'] = len(inner.direct_children())
            except Exception as error:
                receipt['cleanupPoisoned'] = True
                receipt['cleanupFailure'] = type(error).__name__ + ': ' + str(error)
        output.mkdir(parents=True, exist_ok=True)
        (output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
        summary_path = os.environ.get('GITHUB_STEP_SUMMARY')
        if summary_path:
            with open(summary_path, 'a') as summary:
                summary.write('Real corpus diagnostic: evidence only; no full acceptance claim.\n\n')
                for key in ['diagnosticReceiptProduced', 'currentCorpusMatched', 'currentJavaCompiled',
                            'originalBaselineAllMatched', 'originalCliAllMatched', 'cleanupPoisoned']:
                    summary.write('- ' + key + ': ' + str(receipt.get(key, False)) + '\n')
                if 'failure' in receipt:
                    summary.write('\n' + receipt['failure'] + '\n')
                summary.write('\nThe exact public prerequisite contains 560 passed library obligations but its original full gate was NOT GREEN. No library proof is repeated. Zero exit only uploads this diagnostic receipt; inspect the strict original and exploratory outcomes.\n')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
