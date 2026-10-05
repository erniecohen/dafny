"""Bounded current-source integrated structural receipt:557 normalizer+26 neutral.

Diagnostic zero exit preserves a failed receipt. No library/worker/CLI/corpus,
regression/default parity or complete-backend acceptance follows. First Core
and two test snapshots are immutable; failed observations cannot replace them.
"""
import ast
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import selectors
import shutil
import signal
import stat
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path.cwd().resolve()
INPUTS_PATH = Path('.github/review/b3-integrated-structural-inputs.json')
INPUTS_SHA256 = "a4accbe4a3dd1de90bf73cd1edc2f3d0e789279b328fb1db43f691ac5a287f3c"
INPUTS_BYTES = 28318
FOCUSED_CONTROL_COUNT = 583
MAXIMUM_FILE_BYTES = 512 * 1024 * 1024
MAXIMUM_INVENTORY_BYTES = 4 * 1024 * 1024 * 1024
MAXIMUM_INVENTORY_FILES = 40000
MAXIMUM_ARTIFACT_BYTES = 256 * 1024 * 1024
MAXIMUM_TRX_BYTES = 16 * 1024 * 1024
TRX_NS = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
MAXIMUM_STAGE_LOG_BYTES=32 * 1024 * 1024
STAGE_TIMEOUT_SECONDS=1800
NATURAL_CHILD_GRACE_SECONDS=5
MAXIMUM_CHILD_DIAGNOSTICS=64


def digest_bytes(data):
    return hashlib.sha256(data).hexdigest()


def seal(value):
    return digest_bytes(json.dumps(value, sort_keys=True, separators=(',', ':')).encode())


def file_record(path):
    info = path.lstat()
    assert stat.S_ISREG(info.st_mode), 'Require a regular inventory file: ' + str(path)
    assert info.st_size <= MAXIMUM_FILE_BYTES, 'File exceeds inventory bound: ' + str(path)
    digest = hashlib.sha256()
    length = 0
    with path.open('rb') as stream:
        while chunk := stream.read(1024 * 1024):
            length += len(chunk)
            assert length <= MAXIMUM_FILE_BYTES, 'File grew past inventory bound'
            digest.update(chunk)
    assert length == info.st_size, 'Inventory file length changed during hashing'
    return {'bytes': length, 'sha256': digest.hexdigest()}


def directory_records(root):
    assert root.is_dir() and not root.is_symlink(), 'Require a real inventory directory'
    rows = []
    total = 0
    pending = [root]
    while pending:
        directory = pending.pop()
        for path in sorted(directory.iterdir()):
            assert not path.is_symlink(), 'Directory inventory does not follow symlinks'
            if path.is_dir():
                pending.append(path)
            else:
                record = file_record(path)
                total += record['bytes']
                assert total <= MAXIMUM_INVENTORY_BYTES, 'Inventory byte bound exceeded'
                rows.append({'path': path.relative_to(root).as_posix(), **record})
                assert len(rows) <= MAXIMUM_INVENTORY_FILES, 'Inventory file bound exceeded'
    return sorted(rows, key=lambda row: row['path'])


