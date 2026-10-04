#!/usr/bin/env python3
"""Build a bounded, reproducible archive from explicitly pinned experimental inputs."""
import argparse
import gzip
import hashlib
import io
import json
import os
import subprocess
import tempfile
from pathlib import Path
import re
import stat
import tarfile

ROOT = Path(__file__).resolve().parent.parent
ARCHIVE_MAX_BYTES = 1024 * 1024 * 1024
MAX_BYTES = ARCHIVE_MAX_BYTES - 4 * 1024 * 1024
MAX_FILES = 2048
# Real changes the proof source; record a fresh inspected public receipt before packaging.
LIBRARY_SHA = None
VERIFIED_BATCHES = None
VERIFICATION_EVIDENCE = None
SOURCE_SHA = 'a6ecb742096ddf2f4a6dbcfefb847fdb17ff1fbda7bacf0fafd445f60d843976'
SOLVER_SHA = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
LAUNCHER_BYTES = b'#!/bin/sh\nset -eu\npackage_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\nexec dotnet "$package_root/cli/Dafny.dll" "$@"\n'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_regular(path, limit=MAX_BYTES):
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_size > limit:
        raise ValueError('Require a bounded regular file')
    fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as source:
        initial = os.fstat(source.fileno())
        if not stat.S_ISREG(initial.st_mode) or initial.st_size > limit:
            raise ValueError('Input changed its file type or size')
        chunks, total = [], 0
        while chunk := source.read(min(65536, limit - total + 1)):
            total += len(chunk)
            if total > limit:
                raise ValueError('Input byte bound exceeded')
            chunks.append(chunk)
        final = os.fstat(source.fileno())
        if total != initial.st_size or (final.st_size, final.st_mtime_ns, final.st_ino) != (initial.st_size, initial.st_mtime_ns, initial.st_ino):
            raise ValueError('Input changed while capturing its bytes')
        return b''.join(chunks)


def capture(directory, limit):
    captured, pending, visited, total = {}, [directory], 0, 0
    while pending:
        folder = pending.pop()
        with os.scandir(folder) as children:
            for child in children:
                visited += 1
                if visited > MAX_FILES:
                    raise ValueError('Input inventory bound exceeded')
                if child.is_symlink():
                    raise ValueError('Symlinked package input')
                path = Path(child.path)
                if child.is_dir(follow_symlinks=False):
                    pending.append(path)
                else:
                    data = read_regular(path, limit - total)
                    total += len(data)
                    captured[path.relative_to(directory).as_posix()] = data
    return captured


def require_verification_receipt():
    if not isinstance(LIBRARY_SHA, str) or not re.fullmatch('[0-9a-f]{64}', LIBRARY_SHA) or not isinstance(VERIFIED_BATCHES, int) or VERIFIED_BATCHES <= 0 or not isinstance(VERIFICATION_EVIDENCE, str) or not VERIFICATION_EVIDENCE.startswith('https://github.com/erniecohen/dafny/actions/runs/'):
        raise ValueError('Native Real package requires a fresh inspected library verification receipt')


