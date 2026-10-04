#!/usr/bin/env python3
"""Audit and exercise a pinned bundle without restoring/building/downloading dependencies."""
import argparse
import ctypes
import hashlib
import json
import importlib.util
import io
import re
import os
from pathlib import Path
import subprocess
import signal
import time
import tarfile


def digest(data):
    return hashlib.sha256(data).hexdigest()


def direct_children():
    children = set()
    for task in Path('/proc/self/task').iterdir():
        children.update(int(pid) for pid in (task / 'children').read_text().split())
    return children


def cleanup_children():
    # This dedicated checker owns only its CLI invocations. Subreaper adoption retains
    # orphaned workers even when their Unix session differs from the CLI's session.
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
                        # Recheck adoption after opening the handle. The signal targets
                        # this process instance; a reused numeric PID is never killed.
                        if pid in direct_children():
                            signal.pidfd_send_signal(fd, sig)
                            signalled += 1
                    finally:
                        os.close(fd)
                except ProcessLookupError:
                    pass
            time.sleep(0.01)
    raise RuntimeError('Owned CLI descendants remained after bounded cleanup')


def check(options):
    if not Path('/proc/self/task').exists() or not hasattr(os, 'pidfd_open') or not hasattr(signal, 'pidfd_send_signal'):
        raise ValueError('Clean-install acceptance requires Linux pidfd ownership cleanup')
    if direct_children() or ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) != 0:
        raise ValueError('Require an empty dedicated child-subreaper scope')
    options.archive = options.archive.resolve(strict=True)
    options.output = options.output.resolve()
    spec = importlib.util.spec_from_file_location('b3_packager', Path(__file__).with_name('package-b3-experimental.py'))
    packager = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(packager)
    packager.require_verification_receipt()
    archive_bytes = packager.read_regular(options.archive, packager.ARCHIVE_MAX_BYTES)
    if not re.fullmatch('[0-9a-f]{64}', options.archive_sha256) or digest(archive_bytes) != options.archive_sha256:
        raise ValueError('Archive does not match the independently supplied build-receipt digest')
    if options.output.exists():
        raise ValueError('Use a new output directory for each clean-install check')
    options.output.mkdir(parents=True)
    install = options.output / 'clean install λ'
    install.mkdir()
    total = 0
    with tarfile.open(fileobj=io.BytesIO(archive_bytes)) as archive:
        names = set()
        for member in archive:
            if len(names) >= 2048:
                raise ValueError('Archive file bound exceeded')
            total += member.size
            if member.name in names or not member.isfile() or '..' in Path(member.name).parts or member.size < 0 or member.mode not in {0o644, 0o755} or not member.name.startswith('dafny-b3-experimental/') or total > 1024 * 1024 * 1024:
                raise ValueError('Unexpected package archive entry')
            names.add(member.name)
            target = install / member.name
            if not target.resolve().is_relative_to(install.resolve()):
                raise ValueError('Archive path escapes installation')
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.extractfile(member) as source, target.open('xb') as destination:
                while data := source.read(65536):
                    destination.write(data)
            target.chmod(member.mode)
    package = install / 'dafny-b3-experimental'
    manifest_path = package / 'package-manifest.json'
    if manifest_path.stat().st_size > 1024 * 1024:
        raise ValueError('Oversized package inventory')
    manifest = json.loads(manifest_path.read_text())
    required = {'dafny', 'cli/Dafny.dll', 'cli/DafnyDriver.dll', 'cli/DafnyCore.dll',
                'cli/Dafny.deps.json', 'cli/Dafny.runtimeconfig.json', 'cli/b3/DafnyB3Host.dll',
                'cli/b3/DafnyB3Protocol.dll', 'cli/b3/B3Library.dll', 'cli/b3/b3-worker-manifest.json',
                'cli/b3/DafnyB3Host.runtimeconfig.json', 'cli/b3/DafnyB3Host.deps.json',
                'solver/z3', 'support-matrix.json', 'discrepancies.json'}
    if manifest.get('schemaVersion') != 1 or not re.fullmatch('[0-9a-f]{40}', manifest.get('sourceCommit', '')) or not required <= manifest['files'].keys():
        raise ValueError('Incomplete or unsupported package identity')
    if manifest['verifiedLibrary']['sha256'] != packager.LIBRARY_SHA or manifest['verifiedLibrary']['passedBatches'] != packager.VERIFIED_BATCHES or manifest['verifiedLibrary']['evidence'] != packager.VERIFICATION_EVIDENCE or manifest['sourceFingerprint'] != packager.SOURCE_SHA or manifest['solver']['sha256'] != packager.SOLVER_SHA or manifest['solver']['version'] != '5.1.0':
        raise ValueError('Package identity does not match the reviewed input pins')
    expected = set(manifest['files']) | {'package-manifest.json'}
    actual = {p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file()}
    if actual != expected:
        raise ValueError('Unlisted or missing installed file')
    for name, entry in manifest['files'].items():
        target = package / name
        data = target.read_bytes()
        if not target.resolve().is_relative_to(package.resolve()) or len(data) != entry['bytes'] or digest(data) != entry['sha256'] or target.stat().st_mode & 0o777 != entry['mode']:
            raise ValueError('Changed installed package file: ' + name)
    if (package / 'dafny').read_bytes() != packager.LAUNCHER_BYTES or digest((package / 'cli/b3/B3Library.dll').read_bytes()) != packager.LIBRARY_SHA or digest((package / 'solver/z3').read_bytes()) != packager.SOLVER_SHA:
        raise ValueError('Unexpected launcher, library or solver bytes')
    worker = json.loads((package / 'cli/b3/b3-worker-manifest.json').read_text())
    if worker['version'] != 3 or worker['b3Commit'] != manifest['b3Commit'] or worker['sourceFingerprint'] != packager.SOURCE_SHA or worker['normalizerVersion'] != 'experimental-3' or worker['bootstrapCompiler'] != '4.11.0+fcb2042d.review.a171069d':
        raise ValueError('Unsupported installed worker identity')
    for name, sha in worker['files'].items():
        if Path(name).name != name or digest((package / 'cli/b3' / name).read_bytes()) != sha:
            raise ValueError('Changed installed worker dependency')
    receipt = {'schemaVersion': 1, 'archiveSha256': options.archive_sha256,
               'sourceCommit': manifest['sourceCommit'], 'scope': 'experimental clean-install controls',
               'installationPathIncludesSpacesAndUnicode': True, 'dependencyDownloads': 0, 'cleanupScope': 'Dedicated Linux child-subreaper with pidfd-signalled adopted CLI descendants',
               'stages': [], 'passed': False}

    def run(name, arguments, code_expected, required):
        log_path = options.output / (name + '.txt')
        code, failure, residual = None, None, 0
        with log_path.open('wb') as log:
            process = subprocess.Popen([str(package / 'dafny'), *arguments], cwd=package,
                                       stdout=log, stderr=subprocess.STDOUT, start_new_session=True,
                                       env={**os.environ, 'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1'})
            try:
                deadline = time.monotonic() + 90
                while process.poll() is None:
                    if time.monotonic() > deadline or log_path.stat().st_size > 1048576:
                        raise TimeoutError('CLI deadline or output byte bound exceeded')
                    time.sleep(0.01)
                code = process.wait()
                residual = len(direct_children())
                if residual:
                    failure = 'Completed CLI left adopted children'
            except Exception as error:
                failure = str(error)
            finally:
                cleanup_count = cleanup_children()
                process.wait(timeout=5)
        with log_path.open('rb') as captured:
            text = captured.read(1048576).decode('utf-8', errors='replace')
        if log_path.stat().st_size > 1048576:
            failure = 'Output byte bound exceeded'
        diagnostic = '\n'.join(line.split('Error:', 1)[1] for line in text.splitlines() if 'Error:' in line)
        checked_text = diagnostic if code == 4 else text
        matched = failure is None and code == code_expected and all(term in checked_text for term in required)
        matched = matched and 'internal compilation exception' not in text.lower() and 'internal error occurred' not in text.lower()
        if name == 'valid':
            trailer = re.search(r'Dafny program verifier finished with ([0-9]+) verified, 0 errors', text)
            matched = matched and trailer is not None and int(trailer.group(1)) > 0
            matched = matched and not re.search(r'timed out|inconclusive|out of resource|out of memory', text, re.I)
        receipt['stages'].append({'stage': name, 'exitCode': code, 'expectedExitCode': code_expected,
                                  'requiredOutput': required, 'matched': matched, 'failure': failure,
                                  'residualChildrenAtCompletion': residual, 'cleanupSignals': cleanup_count,
                                  'remainingAdoptedChildren': len(direct_children())})
        return matched

    try:
        version = '4.11.0+' + manifest['sourceCommit']
        ok = run('version', ['--version'], 0, [version])
        good = package / 'valid input λ.dfy'
        bad = package / 'invalid input λ.dfy'
        good.write_text('method Good(x: int) { assert x == x; }\n')
        bad.write_text('method Bad() { assert false; }\n')
        common = ['--verification-backend', 'b3', '--b3-worker', str(package / 'cli/b3/DafnyB3Host.dll'),
                  '--solver-path', str(package / 'solver/z3'), '--verification-time-limit', '30', '--cores', '1', '--progress', 'Batch', '--find-project=false']
        ok = run('valid', ['verify', str(good), *common], 0, ['0 errors', 'verified successfully', 'resource count: unavailable']) and ok
        ok = run('invalid', ['verify', str(bad), *common], 4, ['B3 failed:', 'assertion might not hold']) and ok
        receipt['passed'] = ok and len(receipt['stages']) == 3
    finally:
        (options.output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    if not receipt['passed']:
        raise ValueError('Clean-install controls failed; inspect summary.json')
    print('Three strict clean-install controls passed; no dependency restore/build/download performed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--archive-sha256', required=True, help='Digest from the independent builder/public build receipt')
    check(parser.parse_args())