def git_bytes(*args):
    # Metadata tools are bounded and run only in this dedicated owned scope.
    assert not direct_children(), 'Require empty child scope before Git metadata'
    result = subprocess.run(['git', *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            timeout=30, check=True)
    assert not direct_children(), 'Git metadata left an owned child'
    assert len(result.stdout) <= 64 * 1024 * 1024, 'Git metadata byte bound exceeded'
    return result.stdout


def tree_records(revision):
    rows = {}
    for item in git_bytes('ls-tree', '-r', '-z', revision).split(b'\0'):
        if not item:
            continue
        metadata, path_bytes = item.split(b'\t', 1)
        mode, kind, blob = metadata.decode().split()
        path = path_bytes.decode('utf-8')
        assert kind == 'blob' and mode in {'100644', '100755', '120000'}, 'Unexpected source tree entry'
        assert path not in rows and not path.startswith('/') and '..' not in Path(path).parts
        rows[path] = {'mode': mode, 'gitBlob': blob}
    assert len(rows) <= MAXIMUM_INVENTORY_FILES
    return rows


def worktree_records(tree):
    rows = []
    total = 0
    for name, entry in sorted(tree.items()):
        path = Path(name)
        # Never follow a symlink ancestor for tracked file contents.
        assert all(not ancestor.is_symlink() for ancestor in path.parents), 'Source symlink ancestor'
        if entry['mode'] == '120000':
            assert path.is_symlink(), 'Tracked symlink changed kind'
            data = os.readlink(path).encode('utf-8')
        else:
            assert path.is_file() and not path.is_symlink(), 'Tracked regular file changed kind'
            info = path.stat()
            assert info.st_size <= MAXIMUM_FILE_BYTES, 'Source file byte bound exceeded'
            assert bool(info.st_mode & stat.S_IXUSR) == (entry['mode'] == '100755'), 'Tracked executable mode changed'
            git_digest = hashlib.sha1(b'blob ' + str(info.st_size).encode() + b'\0')
            digest = hashlib.sha256()
            length = 0
            with path.open('rb') as stream:
                while chunk := stream.read(1024 * 1024):
                    length += len(chunk)
                    assert length <= MAXIMUM_FILE_BYTES, 'Tracked source grew past byte bound'
                    git_digest.update(chunk)
                    digest.update(chunk)
            assert length == info.st_size, 'Tracked source length changed during hashing'
            blob, sha256 = git_digest.hexdigest(), digest.hexdigest()
        if entry['mode'] == '120000':
            length = len(data)
            blob = hashlib.sha1(b'blob ' + str(length).encode() + b'\0' + data).hexdigest()
            sha256 = digest_bytes(data)
        total += length
        assert total <= MAXIMUM_INVENTORY_BYTES
        untouched = {row['path']: row for row in inputs['untouchedCheckoutFiles']}
        if name in untouched:
            frozen = untouched[name]
            assert entry['mode'] == frozen['mode'] and entry['gitBlob'] == frozen['gitBlob']
            assert length == frozen['checkoutBytes'] and sha256 == frozen['checkoutSha256'], 'Untouched checkout bytes changed'
            byte_kind = 'frozen-nonselected-CRLF-checkout'
        else:
            assert blob == entry['gitBlob'], 'Tracked source differs from committed blob: ' + name
            byte_kind = 'exact-committed-Git-blob'
        rows.append({'path': name, **entry, 'byteKind': byte_kind, 'bytes': length, 'sha256': sha256})
    return rows


def prepare_checkout_bytes(tree):
    declared = inputs['checkoutMaterializations']
    untouched = inputs['untouchedCheckoutFiles']
    assert len(declared) == 43 and len(untouched) == 12
    assert len({row['path'] for row in declared + untouched}) == 55
    assert all(Path(row['path']).suffix in {'.csproj', '.config'} for row in declared)
    assert all(Path(row['path']).suffix in {'.sln', '.bat', '.transcript'} and not row['selectedByThisFocus'] for row in untouched)
    assert not Path('.git/info/attributes').exists(), 'No hidden checkout attribute override'
    raw_attributes = git_bytes('check-attr', '-z', 'eol', '--', *(row['path'] for row in declared + untouched)).split(b'\0')
    assert raw_attributes[-1] == b''
    attrs = {raw_attributes[index].decode(): (raw_attributes[index + 1].decode(), raw_attributes[index + 2].decode())
        for index in range(0, len(raw_attributes) - 1, 3)}
    assert len(attrs) == 55 and all(value == ('eol', 'crlf') for value in attrs.values())
    receipts = []
    for row in declared + untouched:
        path = Path(row['path'])
        assert tree[row['path']] == {'mode': row['mode'], 'gitBlob': row['gitBlob']}
        assert row['mode'] in {'100644', '100755'} and all(not ancestor.is_symlink() for ancestor in path.parents)
        before = file_record(path)
        raw = path.read_bytes()
        assert len(raw) <= MAXIMUM_FILE_BYTES and len(raw) == before['bytes']
        if row in untouched:
            assert before == {'bytes': row['checkoutBytes'], 'sha256': row['checkoutSha256']}
            canonical = raw.replace(b'\r\n', b'\n')
            disposition = 'untouched-nonselected-exact-checkout-bytes'
        else:
            assert before in [{'bytes': row['checkoutBytes'], 'sha256': row['checkoutSha256']},
                              {'bytes': row['gitBytes'], 'sha256': row['gitSha256']}]
            canonical = raw.replace(b'\r\n', b'\n') if before['sha256'] == row['checkoutSha256'] else raw
            assert len(canonical) == row['gitBytes'] and digest_bytes(canonical) == row['gitSha256']
            assert hashlib.sha1(b'blob ' + str(len(canonical)).encode() + b'\0' + canonical).hexdigest() == row['gitBlob']
            if raw != canonical:
                path.write_bytes(canonical)
            disposition = 'materialized-exact-Git-blob' if raw != canonical else 'already-exact-Git-blob'
        assert len(canonical) == row['gitBytes'] and digest_bytes(canonical) == row['gitSha256']
        assert hashlib.sha1(b'blob ' + str(len(canonical)).encode() + b'\0' + canonical).hexdigest() == row['gitBlob']
        after = file_record(path)
        assert (path.stat().st_mode & stat.S_IXUSR != 0) == (row['mode'] == '100755')
        receipts.append({'path': row['path'], 'mode': row['mode'], 'gitBlob': row['gitBlob'],
            'attribute': attrs[row['path']][1], 'selectedByThisFocus': row['selectedByThisFocus'],
            'gitBytes': row['gitBytes'], 'gitSha256': row['gitSha256'], 'before': before,
            'after': after, 'disposition': disposition})
    return receipts


def captured_pinned_bytes(path, expected):
    assert expected['bytes'] <= 16 * 1024 * 1024, 'Pinned metadata exceeds capture bound'
    assert all(not ancestor.is_symlink() for ancestor in path.parents), 'Pinned metadata symlink ancestor'
    info = path.lstat()
    assert stat.S_ISREG(info.st_mode) and info.st_size == expected['bytes'], 'Pinned metadata kind/length differs'
    descriptor = os.open(path, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW)
    with os.fdopen(descriptor, 'rb') as stream:
        observed = os.fstat(stream.fileno())
        assert stat.S_ISREG(observed.st_mode) and (observed.st_dev, observed.st_ino) == (info.st_dev, info.st_ino)
        data = stream.read(expected['bytes'] + 1)
    assert len(data) == expected['bytes'] and digest_bytes(data) == expected['sha256'], 'Pinned metadata bytes differ'
    return data


def capture_toolchain():
    lexical = Path(shutil.which('dotnet') or '')
    assert lexical.is_file(), 'dotnet executable is absent'
    executable = lexical.resolve(strict=True)
    root = executable.parent
    roots = [root / 'host' / 'fxr']
    roots.extend(sorted(path for path in (root / 'sdk').iterdir() if path.name.startswith('8.') and path.is_dir()))
    for family in ['Microsoft.NETCore.App', 'Microsoft.AspNetCore.App']:
        parent = root / 'shared' / family
        if parent.exists():
            roots.extend(sorted(path for path in parent.iterdir() if path.name.startswith('8.') and path.is_dir()))
    assert any(path.parent.name == 'sdk' for path in roots), 'Require installed .NET 8 SDK'
    assert any(path.parent.name == 'Microsoft.NETCore.App' for path in roots), 'Require installed .NET 8 runtime'
    directories = []
    total = 0
    count = 0
    for path in roots:
        rows = directory_records(path)
        total += sum(row['bytes'] for row in rows)
        count += len(rows)
        assert total <= MAXIMUM_INVENTORY_BYTES and count <= MAXIMUM_INVENTORY_FILES
        directories.append({'path': str(path), 'files': rows, 'sealSha256': seal(rows)})
    # Resolve the chosen executable and bind its ancestor identities as well.
    links = []
    for path in [lexical, *lexical.parents]:
        if path.is_symlink():
            links.append({'path': str(path), 'link': os.readlink(path), 'resolved': str(path.resolve(strict=True))})
    return {'executable': {'lexicalPath': str(lexical), 'resolvedPath': str(executable), **file_record(executable)},
            'ancestorSymlinks': links, 'directories': directories, 'totalBytes': total, 'fileCount': count}


def package_records():
    rows = []
    for line in Path('Scripts/boogie-packages.sha256').read_text().splitlines():
        digest, name = line.split()
        assert re.fullmatch('[0-9a-f]{64}', digest) and '/' not in name
        record = file_record(Path('Binaries/boogie-packages') / name)
        assert record['sha256'] == digest, 'Pinned Boogie package differs: ' + name
        rows.append({'path': name, **record})
    assert len(rows) == 14 and len({row['path'] for row in rows}) == 14
    return rows


def pinned_boogie_assembly_entries():
    expected = {}
    for package in package_receipt:
        with zipfile.ZipFile(Path('Binaries/boogie-packages') / package['path']) as archive:
            entries = archive.infolist()
            assert len(entries) <= 4096, 'Pinned package entry bound exceeded'
            for entry in entries:
                name = Path(entry.filename).name
                if not name.startswith('Boogie') or not name.endswith('.dll'):
                    continue
                assert entry.file_size <= 64 * 1024 * 1024, 'Pinned assembly byte bound exceeded'
                digest = hashlib.sha256()
                length = 0
                with archive.open(entry) as stream:
                    while chunk := stream.read(1024 * 1024):
                        length += len(chunk)
                        assert length <= entry.file_size
                        digest.update(chunk)
                assert length == entry.file_size
                expected.setdefault(name, []).append({'package': package['path'], 'entry': entry.filename,
                    'bytes': length, 'sha256': digest.hexdigest()})
    return expected


def verify_boogie_assemblies(directory):
    expected = pinned_boogie_assembly_entries()
    actual = [path for path in sorted(directory.glob('Boogie*.dll')) if path.is_file()]
    assert {'Boogie.Core.dll', 'Boogie.ExecutionEngine.dll', 'Boogie.VCGeneration.dll'} <= {path.name for path in actual}
    rows = []
    for path in actual:
        record = file_record(path)
        matches = [entry for entry in expected.get(path.name, []) if
            entry['bytes'] == record['bytes'] and entry['sha256'] == record['sha256']]
        assert matches, 'Test runtime uses an assembly outside the byte-pinned fork packages: ' + path.name
        rows.append({'assembly': path.name, **record, 'matchingPinnedPackageEntries': matches})
    return rows


def capture_core_dependency_inputs():
    # SDK library builds need not copy NuGet DLLs to the library output. Bind
    # the actual SDK-resolved compile/runtime files without changing that default.
    asset_path = Path('Source/DafnyCore/obj/project.assets.json')
    asset_record = file_record(asset_path)
    assert asset_record['bytes'] <= 16 * 1024 * 1024
    assets = json.loads(asset_path.read_text())
    assert assets['version'] == 3 and len(assets['targets']) == 1
    project = assets['project']
    restore = project['restore']
    expected_project = (ROOT / 'Source/DafnyCore/DafnyCore.csproj').resolve()
    assert Path(restore['projectPath']).resolve() == expected_project
    assert Path(restore['projectUniqueName']).resolve() == expected_project
    assert restore['projectName'] == 'DafnyCore' and restore['projectStyle'] == 'PackageReference'
    assert restore['originalTargetFrameworks'] == ['net8.0'] and set(project['frameworks']) == {'net8.0'}
    assert set(restore['frameworks']) == {'net8.0'}
    target_name, target = next(iter(assets['targets'].items()))
    assert target_name in {'net8.0', '.NETCoreApp,Version=v8.0'}
    assert len(assets['packageFolders']) == 1
    package_root = Path(next(iter(assets['packageFolders'])))
    assert package_root.is_dir() and not package_root.is_symlink()
    assert Path(restore['packagesPath']).resolve() == package_root.resolve()
    pinned_boogie = pinned_boogie_assembly_entries()
    rows = []
    projects = []
    total = 0
    for library, declarations in sorted(target.items()):
        metadata = assets['libraries'][library]
        assert declarations['type'] == metadata['type']
        if metadata['type'] != 'package':
            assert metadata['type'] == 'project'
            project_path = (expected_project.parent / metadata['msbuildProject']).resolve()
            assert project_path in {(ROOT / 'Source/DafnyB3Protocol/DafnyB3Protocol.csproj').resolve(),
                (ROOT / 'Source/DafnyRuntime/DafnyRuntime.csproj').resolve()}, 'Unknown Core project reference'
            projects.append({'library': library, 'projectPath': str(project_path),
                'targetFramework': declarations['framework'], 'source': file_record(project_path)})
            continue
        library_path = Path(metadata['path'])
        assert not library_path.is_absolute() and '..' not in library_path.parts
        for kind in ['compile', 'runtime']:
            for value in sorted(declarations.get(kind, {})):
                if not value.endswith('.dll'):
                    continue
                relative = Path(value)
                assert not relative.is_absolute() and '..' not in relative.parts
                path = package_root / library_path / relative
                assert all(not ancestor.is_symlink() for ancestor in path.parents)
                record = file_record(path)
                total += record['bytes']
                assert total <= MAXIMUM_INVENTORY_BYTES and len(rows) < MAXIMUM_INVENTORY_FILES
                row = {'library': library, 'assetKind': kind, 'assetPath': value,
                       'actualPath': str(path), **record}
                if library.startswith('Boogie'):
                    assert library.rsplit('/', 1)[1] == '3.5.5-review.37e4435d', 'Unexpected Boogie package version'
                    matches = [entry for entry in pinned_boogie.get(path.name, []) if
                        entry['bytes'] == record['bytes'] and entry['sha256'] == record['sha256']]
                    assert matches, 'SDK-selected Boogie input is outside the pinned fork package bytes'
                    row['matchingPinnedPackageEntries'] = matches
                rows.append(row)
    assert {row['library'].split('/')[0] for row in projects} == {'DafnyB3Protocol', 'DafnyRuntime'} and len(projects) == 2
    assert {'Boogie.Core', 'Boogie.ExecutionEngine', 'Boogie.VCGeneration'} <= {
        row['library'].split('/')[0] for row in rows if row['assetKind'] == 'compile'}
    return {'assetsFile': {'path': str(asset_path), **asset_record}, 'target': target_name,
            'packageRoot': str(package_root), 'projectReferences': projects, 'files': rows, 'sealSha256': seal(rows)}


def generated_records(root, required):
    rows = directory_records(root)
    by_path = {row['path']: row for row in rows}
    assert set(required) <= by_path.keys(), 'Current build lacks required assembly/config/prelude'
    return rows


def direct_children():
    children = set()
    for task in Path('/proc/self/task').iterdir():
        children.update(int(pid) for pid in (task / 'children').read_text().split())
    return children


def cleanup_children():
    # This dedicated receipt process owns only its stage invocations. Subreaper
    # adoption retains orphaned children even when their Unix sessions differ.
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


def reap_exited_children():
 while True:
  try:
   if os.waitpid(-1,os.WNOHANG)[0]==0:
    return
  except ChildProcessError:
   return


def reap_completed_adopted_children(stage_pid, receipt):
 # Leave the Popen root exclusively owned by Popen so its real exit status is
 # preserved. Orphaned exited grandchildren must be reaped while a live test
 # host is checking that terminated workers are gone, not only after that host.
 for pid in sorted(direct_children()):
  if pid==stage_pid:
   continue
  try:
   reaped,status=os.waitpid(pid,os.WNOHANG)
  except (ChildProcessError,ProcessLookupError,InterruptedError):
   continue
  if reaped:
   receipt['count']+=1
   assert receipt['count']<=4096, 'Adopted-child reap count exceeds stage bound'
   if len(receipt['records'])<MAXIMUM_CHILD_DIAGNOSTICS:
    receipt['records'].append({'pid':reaped,'waitStatus':status})
   else:
    receipt['recordsTruncated']=True


def child_diagnostics(stage_pid):
 children=sorted(direct_children())
 records=[]
 for pid in children[:MAXIMUM_CHILD_DIAGNOSTICS]:
  record={'pid':pid,'kind':'stage-root' if pid==stage_pid else 'adopted-child'}
  try:
   with (Path('/proc')/str(pid)/'stat').open('rb') as stream:
    stat=stream.read(4096)
   suffix=stat[stat.rindex(b')')+2:].split()
   record['startTimeTicks']=int(suffix[19])
   with (Path('/proc')/str(pid)/'comm').open('rb') as stream:
    record['comm']=stream.read(128).decode('utf-8',errors='replace').rstrip('\n')
   with (Path('/proc')/str(pid)/'cmdline').open('rb') as stream:
    command=stream.read(4097)
   record['cmdline']=command[:4096].replace(b'\0',b' ').decode('utf-8',errors='replace')
   record['cmdlineTruncated']=len(command)>4096
   record['sessionId']=os.getsid(pid)
   record['stillOwned']=pid in direct_children()
  except Exception as error:
   record['inspectionError']=(type(error).__name__+': '+str(error))[:256]
  records.append(record)
 return {'childCount':len(children),'records':records,'recordsTruncated':len(children)>MAXIMUM_CHILD_DIAGNOSTICS}


def run_stage(name, command):
 global cleanup_poisoned
 log_path=output/(name+'.txt')
 actual_code=None
 failure=None
 process=None
 residual=residual_before_grace=None
 cleanup_count=remaining=0
 before_cleanup={'childCount':0,'records':[],'recordsTruncated':False}
 adopted_reaps={'count':0,'records':[],'recordsTruncated':False}
 try:
  assert not cleanup_poisoned and not direct_children(), 'Require empty owned child scope before each stage'
  with log_path.open('wb') as log:
   process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,start_new_session=True,
     cwd="ThirdParty/B3" if name in {"worker-runtime","worker-java"} else None,env=stage_environment)
   deadline=time.monotonic()+STAGE_TIMEOUT_SECONDS
   while process.poll() is None:
    reap_completed_adopted_children(process.pid,adopted_reaps)
    if time.monotonic()>=deadline or log_path.stat().st_size>MAXIMUM_STAGE_LOG_BYTES:
     raise TimeoutError('Stage safety deadline or output byte bound exceeded')
    time.sleep(0.01)
   actual_code=process.wait()
   if time.monotonic()>=deadline:
    raise TimeoutError('Stage safety deadline exceeded')
   residual_before_grace=len(direct_children())
   if residual_before_grace:
    grace_deadline=min(deadline,time.monotonic()+NATURAL_CHILD_GRACE_SECONDS)
    while time.monotonic()<grace_deadline:
     if log_path.stat().st_size>MAXIMUM_STAGE_LOG_BYTES:
      raise TimeoutError('Stage output byte bound exceeded during natural-exit grace')
     reap_exited_children()
     if not direct_children():
      break
     time.sleep(0.01)
    reap_exited_children()
   if time.monotonic()>=deadline:
    raise TimeoutError('Stage safety deadline exceeded during natural-exit grace')
   residual=len(direct_children())
   if residual:
    failure='Completed stage left adopted children after bounded natural-exit grace'
 except Exception as error:
  failure=type(error).__name__+': '+str(error)
 finally:
  try:
   before_cleanup=child_diagnostics(process.pid if process is not None else None)
   if before_cleanup['childCount'] and failure is None:
    failure='Stage left adopted children before bounded cleanup'
  except Exception as error:
   before_cleanup={'inspectionError':(type(error).__name__+': '+str(error))[:256]}
  try:
   cleanup_count=cleanup_children()
   if process is not None:
    process.wait(timeout=5)
   remaining=len(direct_children())
   assert remaining==0, 'Owned stage children remained after bounded cleanup'
  except Exception as error:
   cleanup_poisoned=True
   cleanup_count=None # The bounded helper did not return its final signal count.
   failure=(failure+'; ' if failure else '')+'Cleanup failed: '+str(error)
   try:
    remaining=len(direct_children())
   except Exception:
    remaining=None
 if cleanup_count and failure is None:
  failure='Stage required forced adopted-child cleanup'
 log_bytes=log_path.stat().st_size if log_path.exists() else 0
 truncated=log_bytes>MAXIMUM_STAGE_LOG_BYTES
 if truncated:
  failure=(failure+'; ' if failure else '')+'Output byte bound exceeded'
  # Keep a bounded failed-stage log prefix after the cleanup attempt.
  with log_path.open('r+b') as log:
   log.truncate(MAXIMUM_STAGE_LOG_BYTES)
 log_prefix=b''
 if log_path.exists():
  with log_path.open('rb') as log:
   log_prefix=log.read(MAXIMUM_STAGE_LOG_BYTES)
 code=actual_code if failure is None else 124 if failure.startswith('TimeoutError:') else 2
 if code==0:
  try:
   validate(name)
  except Exception as error:
   failure=type(error).__name__+': '+str(error)
   code=1
 return {'stage':name,'exitCode':code,'actualProcessExitCode':actual_code,'command':command,
   'failure':failure,'naturalChildReapsDuringStage':adopted_reaps,'residualChildrenBeforeGrace':residual_before_grace,
   'residualChildrenAtCompletion':residual,'cleanupSignals':cleanup_count,
   'naturalChildGraceSeconds':NATURAL_CHILD_GRACE_SECONDS,'childrenBeforeCleanup':before_cleanup,
   'remainingAdoptedChildren':remaining,'cleanupPoisoned':cleanup_poisoned,
   'maximumLogBytes':MAXIMUM_STAGE_LOG_BYTES,'observedLogBytes':log_bytes,'logTruncated':truncated,
   'safetyDeadlineSeconds':STAGE_TIMEOUT_SECONDS,
   'hashedLogBytes':len(log_prefix),'logSha256':hashlib.sha256(log_prefix).hexdigest() if log_path.exists() else None}


