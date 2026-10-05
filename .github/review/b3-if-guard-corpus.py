"""Two current unsigned If guard corpus cases using separately qualified prerequisites.

Expected failures exit zero with a strict failed receipt. The prerequisite gate
remains failed overall. Its exact completed library proof/runtime/package are
requalified for bounded reuse, never relabeled as verification in this run.
Current CLI inputs are captured once; failure observations cannot replace them.
"""
import ast
import csv
import ctypes
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import selectors
import shutil
import signal
import stat
import subprocess
import sys
import time
import zipfile

ROOT = Path.cwd().resolve()
INPUTS_PATH = Path('.github/review/b3-if-guard-corpus-inputs.json')
MAXIMUM_FILE_BYTES = 512 * 1024 * 1024
MAXIMUM_INVENTORY_BYTES = 4 * 1024 * 1024 * 1024
MAXIMUM_INVENTORY_FILES = 40000
MAXIMUM_ARTIFACT_BYTES = 256 * 1024 * 1024
MAXIMUM_STAGE_LOG_BYTES = 32 * 1024 * 1024
STAGE_TIMEOUT_SECONDS = 1800
NATURAL_CHILD_GRACE_SECONDS = 5
MAXIMUM_CHILD_DIAGNOSTICS = 64
MAXIMUM_CORPUS_CAPTURE_BYTES = 8 * 1024 * 1024
ARCHIVE_PATH = Path('out/b3-if-guard-prerequisite/artifact.zip')
OUTPUT_PATH = Path('out/b3-if-guard-corpus')
SELECTED_PATH = OUTPUT_PATH / 'prerequisite'
CLI_PATH = Path('Binaries/net8.0')



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



def validate_copied_helper():
    import ast
    original = Path('.github/review/b3-native-compile.py').read_text()
    current = Path(__file__).read_text()
    for name in inputs['copiedOwnershipFunctions']:
        def text_of(source):
            node = next(node for node in ast.parse(source).body if isinstance(node, ast.FunctionDef) and node.name == name)
            return ''.join(source.splitlines(keepends=True)[node.lineno - 1:node.end_lineno])
        assert text_of(original) == text_of(current), 'Reviewed ownership function changed: ' + name



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



def run_with_boundaries(name, command):
    before = after = None
    result = None
    try:
        before = boundary(name, 'before')
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
    result['beforeBoundary'] = before
    result['afterBoundary'] = after
    if name in compiled_stage_acceptance:
        accepted = (result['exitCode'] == 0 and before is not None and after is not None
                    and not cleanup_poisoned)
        compiled_stage_acceptance[name] = accepted
        for snapshot in compiled_snapshots:
            if snapshot['stage'] == name:
                snapshot['accepted'] = accepted and snapshot['copyComplete']
    if result['exitCode'] != 0:
        capture_failure_observations(name)
    return result



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



def validate_selection(inputs):
    assert os.environ.get('GITHUB_EVENT_NAME') == 'workflow_dispatch'
    assert os.environ.get('GITHUB_REF', '').startswith('refs/heads/scratch/')
    expected = {'b3_focus_gate': 'unsigned-if-guard-corpus', 'b3_compile_only': 'true',
                'b3_full_gate': 'false', 'binaries': 'false',
                'resolver_probe': 'false', 'additional_axioms_probe': 'false'}
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    actual = {key: str(event.get('inputs', {}).get(key, '')).lower() for key in expected}
    assert actual == expected, 'Require the exact dedicated two-case dispatch'
    for key, value in expected.items():
        assert os.environ.get('B3_INPUT_' + key.upper(), '').lower() == value
    return {'event': 'workflow_dispatch', 'ref': os.environ['GITHUB_REF'], 'inputs': actual,
            'eventFile': file_record(Path(os.environ['GITHUB_EVENT_PATH']))}


def gh_record():
    lexical = Path(shutil.which('gh') or '')
    assert lexical.is_file(), 'Require the GitHub artifact reader'
    executable = lexical.resolve(strict=True)
    links = [{'path': str(path), 'link': os.readlink(path), 'resolved': str(path.resolve(strict=True))}
             for path in [lexical, *lexical.parents] if path.is_symlink()]
    return {'lexicalPath': str(lexical), 'resolvedPath': str(executable),
            'ancestorSymlinks': links, **file_record(executable)}