def build(options):
    require_verification_receipt()
    if not re.fullmatch('[0-9a-f]{40}', options.source_commit):
        raise ValueError('Require the exact source commit used to build this CLI')
    cli = options.cli.resolve(strict=True)
    worker = options.worker.resolve(strict=True)
    destination = options.output.resolve()
    if destination.exists() or destination.is_relative_to(cli) or destination.is_relative_to(worker):
        raise ValueError('Require a new archive path outside the CLI and worker inputs')
    worker_bytes = capture(worker, 512 * 1024 * 1024)
    manifest_data = worker_bytes['b3-worker-manifest.json']
    if len(manifest_data) > 65536:
        raise ValueError('Oversized worker manifest')
    manifest = json.loads(manifest_data)
    for key, expected in {'version': 2, 'b3Commit': 'ea6e8a18dfe9e317d313de769291f989957dc5f2',
                          'normalizerVersion': 'experimental-2',
                          'bootstrapCompiler': '4.11.0+fcb2042d.review.a171069d',
                          'sourceFingerprint': SOURCE_SHA}.items():
        if manifest.get(key) != expected:
            raise ValueError('Worker build identity mismatch: ' + key)
    files = manifest.get('files', {})
    required_worker = {'DafnyB3Host.dll', 'DafnyB3Protocol.dll', 'B3Library.dll',
                       'DafnyB3Host.runtimeconfig.json', 'DafnyB3Host.deps.json'}
    if not 5 <= len(files) <= 32 or not required_worker <= files.keys():
        raise ValueError('Incomplete worker build manifest')
    for name, expected in files.items():
        if Path(name).name != name or '\\' in name or not re.fullmatch('[0-9a-f]{64}', expected):
            raise ValueError('Invalid worker file identity')
        if digest(worker_bytes[name]) != expected or not worker_bytes[name]:
            raise ValueError('Changed or empty worker file: ' + name)
    listed = {name for name in worker_bytes if Path(name).suffix in {'.dll', '.json'}
              and name != 'b3-worker-manifest.json'}
    if listed != files.keys() or files['B3Library.dll'] != LIBRARY_SHA:
        raise ValueError('Unlisted worker dependency or library without the pinned verification receipt')
    worker_runtime = json.loads(worker_bytes['DafnyB3Host.runtimeconfig.json'])['runtimeOptions']
    if worker_runtime['tfm'] != 'net8.0' or worker_runtime['framework']['name'] != 'Microsoft.NETCore.App' or worker_runtime['framework']['version'] != '8.0.0':
        raise ValueError('Require the reviewed .NET 8 worker runtime configuration')
    if digest(read_regular(ROOT / 'ThirdParty/B3/source-manifest.json', 65536)) != SOURCE_SHA:
        raise ValueError('Changed B3 source identity')
    solver_bytes = read_regular(options.solver, 512 * 1024 * 1024)
    if digest(solver_bytes) != SOLVER_SHA:
        raise ValueError('Require the reviewed Linux x64 Z3 5.1.0 executable')
    cli_bytes = capture(cli, MAX_BYTES - sum(map(len, worker_bytes.values())) - len(solver_bytes))
    for name in ['Dafny.dll', 'DafnyDriver.dll', 'DafnyCore.dll', 'Dafny.deps.json', 'Dafny.runtimeconfig.json']:
        if not cli_bytes.get(name):
            raise ValueError('Incomplete CLI package: ' + name)
    # Check the captured entry assembly's reported metadata, not the hosting Python process.
    # A version string is a build declaration, not cryptographic source attestation.
    cli_version = '4.11.0+' + options.source_commit
    with tempfile.TemporaryDirectory(prefix='b3-package-identity-') as temporary:
        snapshot = Path(temporary)
        for name, data in cli_bytes.items():
            path = snapshot / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        result = subprocess.run(['dotnet', str(snapshot / 'Dafny.dll'), '--version'], cwd=snapshot,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
        if result.returncode != 0 or result.stdout.strip() != cli_version:
            raise ValueError('Captured CLI version does not match its declared source commit')
    entries = {}
    total = 0

    def add(name, path=None, data=None, executable=False):
        nonlocal total
        if name in entries or not name.startswith('dafny-b3-experimental/'):
            raise ValueError('Duplicate or invalid archive path')
        if path is not None:
            data = read_regular(path, MAX_BYTES - total)
        total += len(data)
        if total > MAX_BYTES or len(entries) >= MAX_FILES:
            raise ValueError('Package size bound exceeded')
        entries[name] = (data, 0o755 if executable else 0o644)

    prefix = 'dafny-b3-experimental/'
    for captured, target in [(cli_bytes, 'cli/'), (worker_bytes, 'cli/b3/')]:
        for name, data in sorted(captured.items()):
            add(prefix + target + name, data=data)
    add(prefix + 'solver/z3', data=solver_bytes, executable=True)
    add(prefix + 'licenses/Z3-LICENSE.txt', options.solver_license)
    add(prefix + 'licenses/Dafny-LICENSE.txt', ROOT / 'LICENSE.txt')
    add(prefix + 'licenses/B3-LICENSE.txt', ROOT / 'ThirdParty/B3/LICENSE')
    add(prefix + 'dafny', data=LAUNCHER_BYTES, executable=True)
    add(prefix + 'README.md', ROOT / 'docs/dev/b3/experimental-package.md')
    add(prefix + 'support-matrix.json', ROOT / 'docs/dev/b3/support-matrix.json')
    add(prefix + 'discrepancies.json', ROOT / 'docs/dev/b3/discrepancies.json')
    identity = {'schemaVersion': 1, 'status': 'experimental subset; broader acceptance remains tracked',
                'platform': 'Linux x64; glibc 2.39 or newer; .NET 8 runtime required',
                'sourceCommit': options.source_commit, 'cliReportedVersion': cli_version,
                'sourceIdentityBoundary': 'CLI metadata and the source commit are build declarations; review the public build receipt and exact file hashes.', 'productCommit': (ROOT / '.github/review/base').read_text().strip(),
                'b3Commit': manifest['b3Commit'], 'normalizerVersion': manifest['normalizerVersion'],
                'sourceFingerprint': SOURCE_SHA, 'workerManifestSha256': digest(manifest_data),
                'verifiedLibrary': {'sha256': LIBRARY_SHA, 'passedBatches': VERIFIED_BATCHES,
                                    'evidence': VERIFICATION_EVIDENCE},
                'solver': {'version': '5.1.0', 'sha256': SOLVER_SHA},
                'files': {name.removeprefix(prefix): {'sha256': digest(data), 'bytes': len(data), 'mode': mode}
                          for name, (data, mode) in sorted(entries.items())}}
    add(prefix + 'package-manifest.json', data=(json.dumps(identity, indent=2) + '\n').encode())
    options.output.parent.mkdir(parents=True, exist_ok=True)
    temporary_archive = destination.with_name(destination.name + '.partial')
    with temporary_archive.open('xb') as raw, gzip.GzipFile(filename='', mode='wb', fileobj=raw, mtime=0) as compressed:
        with tarfile.open(fileobj=compressed, mode='w') as archive:
            for name, (data, mode) in sorted(entries.items()):
                item = tarfile.TarInfo(name)
                item.size, item.mode, item.mtime = len(data), mode, 0
                archive.addfile(item, io.BytesIO(data))
    if temporary_archive.stat().st_size > ARCHIVE_MAX_BYTES:
        raise ValueError('Compressed archive byte bound exceeded')
    os.replace(temporary_archive, destination)
    with destination.open('rb') as archive:
        archive_sha = hashlib.file_digest(archive, 'sha256').hexdigest()
    print(json.dumps({'archiveSha256': archive_sha, 'files': len(entries), 'bytes': total}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['cli', 'worker', 'solver', 'solver-license', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--source-commit', required=True)
    build(parser.parse_args())