def bounded_tool_stdout(command):
    # Every child belongs to the enclosing source-head owned stage. A failed
    # capture leaves cleanup to that stage's reviewed adoption/pidfd helper.
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    chunks = []
    length = 0
    deadline = time.monotonic() + 60
    with selectors.DefaultSelector() as selected:
        selected.register(process.stdout, selectors.EVENT_READ)
        while selected.get_map():
            assert time.monotonic() < deadline, 'Metadata/SDK selection tool deadline exceeded'
            for key, _ in selected.select(timeout=0.01):
                chunk = os.read(key.fd, 8192)
                if not chunk:
                    selected.unregister(key.fileobj)
                    break
                length += len(chunk)
                assert length <= 1024 * 1024, 'Metadata/SDK selection output byte bound exceeded'
                chunks.append(chunk)
    assert process.wait(timeout=max(0.01, deadline - time.monotonic())) == 0, 'Metadata/SDK selection tool failed'
    return b''.join(chunks).decode('utf-8').strip()


def source_head_child():
    values = {}
    for key, command in [('head', ['git', 'rev-parse', 'HEAD']),
                         ('sdkVersion', ['dotnet', '--version']),
                         ('runtimeSelectionInfo', ['dotnet', '--info']),
                         ('installedRuntimes', ['dotnet', '--list-runtimes'])]:
        values[key] = bounded_tool_stdout(command)
    print(json.dumps(values))