def validate_source_initial():
    global head, source_tree, source_rows, toolchain_rows, gh_rows
    assert git_bytes('status', '--porcelain', '--untracked-files=all') == b''
    head = git_bytes('rev-parse', 'HEAD').decode().strip()
    assert re.fullmatch('[0-9a-f]{40}', head) and head == os.environ.get('GITHUB_SHA')
    git_bytes('merge-base', '--is-ancestor', inputs['baselineHead'], head)
    assert git_bytes('rev-parse', inputs['implementationHead'] + '^{tree}').decode().strip() == inputs['implementationTree']
    assert Path('.github/review/base').read_text() == 'v4.11.0 ' + inputs['productCommit'] + '\n'
    exclusions = [':(exclude)' + path for path in ['Source/IntegrationTests', 'docs', '.github',
                                                  'REVIEW.md', 'ROADMAP.md', 'AGENTS.md', 'CLAUDE.md']]
    assert git_bytes('diff', '--name-only', inputs['productCommit'], 'HEAD', '--', '.', *exclusions) == b''
    source_tree = tree_records('HEAD')
    baseline_tree = tree_records(inputs['baselineHead'])
    changed = {name for name in source_tree.keys() | baseline_tree.keys()
               if source_tree.get(name) != baseline_tree.get(name)}
    assert changed == set(inputs['allowedRoutingFiles']), 'Unexpected routing-child source change'
    source_receipt['checkoutBytePreparation'] = prepare_checkout_bytes(source_tree)
    source_rows = worktree_records(source_tree)
    by_path = {row['path']: row for row in source_rows}
    for key, expected in [('implementationFiles', 'implementationFilesSealSha256'),
                          ('sourceClosure', 'sourceClosureSealSha256'),
                          ('corpusFiles', 'corpusFilesSealSha256')]:
        assert seal(inputs[key]) == inputs[expected]
    for row in (inputs['implementationFiles'] + inputs['pinnedInputs'] + inputs['existingFocusFiles']
                + inputs['sourceClosure'] + inputs['corpusFiles']):
        assert {key: by_path[row['path']][key] for key in row} == row, 'Frozen source input differs: ' + row['path']
    archived_tree = tree_records(inputs['archivedPrerequisites']['sourceHead'])
    for row in inputs['sourceClosure']:
        assert archived_tree[row['path']] == {'mode': row['mode'], 'gitBlob': row['gitBlob']}
    assert json.loads(Path('Source/IntegrationTests/TestFiles/B3/cases.json').read_text()) == inputs['allCorpusCases']
    assert len(inputs['allCorpusCases']['cases']) == 67
    assert [case for case in inputs['allCorpusCases']['cases'] if case['name'] in {
        'bitvector-unsigned-if-guard', 'bitvector-unsigned-if-guard-false'}] == inputs['selectedCorpusCases']
    assert len(inputs['selectedCorpusCases']) == 2
    validate_copied_helper()
    toolchain_rows = capture_toolchain()
    gh_rows = gh_record()
    (output / 'source-files.json').write_text(json.dumps(source_rows, indent=2) + '\n')
    (output / 'toolchain-files.json').write_text(json.dumps(toolchain_rows, indent=2) + '\n')
    source_receipt.update({'head': head, 'productCommit': inputs['productCommit'],
        'baselineHead': inputs['baselineHead'], 'implementationHead': inputs['implementationHead'],
        'trackedFileCount': len(source_rows), 'trackedFilesSealSha256': seal(source_rows),
        'implementationFilesSealSha256': inputs['implementationFilesSealSha256'],
        'sourceClosureSealSha256': inputs['sourceClosureSealSha256'],
        'corpusFilesSealSha256': inputs['corpusFilesSealSha256'],
        'selectedCorpusCases': inputs['selectedCorpusCases'], 'routingFiles': [by_path[name] for name in inputs['allowedRoutingFiles']],
        'checkoutSelectionLabelsScope': 'Inherited original 35-focus EOL labels; current CLI project selection is bound by actual resolved assets',
        'toolchainSealSha256': seal(toolchain_rows), 'gh': gh_rows,
        'selectedDispatch': validate_selection(inputs)})


def boundary(stage, when):
    assert not direct_children() and not cleanup_poisoned
    assert git_bytes('rev-parse', 'HEAD').decode().strip() == head
    assert worktree_records(source_tree) == source_rows, 'Tracked source changed across stage'
    assert capture_toolchain() == toolchain_rows, 'Actual SDK/runtime input bytes changed'
    assert gh_record() == gh_rows, 'GitHub artifact reader input bytes changed'
    if prerequisite_rows is not None:
        assert directory_records(SELECTED_PATH) == prerequisite_rows, 'Archived selected inputs changed'
        assert file_record(ARCHIVE_PATH) == archive_record(), 'Original prerequisite archive changed'
        assert (solver_path().lstat().st_mode & 0o777) == 0o755, 'Solver executable mode changed'
    if package_receipt is not None:
        assert package_records() == package_receipt, 'Pinned package feed changed'
    if cli_rows is not None:
        assert directory_records(CLI_PATH) == cli_rows, 'Current CLI output bytes changed'
    if cli_dependencies is not None:
        assert capture_cli_dependency_inputs() == cli_dependencies, 'SDK-resolved CLI dependency inputs changed'
    if java_runtime_jar is not None:
        assert file_record(Path('Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime-4.11.0.jar')) == java_runtime_jar
    result = {'stage': stage, 'boundary': when, 'trackedFilesSealSha256': seal(source_rows),
              'toolchainSealSha256': seal(toolchain_rows), 'ghSha256': gh_rows['sha256'],
              'prerequisiteSealSha256': seal(prerequisite_rows) if prerequisite_rows is not None else None,
              'cliSealSha256': seal(cli_rows) if cli_rows is not None else None,
              'cliDependenciesSealSha256': seal(cli_dependencies) if cli_dependencies is not None else None}
    boundary_receipts.append(result)
    return result


def archive_record():
    expected = inputs['archivedPrerequisites']
    return {'bytes': expected['archiveBytes'], 'sha256': expected['archiveSha256']}


def solver_path():
    return SELECTED_PATH / 'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'


def read_bounded_json(path, maximum=16 * 1024 * 1024):
    assert file_record(path)['bytes'] <= maximum
    return json.loads(path.read_text())


def capture_child_streams(command, stdout_path, stderr_path, maximum_stdout, maximum_stderr, maximum_total, seconds):
    # Drain both pipes concurrently. Never write an over-limit chunk. On error,
    # the outer owned-stage helper retains responsibility for all descendants.
    deadline = time.monotonic() + seconds
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    counts = {'stdout': 0, 'stderr': 0}
    limits = {'stdout': maximum_stdout, 'stderr': maximum_stderr}
    with stdout_path.open('xb') as stdout, stderr_path.open('xb') as stderr, selectors.DefaultSelector() as selected:
        streams = {'stdout': stdout, 'stderr': stderr}
        selected.register(process.stdout, selectors.EVENT_READ, 'stdout')
        selected.register(process.stderr, selectors.EVENT_READ, 'stderr')
        while selected.get_map():
            assert time.monotonic() < deadline, 'Child stream safety deadline exceeded'
            for key, _ in selected.select(timeout=0.01):
                chunk = os.read(key.fd, 65536)
                if not chunk:
                    selected.unregister(key.fileobj)
                    continue
                kind = key.data
                counts[kind] += len(chunk)
                assert counts[kind] <= limits[kind] and sum(counts.values()) <= maximum_total, 'Child stream byte ceiling exceeded'
                streams[kind].write(chunk)
        assert time.monotonic() < deadline
        return process.wait(timeout=max(0.01, deadline - time.monotonic()))