def copy_inventory_snapshot(directory, destination, rows):
    # Freeze only this finite regular-file inventory. Partial copies are retained
    # for diagnosis but never marked complete or qualified by the caller.
    assert directory_records(directory) == rows, 'Compiled outputs changed before snapshot'
    assert all(not path.is_symlink() for path in [directory, *directory.parents])
    assert not destination.exists() and not destination.is_symlink(), 'Snapshot already exists'
    assert all(not path.is_symlink() for path in destination.parents)
    expected_bytes = sum(row['bytes'] for row in directory_records(output)) + sum(row['bytes'] for row in rows)
    assert expected_bytes < MAXIMUM_ARTIFACT_BYTES - 4 * 1024 * 1024, 'Complete compiled snapshot exceeds aggregate bound'
    destination.mkdir(parents=True)
    for row in rows:
        relative = Path(row['path'])
        assert not relative.is_absolute() and relative.as_posix() == row['path']
        assert relative.parts and all(part not in {'.', '..'} for part in relative.parts)
        source = directory / relative
        target = destination / relative
        assert all(not path.is_symlink() for path in source.parents)
        assert file_record(source) == {key: row[key] for key in ['bytes', 'sha256']}, 'Snapshot source bytes changed'
        target.parent.mkdir(parents=True, exist_ok=True)
        digest = hashlib.sha256()
        length = 0
        with source.open('rb') as original, target.open('xb') as copied:
            while chunk := original.read(1024 * 1024):
                length += len(chunk)
                assert length <= row['bytes'], 'Snapshot source grew beyond its frozen byte count'
                digest.update(chunk)
                copied.write(chunk)
        assert length == row['bytes'] and digest.hexdigest() == row['sha256'], 'Snapshot bytes changed during copy'
    assert directory_records(directory) == rows, 'Compiled outputs changed during snapshot'
    assert directory_records(destination) == rows, 'Preserved compiled snapshot differs'


def preserve_compiled_snapshot(stage, role, directory, rows):
    assert not any(row['role'] == role for row in compiled_snapshots), 'First compiled pins cannot be replaced'
    destination = output / 'compiled' / (stage + '-' + role)
    record = {'stage': stage, 'role': role, 'accepted': False, 'copyComplete': False,
              'artifactPath': destination.relative_to(output).as_posix(), 'files': rows,
              'sealSha256': seal(rows), 'copyFailure': None, 'finalSnapshotValidated': False}
    compiled_snapshots.append(record)
    try:
        copy_inventory_snapshot(directory, destination, rows)
        record['copyComplete'] = True
    except Exception as error:
        record['copyFailure'] = type(error).__name__ + ': ' + str(error)
        raise


def inventory_delta(expected, observed):
    before = {row['path']: row for row in expected or []}
    after = {row['path']: row for row in observed or []}
    result = []
    for path in sorted(before.keys() | after.keys()):
        if before.get(path) != after.get(path):
            result.append({'path': path, 'change': 'added' if path not in before else 'removed' if path not in after else 'changed',
                           'initial': before.get(path), 'observed': after.get(path)})
    return result