def archive_child():
    # gh and the pinned solver are children of the enclosing owned stage.
    local = json.loads(INPUTS_PATH.read_text())['archivedPrerequisites']
    api = 'repos/erniecohen/dafny/actions/artifacts/' + str(local['artifactId'])
    metadata = json.loads(bounded_tool_stdout(['gh', 'api', api]))
    assert metadata['id'] == local['artifactId'] and metadata['name'] == local['artifactName']
    assert metadata['size_in_bytes'] == local['archiveBytes'] and not metadata['expired']
    assert metadata['digest'] == 'sha256:' + local['archiveSha256']
    assert metadata['workflow_run']['id'] == local['runId']
    assert metadata['workflow_run']['head_sha'] == local['sourceHead']
    ARCHIVE_PATH.parent.mkdir(parents=True, exist_ok=False)
    download_stderr = OUTPUT_PATH / 'archive-download-stderr.txt'
    assert local['archiveBytes'] <= MAXIMUM_ARTIFACT_BYTES
    code = capture_child_streams(['gh', 'api', api + '/zip'], ARCHIVE_PATH, download_stderr,
        local['archiveBytes'], 1024 * 1024, local['archiveBytes'] + 1024 * 1024, 300)
    assert code == 0, 'Exact prerequisite archive retrieval failed'
    assert file_record(ARCHIVE_PATH) == {'bytes': local['archiveBytes'], 'sha256': local['archiveSha256']}
    selected = {row['archivePath']: row for row in local['selectedFiles']}
    assert len(selected) == 19 and sum(row['bytes'] for row in selected.values()) <= 48 * 1024 * 1024
    assert not SELECTED_PATH.exists()
    with zipfile.ZipFile(ARCHIVE_PATH) as archive:
        entries = archive.infolist()
        assert len(entries) == local['allMemberCount'] == 805
        assert len({entry.filename for entry in entries}) == len(entries), 'Duplicate ZIP member'
        metadata_rows = []
        for entry in entries:
            path = PurePosixPath(entry.filename)
            assert path.parts and not path.is_absolute() and path.as_posix() == entry.filename
            assert all(part not in {'.', '..'} for part in path.parts) and '\\' not in entry.filename
            assert not entry.is_dir() and stat.S_IFMT(entry.external_attr >> 16) in {0, stat.S_IFREG}
            assert not entry.flag_bits & 1 and entry.file_size <= MAXIMUM_FILE_BYTES
            metadata_rows.append({'path': entry.filename, 'bytes': entry.file_size,
                'compressedBytes': entry.compress_size, 'crc32': entry.CRC, 'mode': entry.external_attr >> 16})
        assert sum(entry.file_size for entry in entries) == local['allUncompressedBytes'] <= MAXIMUM_FILE_BYTES
        assert seal(metadata_rows) == local['allMemberMetadataSealSha256']
        assert set(selected) <= {entry.filename for entry in entries}
        SELECTED_PATH.mkdir(parents=True)
        for entry in entries:
            if entry.filename not in selected:
                continue
            row = selected[entry.filename]
            relative = PurePosixPath(row['path'])
            assert relative.parts and not relative.is_absolute() and relative.as_posix() == row['path']
            assert all(part not in {'.', '..'} for part in relative.parts) and '\\' not in row['path']
            assert entry.filename == 'out/b3-native-compile/' + row['path']
            assert entry.file_size == row['bytes']
            destination = SELECTED_PATH / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            digest = hashlib.sha256()
            length = 0
            with archive.open(entry) as original, destination.open('xb') as target:
                while chunk := original.read(1024 * 1024):
                    length += len(chunk)
                    assert length <= row['bytes']
                    digest.update(chunk)
                    target.write(chunk)
            assert length == row['bytes'] and digest.hexdigest() == row['sha256']
    executable = SELECTED_PATH / 'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
    executable.chmod(0o755)
    version = bounded_tool_stdout([str(executable.resolve()), '-version'])
    assert version == local['solverVersion']
    print(json.dumps({'artifactId': local['artifactId'], 'runId': local['runId'],
        'archive': file_record(ARCHIVE_PATH), 'solverVersion': version,
        'metadata': {key: metadata[key] for key in ['id', 'name', 'size_in_bytes', 'digest', 'expired', 'workflow_run']}}))