def table_seal(value):
    # The frozen English/data tables include one final LF; artifact seals retain
    # the borrowed no-LF serialization and identify that separate contract.
    return digest_bytes((json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode())


def split_literals(source):
    result, start, quoted, escaped = [], 0, False, False
    for index, character in enumerate(source):
        if quoted:
            if escaped:
                escaped = False
            elif character == '\\':
                escaped = True
            elif character == '"':
                quoted = False
        elif character == '"':
            quoted = True
        elif character == ',':
            result.append(source[start:index].strip())
            start = index + 1
    assert not quoted and not escaped, 'Incomplete fixed C# literal'
    if source.strip():
        result.append(source[start:].strip())
    return result


def decode_literal(token):
    escapes = {'0': '\0', 'a': '\a', 'b': '\b', 'f': '\f', 'n': '\n', 'r': '\r',
               't': '\t', 'v': '\v', '\\': '\\', '"': '"'}
    if token.startswith('"'):
        assert token.endswith('"')
        result, index = '', 1
        while index < len(token) - 1:
            character = token[index]
            index += 1
            if character == '\\':
                assert index < len(token) - 1 and token[index] in escapes
                character = escapes[token[index]]
                index += 1
            else:
                assert character != '"'
            result += character
        assert all(ord(character) < 128 for character in result), 'Non-ASCII fixed literal'
        return result
    if token in {'true', 'false'}:
        return token == 'true'
    if token in {'int.MinValue', 'Int32.MinValue'}:
        return -2147483648
    if token in {'int.MaxValue', 'Int32.MaxValue'}:
        return 2147483647
    if re.fullmatch('-?(0|[1-9][0-9]*)', token):
        value = int(token)
        assert -2147483648 <= value <= 2147483647
        return value
    kind, name = token.rsplit('.', 1)
    assert kind in census['closedEnumDeclarations'] and name in census['closedEnumDeclarations'][kind]['members']
    return {'csharpType': kind, 'kind': 'enum', 'name': name}


def render_argument(value, declared_type):
    if declared_type == 'bool':
        assert type(value) is bool
        return 'True' if value else 'False'
    if declared_type == 'int':
        assert type(value) is int and -2147483648 <= value <= 2147483647
        return str(value)
    if declared_type in census['closedEnumDeclarations']:
        assert value['csharpType'] == declared_type and value['kind'] == 'enum'
        assert value['name'] in census['closedEnumDeclarations'][declared_type]['members']
        return value['name']
    assert declared_type == 'string' and type(value) is str and all(ord(c) < 128 for c in value)
    escapes = {'\0': '0', '\a': 'a', '\b': 'b', '\f': 'f', '\n': 'n', '\r': 'r',
               '\t': 't', '\v': 'v', '\\': '\\'}
    escaped = ''.join('\\' + escapes[c] if c in escapes else
                      '\\x' + format(ord(c), '02x') if ord(c) < 32 else c for c in value)
    escaped = escaped.replace('"', '\\"')
    return '"' + escaped[:50] + '"' + ('...' if len(escaped) > 50 else '')


def validate_source_census():
    assert census['suiteCounts'] == {'normalizer': 557, 'neutral': 26}
    assert census['methodCounts'] == {'normalizer': 263, 'neutral': 15}
    methods, cases = census['methods'], census['cases']
    assert len(methods) == 278 and len(cases) == len({r['displayName'] for r in cases}) == 583
    assert table_seal(methods) == census['methodSealSha256']
    assert table_seal(cases) == census['caseSealSha256']
    by_method = {}
    for row in cases:
        by_method.setdefault((row['className'], row['method']), []).append(row)
    assert len(by_method) == 278
    sources = {row['path']: Path(row['path']).read_text(encoding='utf-8-sig')
               for row in census['sourceFiles']}
    for method in methods:
        text = sources[method['path']]
        found = re.search(r'\bpublic\s+(?:async\s+)?(?:void|Task)\s+' + re.escape(method['method']) +
                          r'\((.*?)\)\s*(?:\{|=>)', text, re.S)
        assert found is not None and found[1] == method['signature'], 'Selected method signature changed'
        previous = text[:found.start()].splitlines()
        while previous and not previous[-1].strip():
            previous.pop()
        attributes = []
        while previous and previous[-1].strip().startswith('['):
            attributes.insert(0, previous.pop().strip())
        assert attributes == method['attributes'], 'Selected attributes changed'
        parameters = [part.split() for part in found[1].split(',')] if found[1].strip() else []
        assert all(len(part) == 2 for part in parameters)
        assert [part[0] for part in parameters] == method['parameterTypes']
        assert [part[1] for part in parameters] == method['parameterNames']
        rows = by_method[(method['className'], method['method'])]
        assert len(rows) == method['count'] and [r['dataRow'] for r in rows] == list(range(method['count']))
        if method['inlineDataSource']:
            arguments = [[decode_literal(token) for token in split_literals(raw)]
                         for raw in method['inlineDataSource']]
        elif method['memberData']:
            assert method['memberData'] == ['[MemberData(nameof(MapTheoryInputs))]'], 'Unknown data producer'
            manifest = json.loads(Path('Source/DafnyB3Normalizer.Test/MapTheoryInputs/cases.json').read_text())
            arguments = [[row[key] for key in ['file', 'expectedAxioms', 'expectedChecks', 'expectedHelpers',
                          'expectedObservations', 'containsLiteralFalse']] for row in manifest['cases']]
            assert len(arguments) == 17
        else:
            assert attributes == ['[Fact]'] and not parameters
            arguments = [[]]
        assert arguments == [row['arguments'] for row in rows], 'Fixed ordered argument rows differ'
        for row in rows:
            assert row['parameterNames'] == method['parameterNames'] and row['parameterTypes'] == method['parameterTypes']
            assert row['suite'] == method['suite'] and len(row['arguments']) == len(parameters)
            display = method['className'] + '.' + method['method']
            if parameters:
                display += '(' + ', '.join(name + ': ' + render_argument(value, kind)
                    for name, kind, value in zip(method['parameterNames'], method['parameterTypes'], row['arguments'])) + ')'
            assert display == row['displayName'], 'Exact xUnit display differs'
    for row in census['sourceFiles']:
        if 'methodCount' not in row:
            continue
        selected = [m for m in methods if m['path'] == row['path']]
        counts = {key: len(re.findall(r'^\s*\[' + key + r'(?:\(|\])', sources[row['path']], re.M))
                  for key in ['Fact', 'Theory', 'InlineData', 'MemberData']}
        assert {key: value for key, value in counts.items() if value} == row['attributes']
        assert len(selected) == row['methodCount']
        assert sum('[Fact]' in m['attributes'] for m in selected) == counts['Fact']
        assert sum('[Theory]' in m['attributes'] for m in selected) == counts['Theory']
    for declaration in census['closedEnumDeclarations'].values():
        text = Path(declaration['sourcePath']).read_text(encoding='utf-8-sig')
        found = re.search(r'\benum\s+' + declaration['enumName'] + r'\s*\{([^}]*)\}', text, re.S)
        assert found is not None and '[Flags]' not in text[max(0, found.start() - 100):found.start()]
        members = [part.strip() for part in re.sub(r'//[^\n]*', '', found[1]).split(',') if part.strip()]
        assert members == declaration['members']
    return {'methods': len(methods), 'cases': len(cases), 'methodSealSha256': census['methodSealSha256'],
            'caseSealSha256': census['caseSealSha256'], 'bodyExecutionForCensus': False}


def validate_copied_helper():
    text = Path(__file__).read_text()
    functions = {node.name: node for node in ast.parse(text).body if isinstance(node, ast.FunctionDef)}
    actual = []
    for name in inputs['copiedOwnershipFunctions']:
        node = functions[name]
        body = ''.join(text.splitlines(keepends=True)[node.lineno - 1:node.end_lineno]).encode()
        actual.append({'function': name, 'bytes': len(body), 'sha256': digest_bytes(body)})
    assert actual == inputs['copiedOwnershipFunctionRecords'], 'Reviewed six ownership bodies changed'
    expected = command_data['borrowedOwnershipCoordinator']
    assert expected['requiredByteIdenticalSixFunctions'] == inputs['copiedOwnershipFunctions']
    assert actual == [{'function': row['name'], 'bytes': row['bytes'], 'sha256': row['sha256']}
        for name in inputs['copiedOwnershipFunctions'] for row in expected['functionRecords'] if row['name'] == name]
    return actual


def validate_selection():
    assert os.environ.get('GITHUB_EVENT_NAME') == 'workflow_dispatch'
    assert os.environ.get('GITHUB_REF', '').startswith('refs/heads/scratch/')
    expected = {'b3_integrated_structural_focus': 'true', 'binaries': 'false',
                'resolver_probe': 'false', 'additional_axioms_probe': 'false'}
    event_path = Path(os.environ['GITHUB_EVENT_PATH'])
    event_record = file_record(event_path)
    event = json.loads(captured_pinned_bytes(event_path, event_record))
    actual = {key: str(event.get('inputs', {}).get(key, '')).lower() for key in expected}
    assert actual == expected and command_data['hypotheticalRoute'] == expected
    for key, value in expected.items():
        assert os.environ.get('B3_INPUT_' + key.upper(), '').lower() == value
    return {'event': 'workflow_dispatch', 'ref': os.environ['GITHUB_REF'], 'inputs': actual, 'eventFile': event_record}


def validate_source_initial():
    global head, source_tree, source_rows, toolchain_rows
    assert git_bytes('status', '--porcelain', '--untracked-files=all') == b'', 'Require clean initial checkout'
    head = git_bytes('rev-parse', 'HEAD').decode().strip()
    assert re.fullmatch('[0-9a-f]{40}', head) and head == os.environ.get('GITHUB_SHA')
    git_bytes('merge-base', '--is-ancestor', inputs['implementationHead'], head)
    assert git_bytes('rev-parse', inputs['implementationHead'] + '^{tree}').decode().strip() == inputs['implementationTree']
    assert census['head'] == command_data['productHead'] == inputs['implementationHead']
    assert census['sourceTree'] == inputs['implementationTree']
    assert census['productCommit'] == command_data['productCommit'] == inputs['productCommit']
    assert Path('.github/review/base').read_text() == 'v4.11.0 ' + inputs['productCommit'] + '\n'
    exclusions = [':(exclude)' + name for name in ['Source/IntegrationTests', 'docs', '.github',
                                                  'REVIEW.md', 'ROADMAP.md', 'AGENTS.md', 'CLAUDE.md']]
    assert git_bytes('diff', '--name-only', inputs['productCommit'], 'HEAD', '--', '.', *exclusions) == b''
    source_tree = tree_records('HEAD')
    parent_tree = tree_records(inputs['implementationHead'])
    changed = {name for name in source_tree.keys() | parent_tree.keys() if source_tree.get(name) != parent_tree.get(name)}
    assert changed == set(inputs['allowedRoutingFiles']), 'Unexpected routing source delta'
    pins = {}
    for group in ['sourceFiles', 'additionalSourcePins', 'buildAndPackagePins', 'fixturePins']:
        rows = census[group]
        assert table_seal(rows) == census[group + 'SealSha256']
        for row in rows:
            if row['path'] in pins:
                assert all(pins[row['path']][key] == row[key] for key in
                           ['mode', 'gitBlob', 'bytes', 'sha256', 'checkoutBytes', 'checkoutSha256'])
            pins[row['path']] = row
    assert len(pins) == 81
    for group in ['checkoutMaterializations', 'untouchedCheckoutFiles']:
        assert inputs[group] == command_data[group]
        assert table_seal(inputs[group]) == command_data[group + 'SealSha256']
    materialized = {row['path']: row for row in inputs['checkoutMaterializations']}
    assert len(materialized) == len(inputs['checkoutMaterializations']) == 43
    before = []
    for name, row in sorted(pins.items()):
        assert source_tree[name] == parent_tree[name] == {'mode': row['mode'], 'gitBlob': row['gitBlob']}
        committed = git_bytes('show', inputs['implementationHead'] + ':' + name)
        assert len(committed) == row['bytes'] and digest_bytes(committed) == row['sha256']
        observed = file_record(Path(name))
        materialization = materialized.get(name)
        if materialization is None:
            permitted = [{'bytes': row['checkoutBytes'], 'sha256': row['checkoutSha256']}]
        else:
            assert materialization['mode'] == row['mode'] and materialization['gitBlob'] == row['gitBlob']
            assert materialization['gitBytes'] == row['bytes'] and materialization['gitSha256'] == row['sha256']
            permitted = [{'bytes': materialization['gitBytes'], 'sha256': materialization['gitSha256']},
                         {'bytes': materialization['checkoutBytes'], 'sha256': materialization['checkoutSha256']}]
        assert observed in permitted, 'Initial source representation differs'
        before.append({'path': name, 'observed': observed})
    for key in ['englishPlan', 'trxBufferAddendum']:
        captured_pinned_bytes(Path(inputs[key]['path']), inputs[key])
    source_receipt['beforeCheckoutPreparation'] = before
    source_receipt['checkoutBytePreparation'] = prepare_checkout_bytes(source_tree)
    source_rows = worktree_records(source_tree)
    current = {row['path']: row for row in source_rows}
    for name, row in pins.items():
        assert all(current[name][key] == row[key] for key in ['mode', 'gitBlob', 'bytes', 'sha256'])
    for name, count in [('MapTheoryInputs', 18), ('VisibilityInputs', 30)]:
        expected = [row['path'].split('/' + name + '/', 1)[1] for row in census['fixturePins'] if '/' + name + '/' in row['path']]
        actual = directory_records(Path('Source/DafnyB3Normalizer.Test') / name)
        assert len(actual) == count and [row['path'] for row in actual] == expected
    source_receipt['census'] = validate_source_census()
    source_receipt['ownershipFunctions'] = validate_copied_helper()
    toolchain_rows = capture_toolchain()
    (output / 'source-files.json').write_text(json.dumps(source_rows, indent=2) + '\n')
    (output / 'toolchain-files.json').write_text(json.dumps(toolchain_rows, indent=2) + '\n')
    source_receipt.update({'head': head, 'implementationHead': inputs['implementationHead'],
        'productCommit': inputs['productCommit'], 'trackedFileCount': len(source_rows),
        'trackedFilesSealSha256': seal(source_rows), 'sourceInventory': file_record(output / 'source-files.json'),
        'routingFiles': [current[name] for name in inputs['allowedRoutingFiles']],
        'toolchainInventory': file_record(output / 'toolchain-files.json'), 'toolchainSealSha256': seal(toolchain_rows),
        'pythonTool': {'executable': str(Path(sys.executable).resolve(strict=True)),
                       **file_record(Path(sys.executable).resolve(strict=True)), 'version': sys.version},
        'selectedDispatch': validate_selection(), 'finiteSourcePins': [pins[name] for name in sorted(pins)],
        'expectedExecutionControls': census['cases'], 'displayNameSources': census['displaySourceUrls']})


def compiled_directories():
    return [('current-core', core_output, core_rows)] + [
        ('current-' + suite + '-tests', test_outputs[suite], test_rows[suite]) for suite in ['normalizer', 'neutral']]


def boundary(stage, when):
    assert not direct_children() and not cleanup_poisoned
    assert git_bytes('rev-parse', 'HEAD').decode().strip() == head
    rows = worktree_records(source_tree)
    assert rows == source_rows, 'Tracked source changed across stage'
    actual_tools = capture_toolchain()
    assert actual_tools == toolchain_rows
    if package_receipt is not None:
        assert package_records() == package_receipt
    for role, directory, frozen in compiled_directories():
        if frozen is not None:
            assert directory_records(directory) == frozen, role + ' changed across stage'
    if core_dependencies is not None:
        assert capture_core_dependency_inputs() == core_dependencies
        version = source_receipt['coreSourceVersion']
        assert file_record(Path(version['generatedAssemblyInfoPath'])) == version['generatedAssemblyInfo']
    for suite, frozen in test_dependencies.items():
        assert capture_test_dependency_inputs(suite) == frozen
    if java_runtime_jar is not None:
        assert file_record(Path('Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime-4.11.0.jar')) == java_runtime_jar
    for suite, receipt in test_receipts.items():
        assert file_record(output / suite / 'result.trx') == receipt['trx'], 'Captured TRX changed across boundary'
    receipt = {'stage': stage, 'boundary': when, 'trackedFilesSealSha256': seal(rows),
               'toolchainSealSha256': seal(actual_tools), 'coreSealSha256': seal(core_rows) if core_rows is not None else None,
               'testSealSha256': {suite: seal(value) if value is not None else None for suite, value in test_rows.items()}}
    boundary_receipts.append(receipt)
    return receipt


def capture_trx(path):
    assert all(not ancestor.is_symlink() for ancestor in path.parents), 'TRX symlink ancestor'
    initial = path.lstat()
    assert stat.S_ISREG(initial.st_mode) and 0 < initial.st_size <= MAXIMUM_TRX_BYTES, 'TRX regular length bound'
    descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC)
    attempted, chunks, observed = 0, [], 0
    try:
        before = os.fstat(descriptor)
        identity = lambda info: (info.st_dev, info.st_ino, info.st_mode, info.st_size, info.st_mtime_ns, info.st_ctime_ns)
        assert stat.S_ISREG(before.st_mode) and identity(before) == identity(initial), 'TRX descriptor differs'
        remaining = before.st_size
        while remaining:
            assert time.monotonic() < stage_deadline, 'TRX capture deadline exceeded'
            request = min(65536, remaining)
            attempted += request
            chunk = os.read(descriptor, request)
            assert len(chunk) == request, 'TRX short content; no retry'
            chunks.append(chunk)
            observed += len(chunk)
            remaining -= len(chunk)
        assert time.monotonic() < stage_deadline, 'TRX probe deadline exceeded'
        attempted += 1
        assert not os.read(descriptor, 1), 'TRX grew beyond admitted content'
        after = os.fstat(descriptor)
        assert identity(after) == identity(before) and identity(path.lstat()) == identity(before), 'TRX capture identity changed'
        assert attempted <= MAXIMUM_TRX_BYTES + 1 and observed == before.st_size
        raw = b''.join(chunks)
        text = raw.decode('utf-8-sig')
        assert '\0' not in text, 'TRX UTF-8 input contains forbidden NUL'
        declaration = re.match(r'\s*<\?xml\s+([^?]*)\?>', text)
        if declaration:
            encoding = re.search(r'\bencoding\s*=\s*([\'"])([^\'"]+)\1', declaration[1])
            assert encoding is None or encoding[2].lower() == 'utf-8', 'Unsupported TRX XML encoding'
        assert '<!DOCTYPE' not in text.upper() and '<!ENTITY' not in text.upper(), 'Prohibited TRX XML declaration'
        assert '<!' not in text.replace('<!--', ''), 'Unsupported TRX XML construct'
        assert not re.search(r'<\?(?!xml\s)', text), 'Unsupported TRX processing instruction'
        root = ET.fromstring(raw)
        validate_trx_xml_shape(root)
        record = {'bytes': len(raw), 'sha256': digest_bytes(raw)}
        return root, record, {'attemptedInputBytes': attempted, 'observedInputBytes': observed,
            'maximumContentBytes': MAXIMUM_TRX_BYTES, 'maximumAttemptedBytes': MAXIMUM_TRX_BYTES + 1,
            'growthProbeBytes': 1, 'sameCapturedBufferHashedAndParsed': True, 'encoding': 'UTF-8/optional BOM'}
    finally:
        os.close(descriptor)