def qualify_archived_prerequisites():
    global prerequisite_rows, prerequisite_receipt
    frozen = inputs['archivedPrerequisites']
    assert file_record(ARCHIVE_PATH) == archive_record()
    expected = sorted([{'path': row['path'], 'bytes': row['bytes'], 'sha256': row['sha256']}
                       for row in frozen['selectedFiles']], key=lambda row: row['path'])
    actual = directory_records(SELECTED_PATH)
    assert actual == expected, 'Selected archive inventory incomplete or changed'
    gathering = read_bounded_json(output / 'archived-prerequisites.txt')
    assert gathering['archive'] == archive_record() and gathering['solverVersion'] == frozen['solverVersion']
    summary = read_bounded_json(SELECTED_PATH / 'summary.json')
    assert summary['head'] == frozen['sourceHead'] and summary['passed'] is False and summary['fullGate'] is True
    assert summary['cleanupPoisoned'] is False and summary['defaultCompatibilityVerified'] is False
    assert summary['completeLibraryVerified'] is True and summary['libraryBinaryProduced'] is True
    assert summary['sourceInventory'] == frozen['sourceInventory'] and summary['libraryProof'] == frozen['libraryProof']
    assert summary['sourceInventory']['sourceValidated'] is True and summary['sourceInventory']['sourceInventoryCount'] == 60
    assert summary['sourceInventory']['sourceConsumerPins'] == [frozen['sourceInventory']['sourceManifestSha256']] * 3
    actual_stages = {row['stage']: row for row in summary['stages']}
    assert len(actual_stages) == len(summary['stages'])
    for name, expected_stage in frozen['successfulStages'].items():
        stage = actual_stages[name]
        assert stage == expected_stage and stage['exitCode'] == stage['actualProcessExitCode'] == 0
        assert stage['cleanupSignals'] == stage['remainingAdoptedChildren'] == stage['residualChildrenAtCompletion'] == 0
        assert stage['cleanupPoisoned'] is False and stage['failure'] is None and not stage['logTruncated']
        log = file_record(SELECTED_PATH / (name + '.txt'))
        assert log == {'bytes': stage['hashedLogBytes'], 'sha256': stage['logSha256']}
    assert actual_stages['host']['exitCode'] != 0, 'Historical failed host gate must remain failed'
    assert (SELECTED_PATH / 'source-head.txt').read_text().strip() == frozen['sourceHead']
    assert seal(frozen['proofBatches']) == frozen['proofBatchesSealSha256']
    with (SELECTED_PATH / 'worker/library/resources.csv').open() as stream:
        reader = csv.DictReader(stream)
        assert reader.fieldnames == ['TestResult.DisplayName', 'TestResult.Outcome', 'TestResult.Duration', 'TestResult.ResourceCount', 'RandomSeed']
        proof = [{'name': row['TestResult.DisplayName'], 'outcome': row['TestResult.Outcome'],
                  'seed': row['RandomSeed'], 'resourceCount': int(row['TestResult.ResourceCount'])} for row in reader]
    assert proof == frozen['proofBatches'] and len(proof) == 590 == len({row['name'] for row in proof})
    assert all(row['outcome'] == 'Passed' and row['seed'] == '0' and 0 <= row['resourceCount'] <= 100000000 for row in proof)
    assert sum(row['resourceCount'] for row in proof) == 178281304
    assert max(row['resourceCount'] for row in proof) == 21196571
    assert re.findall(r'Dafny program verifier finished with (\d+) verified, (\d+) errors?',
        (SELECTED_PATH / 'worker-bootstrap.txt').read_text()) == [('590', '0')]
    runtime_text = (SELECTED_PATH / 'worker-runtime.txt').read_text()
    names = re.findall(r'^(\w+\.\w+):', runtime_text, re.M)
    assert seal(frozen['runtimeControls']) == frozen['runtimeControlsSealSha256']
    assert names == [row['name'] for row in frozen['runtimeControls']] and len(names) == 37 == len(set(names))
    assert len(re.findall(r'PASSED$', runtime_text, re.M)) == 37 and not re.search(r'FAILED|HALT', runtime_text)
    manifest = read_bounded_json(SELECTED_PATH / 'worker/package/b3-worker-manifest.json')
    assert manifest == frozen['workerManifest']
    package = directory_records(SELECTED_PATH / 'worker/package')
    assert len(package) == 9 and set(manifest['files']) == {
        'B3Library.dll', 'DafnyB3Host.dll', 'DafnyB3Protocol.dll', 'DafnyB3Host.deps.json', 'DafnyB3Host.runtimeconfig.json'}
    for name, digest in manifest['files'].items():
        assert file_record(SELECTED_PATH / 'worker/package' / name)['sha256'] == digest
    assert file_record(SELECTED_PATH / 'worker/package/B3Library.dll') == file_record(SELECTED_PATH / 'worker/library/B3Library.dll')
    assert (solver_path().lstat().st_mode & 0o777) == 0o755
    prerequisite_rows = actual
    prerequisite_receipt = {'qualified': False, 'evidenceValidated': True, 'archivedOverallGatePassed': False, 'archivedHostQualified': False,
        'sourceHead': frozen['sourceHead'], 'runId': frozen['runId'], 'artifactId': frozen['artifactId'],
        'archive': archive_record(), 'selectedFiles': actual, 'selectedFilesSealSha256': seal(actual),
        'sourceClosureSealSha256': inputs['sourceClosureSealSha256'], 'sourceInventory': summary['sourceInventory'],
        'libraryProof': summary['libraryProof'], 'proofBatchNamesSealSha256': frozen['proofBatchesSealSha256'],
        'runtimeNamesSealSha256': frozen['runtimeControlsSealSha256'], 'runtimeCount': 37,
        'historicalSuccessfulStages': frozen['successfulStages'], 'workerManifest': manifest,
        'solverVersion': gathering['solverVersion'], 'libraryProofReused': True,
        'libraryVerifiedInThisRun': False, 'runtimeExecutedInThisRun': False, 'javaExecutedInThisRun': False}


def capture_cli_dependency_inputs():
    # Bind the actual SDK-selected net8 package/project closure and its package
    # bytes. Runtime copy checks use exact asset paths or a unique flat basename.
    path = Path('Source/Dafny/obj/project.assets.json')
    record = file_record(path)
    assert record['bytes'] <= 16 * 1024 * 1024
    assets = json.loads(path.read_text())
    assert assets['version'] == 3 and len(assets['targets']) == 1
    project = assets['project']
    restore = project['restore']
    expected = ROOT / 'Source/Dafny/Dafny.csproj'
    assert Path(restore['projectPath']).resolve() == expected and Path(restore['projectUniqueName']).resolve() == expected
    assert restore['projectName'] == 'Dafny' and restore['projectStyle'] == 'PackageReference'
    assert restore['originalTargetFrameworks'] == ['net8.0'] and set(project['frameworks']) == set(restore['frameworks']) == {'net8.0'}
    target_name, target = next(iter(assets['targets'].items()))
    assert target_name in {'net8.0', '.NETCoreApp,Version=v8.0'} and len(assets['packageFolders']) == 1
    package_root = Path(next(iter(assets['packageFolders'])))
    assert package_root.is_dir() and not package_root.is_symlink()
    assert Path(restore['packagesPath']).resolve() == package_root.resolve()
    boogie = pinned_boogie_assembly_entries()
    files = []
    projects = []
    copied = {row['path']: row for row in cli_rows}
    total = 0
    for library, declaration in sorted(target.items()):
        metadata = assets['libraries'][library]
        assert metadata['type'] == declaration['type']
        if metadata['type'] == 'project':
            actual = (expected.parent / metadata['msbuildProject']).resolve()
            relative = actual.relative_to(ROOT).as_posix()
            assert relative in source_tree and relative.endswith('.csproj') and actual.is_file()
            name = library.rsplit('/', 1)[0] + '.dll'
            assert name in copied, 'Current CLI lacks a resolved project assembly: ' + name
            projects.append({'library': library, 'projectPath': relative, 'source': file_record(actual),
                             'targetFramework': declaration['framework'], 'copied': copied[name]})
            continue
        assert metadata['type'] == 'package'
        folder = PurePosixPath(metadata['path'])
        assert not folder.is_absolute() and all(part not in {'.', '..'} for part in folder.parts)
        for kind in ['compile', 'runtime', 'native', 'runtimeTargets']:
            for asset_name in sorted(declaration.get(kind, {})):
                asset = PurePosixPath(asset_name)
                assert not asset.is_absolute() and all(part not in {'.', '..'} for part in asset.parts)
                if asset.name == '_._':
                    continue
                actual = package_root / folder / asset
                assert all(not ancestor.is_symlink() for ancestor in actual.parents)
                pin = file_record(actual)
                total += pin['bytes']
                assert total <= MAXIMUM_INVENTORY_BYTES and len(files) < MAXIMUM_INVENTORY_FILES
                row = {'library': library, 'assetKind': kind, 'assetPath': asset_name, 'actualPath': str(actual), **pin}
                if library.startswith('Boogie') and asset_name.endswith('.dll'):
                    assert library.rsplit('/', 1)[1] == '3.5.5-review.37e4435d'
                    matches = [item for item in boogie.get(asset.name, []) if item['bytes'] == pin['bytes'] and item['sha256'] == pin['sha256']]
                    assert matches, 'Resolved Boogie bytes are outside the fixed fork packages'
                    row['matchingPinnedPackageEntries'] = matches
                if kind in {'runtime', 'native', 'runtimeTargets'}:
                    rid = declaration[kind][asset_name].get('rid') if kind == 'runtimeTargets' else None
                    candidates = [asset_name] if asset_name in copied else []
                    if not candidates and (kind != 'runtimeTargets' or rid in {'linux-x64', 'linux', 'unix-x64', 'unix', 'any'}):
                        candidates = [asset.name] if asset.name in copied else []
                    if candidates:
                        assert len(set(candidates)) == 1, 'Ambiguous copied runtime asset path'
                        selected = copied[candidates[0]]
                        assert {key: selected[key] for key in pin} == pin, 'Copied runtime asset differs from its actual resolved package'
                        row['copiedPath'] = candidates[0]
                    else:
                        assert kind == 'runtimeTargets' and rid not in {'linux-x64', 'linux', 'unix-x64', 'unix', 'any'}, 'Missing current-platform runtime asset'
                        row['notSelectedForCurrentPlatform'] = True
                files.append(row)
    assert {'DafnyCore', 'DafnyB3Protocol', 'DafnyRuntime', 'DafnyDriver', 'DafnyPipeline'} <= {row['library'].rsplit('/', 1)[0] for row in projects}
    assert {'Boogie.Core', 'Boogie.ExecutionEngine', 'Boogie.VCGeneration'} <= {row['library'].split('/')[0] for row in files}
    return {'assetsFile': {'path': str(path), **record}, 'target': target_name,
            'packageRoot': str(package_root), 'projects': projects, 'files': files, 'sealSha256': seal(files)}


def capture_cli():
    global cli_rows, cli_dependencies, java_runtime_jar
    cli_rows = generated_records(CLI_PATH, ['Dafny.dll', 'Dafny.deps.json', 'Dafny.runtimeconfig.json',
        'DafnyCore.dll', 'DafnyB3Protocol.dll', 'DafnyRuntime.dll', 'DafnyDriver.dll', 'DafnyPipeline.dll', 'DafnyPrelude.bpl'])
    # Preserve first bytes even if a later dependency/configuration check fails.
    preserve_compiled_snapshot('cli-build', 'current-cli', CLI_PATH, cli_rows)
    assert next(row for row in cli_rows if row['path'] == 'DafnyPrelude.bpl')['sha256'] == file_record(Path('Source/DafnyCore/DafnyPrelude.bpl'))['sha256']
    runtime = read_bounded_json(CLI_PATH / 'Dafny.runtimeconfig.json')
    assert runtime['runtimeOptions']['tfm'] == 'net8.0'
    framework = runtime['runtimeOptions']['framework']
    assert framework['name'] == 'Microsoft.NETCore.App' and framework['version'].startswith('8.')
    deps = read_bounded_json(CLI_PATH / 'Dafny.deps.json')
    assert deps['runtimeTarget']['name'] in {'.NETCoreApp,Version=v8.0', 'net8.0'}
    assert set(deps['targets']) == {deps['runtimeTarget']['name']}
    for name in ['DafnyCore', 'DafnyB3Protocol', 'DafnyRuntime', 'DafnyDriver', 'DafnyPipeline']:
        matches = [item for library, item in deps['libraries'].items() if library.startswith(name + '/')]
        assert len(matches) == 1 and matches[0]['type'] == 'project'
    cli_dependencies = capture_cli_dependency_inputs()
    source_receipt['cliBoogieAssemblies'] = verify_boogie_assemblies(CLI_PATH)
    java_runtime_jar = file_record(Path('Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime-4.11.0.jar'))
    (output / 'cli-files.json').write_text(json.dumps({'files': cli_rows,
        'resolvedDependencyInputs': cli_dependencies, 'javaRuntimeBuildDependency': java_runtime_jar}, indent=2) + '\n')


def corpus_command(case):
    return ['dotnet', str((CLI_PATH / 'Dafny.dll').resolve()), 'verify',
        str((ROOT / 'Source/IntegrationTests/TestFiles/B3' / case['file']).resolve()),
        '--verification-backend', 'b3', '--b3-worker', str((SELECTED_PATH / 'worker/package/DafnyB3Host.dll').resolve()),
        '--solver-path', str(solver_path().resolve()), '--cores', '1', '--resource-limit', '200000',
        '--verification-time-limit', '20', '--show-snippets:false', '--use-basename-for-filename', '--progress', 'Batch']