def validate_trx_xml_shape(root):
    # The accepted TRX document grammar is finite. Unknown elements or misplaced
    # known rows cannot be hidden by descendant queries.
    children = {
        'TestRun': {'Times', 'TestSettings', 'Results', 'TestDefinitions', 'TestEntries', 'TestLists', 'ResultSummary'},
        'TestSettings': {'Deployment'}, 'Results': {'UnitTestResult'},
        'UnitTestResult': {'Output'}, 'TestDefinitions': {'UnitTest'},
        'UnitTest': {'Execution', 'TestMethod'}, 'TestEntries': {'TestEntry'},
        'TestLists': {'TestList'}, 'ResultSummary': {'Counters', 'Output', 'RunInfos'},
        'Output': {'StdOut', 'StdErr'}, 'RunInfos': {'RunInfo'},
        'RunInfo': {'Text'}, 'Times': set(), 'Deployment': set(), 'Execution': set(),
        'TestMethod': set(), 'TestEntry': set(), 'TestList': set(), 'Counters': set(),
        'StdOut': set(), 'StdErr': set(), 'Text': set()}
    pending = [(root, 0)]
    while pending:
        element, depth = pending.pop()
        assert depth <= 5 and element.tag.startswith(TRX_NS), 'Unsupported TRX tree shape'
        name = element.tag[len(TRX_NS):]
        assert name in children, 'Unsupported TRX element'
        assert all(child.tag in {TRX_NS + value for value in children[name]} for child in element)
        pending.extend((child, depth + 1) for child in element)


def canonical_id(value):
    assert value is not None and str(uuid.UUID(value)) == value, 'Noncanonical TRX identity'
    return value


def validate_trx(suite):
    root, record, capture = capture_trx(output / suite / 'result.trx')
    assert root.tag == TRX_NS + 'TestRun'
    rows = root.findall('.//' + TRX_NS + 'UnitTestResult')
    definitions = root.findall('.//' + TRX_NS + 'UnitTest')
    entries = root.findall('.//' + TRX_NS + 'TestEntry')
    counters = root.findall('.//' + TRX_NS + 'Counters')
    count = census['suiteCounts'][suite]
    assert len(rows) == len(definitions) == len(entries) == count and len(counters) == 1
    counts = {key: int(value) for key, value in counters[0].attrib.items()}
    expected_keys = {'total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted', 'inconclusive',
                     'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected', 'warning',
                     'completed', 'inProgress', 'pending'}
    assert set(counts) == expected_keys
    assert all(value == (count if key in {'total', 'executed', 'passed'} else 0) for key, value in counts.items())
    ids = {canonical_id(row.get('id')): row for row in definitions}
    entry_ids = {canonical_id(row.get('testId')): row for row in entries}
    entry_executions = {canonical_id(row.get('executionId')) for row in entries}
    assert len(ids) == len(entry_ids) == len(entry_executions) == count and set(ids) == set(entry_ids)
    expected = {(row['className'], row['method']): row['count'] for row in census['methods'] if row['suite'] == suite}
    assert len(expected) == census['methodCounts'][suite]
    actual = {key: 0 for key in expected}
    names, tests, executions, observed = set(), set(), set(), []
    for row in rows:
        test_id, execution = canonical_id(row.get('testId')), canonical_id(row.get('executionId'))
        name = row.get('testName')
        assert name and row.get('outcome') == 'Passed'
        assert test_id not in tests and execution not in executions and name not in names
        tests.add(test_id); executions.add(execution); names.add(name)
        assert test_id in ids and entry_ids[test_id].get('executionId') == execution
        definition = ids[test_id]
        method, declared = definition.find(TRX_NS + 'TestMethod'), definition.find(TRX_NS + 'Execution')
        assert method is not None and declared is not None and canonical_id(declared.get('id')) == execution
        key = method.get('className'), method.get('name')
        assert key in expected and definition.get('name') == name
        assert name == '.'.join(key) or name.startswith('.'.join(key) + '(')
        assert method.get('adapterTypeName') == 'executor://xunit/VsTestRunner2/netcoreapp'
        assert Path(method.get('codeBase', '')).resolve() == (test_outputs[suite] / suite_assemblies[suite]).resolve()
        actual[key] += 1
        observed.append({'testId': test_id, 'executionId': execution, 'name': name, 'className': key[0],
                         'method': key[1], 'outcome': row.get('outcome')})
    assert actual == expected and tests == set(ids)
    assert names == {row['displayName'] for row in census['cases'] if row['suite'] == suite}
    entry_rows = [{'testId': row.get('testId'), 'executionId': row.get('executionId')} for row in entries]
    return {'suite': suite, 'expected': count, 'actual': len(rows), 'allPassed': True, 'trx': record,
            'rawCapture': capture, 'counters': counts, 'entryCountExpected': count, 'entryCount': len(entries),
            'entryAssociations': entry_rows, 'entryAssociationsSealSha256': seal(entry_rows),
            'methods': [{'className': key[0], 'method': key[1], 'count': value} for key, value in sorted(actual.items())],
            'tests': observed}


def capture_test_dependency_inputs(suite):
    project_name = suite_assemblies[suite][:-4]
    project_directory = Path('Source') / project_name
    path = project_directory / 'obj/project.assets.json'
    asset_record = file_record(path)
    assets = json.loads(captured_pinned_bytes(path, asset_record))
    assert assets['version'] == 3 and len(assets['targets']) == 1
    restore = assets['project']['restore']
    project_path = (ROOT / project_directory / (project_name + '.csproj')).resolve()
    assert Path(restore['projectPath']).resolve() == Path(restore['projectUniqueName']).resolve() == project_path
    assert restore['projectName'] == project_name and restore['projectStyle'] == 'PackageReference'
    assert restore['originalTargetFrameworks'] == ['net8.0']
    target_name, target = next(iter(assets['targets'].items()))
    assert target_name in {'net8.0', '.NETCoreApp,Version=v8.0'} and len(assets['packageFolders']) == 1
    package_root = Path(next(iter(assets['packageFolders'])))
    assert package_root.is_dir() and not package_root.is_symlink()
    assert Path(restore['packagesPath']).resolve() == package_root.resolve()
    files, projects, total = [], [], 0
    allowed_projects = {(ROOT / 'Source' / name / (name + '.csproj')).resolve()
                        for name in ['DafnyCore', 'DafnyB3Protocol', 'DafnyRuntime']}
    for library, declarations in sorted(target.items()):
        metadata = assets['libraries'][library]
        assert declarations['type'] == metadata['type']
        if metadata['type'] == 'project':
            reference = (project_directory / metadata['msbuildProject']).resolve()
            assert reference in allowed_projects
            projects.append({'library': library, 'projectPath': str(reference),
                             'source': file_record(reference), 'targetFramework': declarations['framework']})
            continue
        assert metadata['type'] == 'package'
        relative_package = Path(metadata['path'])
        assert not relative_package.is_absolute() and '..' not in relative_package.parts
        for kind in ['compile', 'runtime']:
            for asset in sorted(declarations.get(kind, {})):
                if not asset.endswith('.dll'):
                    continue
                relative = Path(asset)
                assert not relative.is_absolute() and '..' not in relative.parts
                actual_path = package_root / relative_package / relative
                assert all(not ancestor.is_symlink() for ancestor in actual_path.parents)
                record = file_record(actual_path)
                total += record['bytes']
                assert total <= MAXIMUM_INVENTORY_BYTES and len(files) < MAXIMUM_INVENTORY_FILES
                files.append({'library': library, 'assetKind': kind, 'assetPath': asset,
                              'actualPath': str(actual_path), **record})
    assert {row['library'].split('/')[0] for row in projects} == {'DafnyCore', 'DafnyB3Protocol', 'DafnyRuntime'}
    return {'assetsFile': {'path': str(path), **asset_record}, 'target': target_name,
            'projectReferences': projects, 'files': files, 'sealSha256': seal(files),
            'scope': 'raw SDK-resolved package byte inputs; not copied-runtime/CLR selection attestation'}


def validate_test_snapshot(suite, stage):
    directory, assembly = test_outputs[suite], suite_assemblies[suite]
    stem = assembly[:-4]
    rows = generated_records(directory, [assembly, stem + '.deps.json', stem + '.runtimeconfig.json',
                                        'DafnyCore.dll', 'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyPrelude.bpl'])
    test_rows[suite] = rows
    preserve_compiled_snapshot(stage, 'current-' + suite + '-tests', directory, rows)
    core_map, copied = {r['path']: r for r in core_rows}, {r['path']: r for r in rows}
    for path in ['DafnyCore.dll', 'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyPrelude.bpl']:
        assert copied[path] == core_map[path], 'Test snapshot differs from first current Core'
    boogie = verify_boogie_assemblies(directory)
    for row in core_dependencies['files']:
        if row['assetKind'] == 'runtime' and row['library'].startswith('Boogie'):
            assert all(copied[Path(row['assetPath']).name][key] == row[key] for key in ['bytes', 'sha256'])
    deps = json.loads((directory / (stem + '.deps.json')).read_text())
    core = [(name, row) for name, row in deps['libraries'].items() if name.startswith('DafnyCore/')]
    assert len(core) == 1 and core[0][1]['type'] == 'project'
    assert deps['libraries']['xunit.core/2.4.1']['type'] == 'package'
    assert deps['libraries']['xunit.runner.visualstudio/2.4.3']['type'] == 'package'
    runtime = json.loads((directory / (stem + '.runtimeconfig.json')).read_text())
    assert runtime['runtimeOptions']['tfm'] == 'net8.0'
    copied_metadata = []
    for row in rows:
        if Path(row['path']).name.lower().startswith(('xunit', 'microsoft.testplatform', 'microsoft.visualstudio.testplatform')):
            copied_metadata.append(row)
    if suite == 'normalizer':
        for row in census['fixturePins']:
            relative = row['path'].split('Source/DafnyB3Normalizer.Test/', 1)[1]
            assert relative in copied and all(copied[relative][key] == row[key] for key in ['bytes', 'sha256'])
        for folder, count in [('MapTheoryInputs', 18), ('VisibilityInputs', 30)]:
            assert len(directory_records(directory / folder)) == count
    (output / (suite + '-files.json')).write_text(json.dumps(rows, indent=2) + '\n')
    test_dependencies[suite] = capture_test_dependency_inputs(suite)
    source_receipt.setdefault('testDependencies', {})[suite] = {'boogieAssemblies': boogie,
        'testInfrastructureFiles': copied_metadata, 'deps': file_record(directory / (stem + '.deps.json')),
        'runtimeconfig': file_record(directory / (stem + '.runtimeconfig.json')),
        'resolvedAssetInputs': test_dependencies[suite]}


def validate(name):
    global core_rows, package_receipt, java_runtime_jar, core_dependencies
    assert time.monotonic() < stage_deadline, 'Stage validation deadline exceeded'
    if name == 'source-head':
        gathered = json.loads((output / 'source-head.txt').read_text())
        assert gathered['head'] == head and re.fullmatch(r'8\.\d+\.\d+', gathered['sdkVersion'])
        selected = Path(toolchain_rows['executable']['resolvedPath']).parent / 'sdk' / gathered['sdkVersion']
        assert str(selected) in {row['path'] for row in toolchain_rows['directories']}
        source_receipt['actualSdkSelection'] = gathered
    elif name == 'packages':
        package_receipt = package_records()
    elif name == 'core-build':
        core_rows = generated_records(core_output, ['DafnyCore.dll', 'DafnyCore.deps.json',
            'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyPrelude.bpl'])
        preserve_compiled_snapshot(name, 'current-core', core_output, core_rows)
        core_dependencies = capture_core_dependency_inputs()
        assert next(row for row in core_rows if row['path'] == 'DafnyPrelude.bpl')['sha256'] == file_record(Path('Source/DafnyCore/DafnyPrelude.bpl'))['sha256']
        assembly_path = Path('Source/DafnyCore/obj/Release/net8.0/DafnyCore.AssemblyInfo.cs')
        assembly_record = file_record(assembly_path)
        assembly_bytes = captured_pinned_bytes(assembly_path, assembly_record)
        assembly_source = assembly_bytes.decode('utf-8-sig')
        (output / 'core-assembly-info.cs').write_bytes(assembly_bytes)
        expected_version = '4.11.0+' + head
        assert '[assembly: System.Reflection.AssemblyInformationalVersionAttribute("' + expected_version + '")]' in assembly_source
        assert expected_version.encode() in (core_output / 'DafnyCore.dll').read_bytes()
        source_receipt['coreSourceVersion'] = {'expected': expected_version, 'generatedAssemblyInfo': assembly_record, 'generatedAssemblyInfoPath': str(assembly_path),
            'preservedAttributeSource': 'core-assembly-info.cs',
            'dllContainsExactVersionUtf8': True, 'scope': 'generated attribute and byte marker, not an independent CLR metadata/load attestation'}
        java_runtime_jar = file_record(Path('Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime-4.11.0.jar'))
        (output / 'core-files.json').write_text(json.dumps({'files': core_rows,
            'resolvedDependencyInputs': core_dependencies, 'javaRuntimeDependency': java_runtime_jar}, indent=2) + '\n')
    elif name in {'normalizer-build', 'neutral-build'}:
        validate_test_snapshot(name.removesuffix('-build'), name)
    elif name in {'normalizer-structural', 'neutral-structural'}:
        suite = name.removesuffix('-structural')
        test_receipts[suite] = validate_trx(suite)
    else:
        raise AssertionError('Unknown selected stage')
    assert time.monotonic() < stage_deadline, 'Stage validation exceeded its original deadline'


def run_with_boundaries(name, command):
    global stage_deadline
    before = after = result = None
    try:
        before = boundary(name, 'before')
        stage_deadline = time.monotonic() + STAGE_TIMEOUT_SECONDS
        result = run_stage(name, command)
    except Exception as error:
        result = {'stage': name, 'exitCode': 1, 'command': command, 'failure': type(error).__name__ + ': ' + str(error)}
    finally:
        try:
            after = boundary(name, 'after')
        except Exception as error:
            failure = type(error).__name__ + ': ' + str(error)
            if result is None:
                result = {'stage': name, 'exitCode': 1, 'command': command, 'failure': failure}
            else:
                result['boundaryFailure'] = failure
                result['exitCode'] = 1
    result['beforeBoundary'], result['afterBoundary'] = before, after
    if name in compiled_stage_acceptance:
        accepted = result['exitCode'] == 0 and before is not None and after is not None and not cleanup_poisoned
        compiled_stage_acceptance[name] = accepted
        for snapshot in compiled_snapshots:
            if snapshot['stage'] == name:
                snapshot['accepted'] = accepted and snapshot['copyComplete']
    if result['exitCode'] != 0:
        capture_failure_observations(name)
    return result