def corpus_result(case, code, stdout, stderr):
    text = stdout + stderr
    error_bodies = '\n'.join(line.split('Error:', 1)[1] for line in text.splitlines() if 'Error:' in line)
    forbidden = re.findall(r'internal compilation exception|internal error occurred|was inconclusive|is unsupported|'
        r'ran into a tool error|was cancel(?:l)?ed|timed out|ran out of resources|ran out of memory|'
        r'B3\s+(?:unknown|unsupported|toolerror|cancelled|timedout|outofresource|outofmemory)\b', text, re.I)
    progress_pattern = r'^Verified (\d+)/(\d+) of ([^:\r\n]+): (.+) - (verified successfully|could not be verified) \(time: (.+), resource count: unavailable\)$'
    progress_lines = [line for line in text.splitlines() if re.match(r'^Verified \d+/\d+ of ', line)]
    progress = [re.fullmatch(progress_pattern, line) for line in progress_lines]
    well_formed = bool(progress) and all(progress)
    units = []
    if well_formed:
        groups = {}
        for match in progress:
            completed, total, symbol, description, outcome, duration = match.groups()
            row = {'completed': int(completed), 'total': int(total), 'symbol': symbol,
                   'description': description, 'outcome': outcome, 'duration': duration}
            units.append(row)
            groups.setdefault(symbol, []).append(row)
        well_formed = all(len(rows) == rows[0]['total'] > 0 and
            all(row['total'] == rows[0]['total'] for row in rows) and
            sorted(row['completed'] for row in rows) == list(range(1, rows[0]['total'] + 1)) for rows in groups.values())
    terminal = re.findall(r'Dafny program verifier finished with (\d+) verified, (\d+) errors?', text)
    matched = code == case['exitCode'] and not forbidden and well_formed and len(terminal) == 1
    if case['exitCode'] == 0:
        matched = matched and int(terminal[0][0]) > 0 and terminal[0][1] == '0' and not error_bodies
        matched = matched and all(row['outcome'] == 'verified successfully' for row in units)
    else:
        assert case == next(row for row in json.loads(INPUTS_PATH.read_text())['selectedCorpusCases'] if row['exitCode'] == 4)
        matched = matched and int(terminal[0][1]) > 0 and 'B3 failed:' in error_bodies and case['diagnostic'] in error_bodies
        matched = matched and any(row['outcome'] == 'could not be verified' for row in units)
    return {'name': case['name'], 'exitCode': code, 'expectedExitCode': case['exitCode'], 'matched': bool(matched),
        'expectedDiagnostic': case['diagnostic'], 'errorBodies': error_bodies, 'forbiddenOutcomes': forbidden,
        'allProgressUnitsCompleted': bool(well_formed), 'terminalSummary': terminal,
        'unitCountObserved': len(units), 'units': units, 'resourceCountAvailable': False}


def corpus_child(name):
    # A child receipt preserves actual CLI exit 4 while the outer owned stage
    # returns zero only for its exact expected Failed result. It does no SDK
    # build and no separate proof workload. Both original streams are retained.
    local = json.loads(INPUTS_PATH.read_text())
    case = next(row for row in local['selectedCorpusCases'] if row['name'] == name)
    command = corpus_command(case)
    stdout_path = OUTPUT_PATH / (name + '-stdout.txt')
    stderr_path = OUTPUT_PATH / (name + '-stderr.txt')
    code = capture_child_streams(command, stdout_path, stderr_path,
        MAXIMUM_CORPUS_CAPTURE_BYTES, MAXIMUM_CORPUS_CAPTURE_BYTES, MAXIMUM_CORPUS_CAPTURE_BYTES, 115)
    receipt = corpus_result(case, code, stdout_path.read_text(), stderr_path.read_text())
    receipt.update({'command': command, 'stdout': file_record(stdout_path), 'stderr': file_record(stderr_path),
                    'source': file_record(ROOT / 'Source/IntegrationTests/TestFiles/B3' / case['file'])})
    serialized = json.dumps(receipt, indent=2) + '\n'
    assert len(serialized.encode()) <= 16 * 1024 * 1024, 'Corpus receipt byte bound exceeded'
    (OUTPUT_PATH / (name + '-result.json')).write_text(serialized)
    print(json.dumps({'name': name, 'actualCliExitCode': code, 'matched': receipt['matched'],
        'result': file_record(OUTPUT_PATH / (name + '-result.json'))}))
    return 0 if receipt['matched'] else 1


def validate(name):
    global package_receipt, cli_version
    if name == 'source-head':
        gathering = read_bounded_json(output / 'source-head.txt')
        assert gathering['head'] == head and re.fullmatch(r'8\.\d+\.\d+', gathering['sdkVersion'])
        dotnet_root = Path(toolchain_rows['executable']['resolvedPath']).parent
        assert str(dotnet_root / 'sdk' / gathering['sdkVersion']) in {row['path'] for row in toolchain_rows['directories']}
        source_receipt['actualSdkSelection'] = gathering
    elif name == 'archived-prerequisites':
        qualify_archived_prerequisites()
    elif name == 'packages':
        package_receipt = package_records()
    elif name == 'cli-build':
        capture_cli()
    elif name == 'cli-version':
        cli_version = (output / 'cli-version.txt').read_text().strip()
        assert cli_version == '4.11.0+' + head, 'Current CLI informational identity differs'
    elif name in {row['name'] for row in inputs['selectedCorpusCases']}:
        case = next(row for row in inputs['selectedCorpusCases'] if row['name'] == name)
        result = read_bounded_json(output / (name + '-result.json'))
        assert result['stdout'] == file_record(output / (name + '-stdout.txt'))
        assert result['stderr'] == file_record(output / (name + '-stderr.txt'))
        actual = corpus_result(case, result['exitCode'], (output / (name + '-stdout.txt')).read_text(),
                               (output / (name + '-stderr.txt')).read_text())
        assert {key: result[key] for key in actual} == actual and actual['matched']
        assert result['command'] == corpus_command(case)
        assert result['source'] == file_record(ROOT / 'Source/IntegrationTests/TestFiles/B3' / case['file'])
        corpus_receipts.append(result)
    else:
        raise AssertionError('Unknown selected stage')