def capture_failure_observations(stage):
    for role, directory, initial in compiled_directories():
        record = {'stage': stage, 'role': role, 'accepted': False, 'initialCaptured': initial is not None,
                  'initialSealSha256': seal(initial) if initial is not None else None,
                  'observedFiles': None, 'observedSealSha256': None, 'delta': None,
                  'artifactPath': None, 'copyComplete': False, 'observationFailure': None, 'copyFailure': None}
        failure_observations.append(record)
        try:
            if not directory.exists() and not directory.is_symlink():
                record.update({'missing': True, 'observedFiles': [], 'observedSealSha256': seal([]),
                               'delta': inventory_delta(initial, [])})
                continue
            observed = directory_records(directory)
            record.update({'observedFiles': observed, 'observedSealSha256': seal(observed), 'delta': inventory_delta(initial, observed)})
            first = next((row for row in compiled_snapshots if row['role'] == role), None)
            if initial is not None and observed == initial and first is not None and first['copyComplete']:
                assert directory_records(output / first['artifactPath']) == initial
                record.update({'artifactPath': first['artifactPath'], 'copyComplete': True, 'usesInitialSnapshot': True})
                continue
            destination = output / 'failure-observed' / (stage + '-' + role)
            record['artifactPath'] = destination.relative_to(output).as_posix()
            try:
                copy_inventory_snapshot(directory, destination, observed)
                record['copyComplete'] = True
            except Exception as error:
                record['copyFailure'] = type(error).__name__ + ': ' + str(error)
        except Exception as error:
            record['observationFailure'] = type(error).__name__ + ': ' + str(error)


def validate_compiled_artifact_snapshots():
    failures = []
    for record in compiled_snapshots:
        try:
            assert record['copyComplete'] and directory_records(output / record['artifactPath']) == record['files']
            record['finalSnapshotValidated'] = True
        except Exception as error:
            record['accepted'] = False
            failures.append({'role': record['role'], 'scope': 'first-snapshot', 'failure': type(error).__name__ + ': ' + str(error)})
    needs_observation = False
    for role, directory, rows in compiled_directories():
        if rows is not None:
            observed = None
            try:
                observed = directory_records(directory)
                assert observed == rows
            except Exception as error:
                needs_observation |= observed is None or not any(record['role'] == role and record['observedFiles'] == observed for record in failure_observations)
                failures.append({'role': role, 'scope': 'live-output', 'failure': type(error).__name__ + ': ' + str(error)})
    if needs_observation:
        capture_failure_observations('final-export')
    for record in failure_observations:
        if record['copyComplete']:
            try:
                assert directory_records(output / record['artifactPath']) == record['observedFiles']
            except Exception as error:
                record['copyComplete'], record['copyFailure'] = False, type(error).__name__ + ': ' + str(error)
        for key in ['observationFailure', 'copyFailure']:
            if record[key] is not None:
                failures.append({'role': record['role'], 'scope': 'failure-observation', 'failure': record[key]})
    for suite, receipt in test_receipts.items():
        try:
            assert file_record(output / suite / 'result.trx') == receipt['trx']
        except Exception as error:
            failures.append({'role': suite, 'scope': 'captured-trx', 'failure': type(error).__name__ + ': ' + str(error)})
    return failures


def main():
    global inputs, census, command_data, output, head, source_tree, source_rows, toolchain_rows, source_receipt
    global core_output, core_rows, core_dependencies, test_outputs, test_rows, test_receipts, test_dependencies, suite_assemblies
    global package_receipt, java_runtime_jar, boundary_receipts, stage_environment, cleanup_poisoned, stage_deadline
    global compiled_snapshots, compiled_stage_acceptance, failure_observations
    output = Path('out/b3-integrated-structural')
    output.mkdir(parents=True, exist_ok=False)
    head = source_tree = source_rows = toolchain_rows = core_rows = core_dependencies = package_receipt = java_runtime_jar = None
    source_receipt, boundary_receipts = {}, []
    core_output = Path('Binaries/net8.0')
    test_outputs = {'normalizer': Path('Source/DafnyB3Normalizer.Test/bin/Release/net8.0'),
                    'neutral': Path('Source/DafnyCore.Test/bin/Release/net8.0')}
    suite_assemblies = {'normalizer': 'DafnyB3Normalizer.Test.dll', 'neutral': 'DafnyCore.Test.dll'}
    test_rows, test_receipts, test_dependencies = {'normalizer': None, 'neutral': None}, {}, {}
    cleanup_poisoned, stage_deadline = False, 0
    compiled_snapshots, failure_observations = [], []
    compiled_stage_acceptance = {'core-build': False, 'normalizer-build': False, 'neutral-build': False}
    results, final_error, preservation_failures = [], None, []
    try:
        assert Path('/proc/self/task').exists() and hasattr(os, 'pidfd_open') and hasattr(signal, 'pidfd_send_signal')
        assert not direct_children() and ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) == 0
        inputs = json.loads(captured_pinned_bytes(INPUTS_PATH, {'bytes': INPUTS_BYTES, 'sha256': INPUTS_SHA256}))
        assert inputs['schemaVersion'] == 1
        census = json.loads(captured_pinned_bytes(Path(inputs['census']['path']), inputs['census']))
        command_data = json.loads(captured_pinned_bytes(Path(inputs['commandData']['path']), inputs['commandData']))
        validate_selection()
        stage_environment = {**os.environ, **command_data['stageEnvironment']}
        assert not core_output.exists() and all(not path.exists() for path in test_outputs.values())
        validate_source_initial()
        replacements = {'{PINNED_RESOLVED_PYTHON}': str(Path(sys.executable).resolve(strict=True)),
                        '{CURRENT_CHECKOUT}': str(ROOT), '{CURRENT_CI_HEAD}': head}
        commands = [(row['stage'], [replace_placeholders(arg, replacements) for arg in row['argv']]) for row in command_data['commands']]
        assert len(commands) == 7 and table_seal(command_data['commands']) == command_data['commandsSealSha256']
        for name, command in commands:
            result = run_with_boundaries(name, command)
            results.append(result)
            print(name, result['exitCode'], result.get('failure') or result.get('boundaryFailure') or '', flush=True)
            if result['exitCode'] != 0 or cleanup_poisoned:
                break
        preservation_failures = validate_compiled_artifact_snapshots()
    except Exception as error:
        final_error = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            assert not direct_children(), 'Final owned scope is not empty'
        except Exception as error:
            cleanup_poisoned = True
            final_error = (final_error + '; ' if final_error else '') + str(error)
            try:
                final_error += '; final bounded cleanup signals=' + str(cleanup_children())
            except Exception as cleanup_error:
                final_error += '; final cleanup failed: ' + str(cleanup_error)
        passed = (len(results) == 7 and all(row['exitCode'] == 0 for row in results)
                  and len(boundary_receipts) == 14 and all(compiled_stage_acceptance.values())
                  and set(test_receipts) == {'normalizer', 'neutral'} and final_error is None
                  and not preservation_failures and not cleanup_poisoned)
        receipt = {'passed': passed, 'scope': 'integrated-current-source-structural-only', 'head': head,
            'focusedControlCountExpected': 583, 'normalizerExpected': 557, 'neutralExpected': 26,
            'completeLibraryVerified': False, 'libraryBinaryProduced': False, 'workerRuntimeVerified': False,
            'cliVerified': False, 'corpusVerified': False, 'regressionRuntimeVerified': False,
            'defaultCompatibilityVerified': False, 'backendAccepted': False,
            'source': source_receipt, 'stages': results, 'stageBoundaries': boundary_receipts,
            'packages': package_receipt, 'resolvedCoreDependencies': core_dependencies, 'javaRuntimeDependency': java_runtime_jar,
            'focusedControls': test_receipts, 'finalError': final_error,
            'compiledStageAcceptance': compiled_stage_acceptance, 'compiledSnapshots': compiled_snapshots,
            'failureObservations': failure_observations, 'artifactPreservationFailures': preservation_failures,
            'primaryFailure': next(({'stage': row['stage'], 'actualProcessExitCode': row.get('actualProcessExitCode'),
                'failure': row.get('failure'), 'boundaryFailure': row.get('boundaryFailure')}
                for row in results if row['exitCode'] != 0), None),
            'cleanupPoisoned': cleanup_poisoned, 'maximumArtifactBytes': MAXIMUM_ARTIFACT_BYTES,
            'provenanceScope': 'Filesystem byte pins at boundaries; unuploaded tool files are runner metadata, not CLR selection or atomic execution-image attestation',
            'ownershipScope': 'Dedicated Linux subreaper/pidfd owned children; no cgroup or arbitrary escape claim'}
        summary = output / 'summary.json'
        summary.write_text(json.dumps(receipt, indent=2) + '\n')
        try:
            rows = directory_records(output)
            assert sum(row['bytes'] for row in rows) <= MAXIMUM_ARTIFACT_BYTES
            (output / 'artifact-inventory.json').write_text(json.dumps({'files': rows, 'sealSha256': seal(rows)}, indent=2) + '\n')
            assert sum(row['bytes'] for row in directory_records(output)) <= MAXIMUM_ARTIFACT_BYTES
        except Exception as error:
            receipt['passed'], receipt['artifactFailure'] = False, type(error).__name__ + ': ' + str(error)
            summary.write_text(json.dumps(receipt, indent=2) + '\n')
        wording = 'PASS: current Core/two test compiles and583 structural controls only' if receipt['passed'] else 'NOT GREEN: source/compile/structural receipt incomplete or failed'
        print(wording, flush=True)
        if path := os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(path, 'a') as stream:
                stream.write('## Integrated structural receipt\n\n' + wording + '\n\nBroader backend acceptance remains false.\n')
    return 0


def replace_placeholders(value, replacements):
    for key, replacement in replacements.items():
        value = value.replace(key, replacement)
    assert '{' not in value and '}' not in value
    return value


if __name__ == '__main__':
    if sys.argv[1:] == ['--source-head']:
        source_head_child()
    else:
        assert not sys.argv[1:]
        raise SystemExit(main())