def capture_failure_observations(stage):
    record = {'stage': stage, 'role': 'current-cli', 'accepted': False, 'initialCaptured': cli_rows is not None,
              'initialSealSha256': seal(cli_rows) if cli_rows is not None else None,
              'observedFiles': None, 'observedSealSha256': None, 'delta': None,
              'artifactPath': None, 'copyComplete': False, 'observationFailure': None, 'copyFailure': None}
    failure_observations.append(record)
    try:
        if not CLI_PATH.exists() and not CLI_PATH.is_symlink():
            record.update({'missing': True, 'observedFiles': [], 'observedSealSha256': seal([]), 'delta': inventory_delta(cli_rows, [])})
            return
        observed = directory_records(CLI_PATH)
        record.update({'observedFiles': observed, 'observedSealSha256': seal(observed), 'delta': inventory_delta(cli_rows, observed)})
        first = next((row for row in compiled_snapshots if row['role'] == 'current-cli'), None)
        if cli_rows is not None and observed == cli_rows and first is not None and first['copyComplete']:
            assert directory_records(output / first['artifactPath']) == cli_rows
            record.update({'artifactPath': first['artifactPath'], 'copyComplete': True, 'usesInitialSnapshot': True})
            return
        destination = output / 'failure-observed' / (stage + '-current-cli')
        record['artifactPath'] = destination.relative_to(output).as_posix()
        try:
            copy_inventory_snapshot(CLI_PATH, destination, observed)
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
    if cli_rows is not None:
        try:
            assert directory_records(CLI_PATH) == cli_rows, 'Current CLI live bytes changed before export'
        except Exception as error:
            failures.append({'role': 'current-cli', 'scope': 'live-output', 'failure': type(error).__name__ + ': ' + str(error)})
            capture_failure_observations('final-export')
    for record in failure_observations:
        if record['copyComplete']:
            try:
                assert directory_records(output / record['artifactPath']) == record['observedFiles']
            except Exception as error:
                record['copyComplete'] = False
                record['copyFailure'] = type(error).__name__ + ': ' + str(error)
        for key in ['observationFailure', 'copyFailure']:
            if record[key] is not None:
                failures.append({'role': record['role'], 'scope': 'failure-observation', 'failure': record[key]})
    return failures


def capture_failed_corpus_receipt(case):
    # Failed stages remain failed. Retain their actual CLI result or explicitly
    # partial streams without treating either as an accepted expectation.
    path = output / (case['name'] + '-result.json')
    observation = {'name': case['name'], 'matched': False, 'accepted': False, 'partial': True,
                   'expectedExitCode': case['exitCode'], 'exitCode': None}
    try:
        if path.exists():
            candidate = read_bounded_json(path)
            stdout = output / (case['name'] + '-stdout.txt')
            stderr = output / (case['name'] + '-stderr.txt')
            assert candidate['stdout'] == file_record(stdout) and candidate['stderr'] == file_record(stderr)
            actual = corpus_result(case, candidate['exitCode'], stdout.read_text(), stderr.read_text())
            assert {key: candidate[key] for key in actual} == actual
            assert candidate['command'] == corpus_command(case)
            observation = {**candidate, 'accepted': False, 'partial': False}
        else:
            observation['streams'] = [{ 'path': stream.name, **file_record(stream)} for stream in
                [output / (case['name'] + '-stdout.txt'), output / (case['name'] + '-stderr.txt')] if stream.is_file()]
    except Exception as error:
        observation['observationFailure'] = type(error).__name__ + ': ' + str(error)
    corpus_receipts.append(observation)


def main():
    global inputs, output, head, source_tree, source_rows, toolchain_rows, gh_rows, source_receipt
    global prerequisite_rows, prerequisite_receipt, package_receipt, cli_rows, cli_dependencies, java_runtime_jar, cli_version
    global boundary_receipts, corpus_receipts, stage_environment, cleanup_poisoned
    global compiled_snapshots, compiled_stage_acceptance, failure_observations, STAGE_TIMEOUT_SECONDS, MAXIMUM_STAGE_LOG_BYTES
    output = OUTPUT_PATH
    output.mkdir(parents=True, exist_ok=False)
    head = source_tree = source_rows = toolchain_rows = gh_rows = None
    source_receipt = {}
    prerequisite_rows = prerequisite_receipt = package_receipt = cli_rows = cli_dependencies = java_runtime_jar = cli_version = None
    boundary_receipts = []
    corpus_receipts = []
    cleanup_poisoned = False
    compiled_snapshots = []
    compiled_stage_acceptance = {'cli-build': False}
    failure_observations = []
    stage_environment = {**os.environ, 'GRADLE_OPTS': '-Dorg.gradle.daemon=false', 'UseSharedCompilation': 'false',
        'DOTNET_PROCESSOR_COUNT': '1', 'DOTNET_GCHeapHardLimit': '40000000',
        'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER': '1', 'MSBUILDDISABLENODEREUSE': '1',
        'DafnyNormalizerReferenceDirectory': ''}
    results = []
    final_error = None
    preservation_failures = []
    try:
        assert Path('/proc/self/task').exists() and hasattr(os, 'pidfd_open') and hasattr(signal, 'pidfd_send_signal')
        assert not direct_children() and ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) == 0
        inputs = json.loads(INPUTS_PATH.read_text())
        validate_selection(inputs)
        assert not CLI_PATH.exists() and not ARCHIVE_PATH.parent.exists(), 'Require fresh current/prerequisite outputs'
        validate_source_initial()
        commands = [
            ('source-head', [sys.executable, str(Path(__file__).resolve()), '--source-head']),
            ('archived-prerequisites', [sys.executable, str(Path(__file__).resolve()), '--archived-prerequisites']),
            ('packages', ['sh', 'Scripts/fetch-boogie-packages.sh']),
            ('cli-build', ['dotnet', 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release', '-m:1',
                '-p:UseSharedCompilation=false', '-p:SourceRevisionId=' + head, '--nologo']),
            ('cli-version', ['dotnet', str((CLI_PATH / 'Dafny.dll').resolve()), '--version'])]
        commands.extend((case['name'], [sys.executable, str(Path(__file__).resolve()), '--corpus', case['name']])
                        for case in inputs['selectedCorpusCases'])
        assert len(commands) == 7
        for name, command in commands:
            STAGE_TIMEOUT_SECONDS = 120 if name in {case['name'] for case in inputs['selectedCorpusCases']} else 1800
            # The per-stage log ceiling stays at most 32 MiB. Reserve bounded
            # child evidence/snapshot space before each launch so the archive
            # itself, outside this tree, never consumes the upload budget.
            reserve = 4 * 1024 * 1024
            if name == 'archived-prerequisites':
                reserve += 48 * 1024 * 1024
            elif name == 'cli-build':
                reserve += 64 * 1024 * 1024
            elif name in {case['name'] for case in inputs['selectedCorpusCases']}:
                reserve += 24 * 1024 * 1024
            remaining = MAXIMUM_ARTIFACT_BYTES - sum(row['bytes'] for row in directory_records(output)) - reserve
            assert remaining >= 1024 * 1024, 'Insufficient bounded evidence space for next stage'
            MAXIMUM_STAGE_LOG_BYTES = min(32 * 1024 * 1024, remaining)
            result = run_with_boundaries(name, command)
            results.append(result)
            if name == 'archived-prerequisites' and prerequisite_receipt is not None:
                prerequisite_receipt['qualified'] = result['exitCode'] == 0 and not cleanup_poisoned
            if result['exitCode'] != 0:
                case = next((case for case in inputs['selectedCorpusCases'] if case['name'] == name), None)
                if case is not None and not any(row['name'] == name for row in corpus_receipts):
                    capture_failed_corpus_receipt(case)
            print(name, result['exitCode'], result.get('failure') or result.get('boundaryFailure') or '', flush=True)
            if result['exitCode'] != 0 or cleanup_poisoned:
                break
    except Exception as error:
        final_error = type(error).__name__ + ': ' + str(error)
    finally:
        try:
            preservation_failures = validate_compiled_artifact_snapshots()
        except Exception as error:
            preservation_failures.append({'scope': 'export', 'failure': type(error).__name__ + ': ' + str(error)})
        try:
            assert not direct_children(), 'Final owned scope is not empty'
        except Exception as error:
            cleanup_poisoned = True
            final_error = (final_error + '; ' if final_error else '') + str(error)
            try:
                signals = cleanup_children()
                final_error += '; final bounded cleanup signals=' + str(signals)
            except Exception as cleanup_error:
                final_error += '; final cleanup failed: ' + str(cleanup_error)
        passed = (len(results) == 7 and all(row['exitCode'] == 0 for row in results)
            and all(compiled_stage_acceptance.values()) and len(corpus_receipts) == 2
            and all(row['matched'] for row in corpus_receipts) and prerequisite_receipt is not None and prerequisite_receipt['qualified']
            and final_error is None and not preservation_failures and not cleanup_poisoned)
        receipt = {'passed': passed, 'scope': 'unsigned-if-guard-corpus', 'head': head,
            'focusedCorpusCountExpected': 2, 'focusedCorpusMatched': sum(row['matched'] for row in corpus_receipts),
            'archivedPrerequisitesQualified': prerequisite_receipt is not None and prerequisite_receipt['qualified'],
            'libraryProofReused': prerequisite_receipt is not None and prerequisite_receipt['qualified'], 'libraryVerifiedInThisRun': False,
            'runtimeExecutedInThisRun': False, 'completeLibraryVerified': False, 'libraryBinaryProduced': False,
            'completeNormalizerVerified': False, 'completeCorpusVerified': False, 'defaultCompatibilityVerified': False,
            'backendAccepted': False, 'fullNormalizerCountSourceDefined': 469, 'corpusCountSourceDefined': 79,
            'source': source_receipt, 'archivedPrerequisites': prerequisite_receipt,
            'stages': results, 'stageBoundaries': boundary_receipts, 'packages': package_receipt,
            'resolvedCliDependencies': cli_dependencies, 'currentCliVersion': cli_version,
            'javaRuntimeBuildDependency': java_runtime_jar, 'corpus': corpus_receipts, 'finalError': final_error,
            'compiledStageAcceptance': compiled_stage_acceptance, 'compiledSnapshots': compiled_snapshots,
            'failureObservations': failure_observations, 'artifactPreservationFailures': preservation_failures,
            'primaryFailure': next(({'stage': row['stage'], 'actualProcessExitCode': row.get('actualProcessExitCode'),
                'failure': row.get('failure'), 'boundaryFailure': row.get('boundaryFailure')}
                for row in results if row['exitCode'] != 0), None),
            'cleanupPoisoned': cleanup_poisoned, 'maximumArtifactBytes': MAXIMUM_ARTIFACT_BYTES,
            'provenanceScope': 'Filesystem byte pins at stage boundaries; not atomic execution-image attestation',
            'ownershipScope': 'Dedicated Linux child-subreaper/pidfd scope; no cgroup or arbitrary escape claim'}
        summary = output / 'summary.json'
        summary.write_text(json.dumps(receipt, indent=2) + '\n')
        try:
            rows = directory_records(output)
            assert sum(row['bytes'] for row in rows) < MAXIMUM_ARTIFACT_BYTES - 4 * 1024 * 1024
            (output / 'artifact-inventory.json').write_text(json.dumps({'files': rows, 'sealSha256': seal(rows)}, indent=2) + '\n')
            assert sum(row['bytes'] for row in directory_records(output)) <= MAXIMUM_ARTIFACT_BYTES
        except Exception as error:
            receipt['passed'] = False
            receipt['artifactFailure'] = type(error).__name__ + ': ' + str(error)
            summary.write_text(json.dumps(receipt, indent=2) + '\n')
        wording = 'PASS: two current corpus expectations only, with qualified reused prerequisites' if receipt['passed'] else 'NOT GREEN: two-case receipt incomplete or failed'
        print(wording, flush=True)
        if path := os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(path, 'a') as stream:
                stream.write('## Unsigned If guard two-case corpus receipt\n\n' + wording + '\n\n'
                    'The prerequisite gate remains failed overall. Library proof/runtime are reused, not executed here. '
                    'Complete normalizer/corpus/backend/default compatibility acceptance remain false.\n')
    return 0


if __name__ == '__main__':
    if sys.argv[1:] == ['--source-head']:
        source_head_child()
    elif sys.argv[1:] == ['--archived-prerequisites']:
        archive_child()
    elif len(sys.argv) == 3 and sys.argv[1] == '--corpus':
        raise SystemExit(corpus_child(sys.argv[2]))
    else:
        assert not sys.argv[1:]
        raise SystemExit(main())
