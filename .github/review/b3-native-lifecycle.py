"""Scratch lifecycle controls; job success never substitutes for strict receipts."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import time
import uuid

output = Path('out/b3-native-compile')
output.mkdir(parents=True, exist_ok=True)
sources = Path('.github/review/alc-controls')
receipt = {'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'scope': 'native lifecycle prerequisites only', 'passed': False, 'stages': []}
parent = None
parent_fd = None
parent_identity = None
parent_created = False


def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def stage(name, command, timeout=1200):
    with (output / (name + '.txt')).open('w') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
    receipt['stages'].append({'stage': name, 'exitCode': result.returncode, 'command': command})
    print(name, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(name + ' failed')


try:
    manifest = json.loads((sources / 'source-manifest.json').read_text())
    inventory = {p.relative_to(sources).as_posix() for p in sources.rglob('*')
                 if p.is_file() and p.name != 'source-manifest.json'}
    assert {f['path'] for f in manifest['files']} == inventory
    for item in manifest['files']:
        path = sources / item['path']
        assert path.stat().st_size == item['bytes'] and sha(path) == item['sha256'], item['path']
    receipt['sourceManifestSha256'] = sha(sources / 'source-manifest.json')
    inputs = output / 'solver-inputs'
    stage('solver-input', ['gh', 'run', 'download', '37201990430', '-R', 'erniecohen/dafny',
                          '-n', 'b3-native-compile', '-D', str(inputs)], 180)
    original = inputs / 'out/b3-native-compile'
    summary = json.loads((original / 'summary.json').read_text())
    assert summary['head'] == '89a3b25f96e916a758bd581cd1933e217b7b1352'
    solver_source = original / 'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
    solver_sha = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
    assert sha(solver_source) == solver_sha
    solver = (output / 'z3').resolve()
    shutil.copyfile(solver_source, solver)
    solver.chmod(0o755)
    assert subprocess.check_output([str(solver), '-version'], text=True).strip() == 'Z3 version 5.1.0 - 64 bit'
    shutil.rmtree(inputs)
    receipt['solverSha256'] = solver_sha
    stage('harness-build', ['dotnet', 'build', str(sources / 'B3AlcGate.csproj'), '-c', 'Release',
                            '-m:1', '-p:UseSharedCompilation=false', '--output', str(output / 'harness'), '--nologo'])
    receipt['harnessAssemblySha256'] = sha(output / 'harness/B3AlcGate.dll')
    compiler = Path(shutil.which('cc')).resolve()
    compiler_version = subprocess.check_output([str(compiler), '--version'], text=True, timeout=10)
    assert len(compiler_version) <= 4096
    fixture_source = sources / 'native-lifecycle-fixture.c'
    fixture_binary = output / 'harness/native-lifecycle-fixture'
    fixture_arguments = ['-std=c11', '-O2', '-Wall', '-Wextra', '-Werror',
                         str(fixture_source), '-o', str(fixture_binary)]
    stage('synthetic-fixture-build', [str(compiler), *fixture_arguments])
    build = {'schemaVersion': 1,
             'compiler': {'path': str(compiler), 'sha256': sha(compiler), 'version': compiler_version},
             'arguments': [str(compiler), *fixture_arguments], 'sourceSha256': sha(fixture_source),
             'binarySha256': sha(fixture_binary), 'sourceManifestSha256': receipt['sourceManifestSha256']}
    build_path = output / 'harness/native-lifecycle-fixture.build.json'
    build_path.write_text(json.dumps(build, indent=2) + '\n')
    receipt['syntheticFixtureSha256'] = build['binarySha256']
    receipt['fixtureSourceSha256'] = build['sourceSha256']
    receipt['fixtureBuildReceiptSha256'] = sha(build_path)
    parent = Path('/sys/fs/cgroup') / ('b3-alc-ci-' + uuid.uuid4().hex)
    stage('private-delegation', ['sudo', 'mkdir', '-m', '700', str(parent)], 20)
    parent_created = True
    parent_identity = (parent.stat().st_dev, parent.stat().st_ino)
    stage('delegate-owner', ['sudo', 'chown', str(os.getuid()) + ':' + str(os.getgid()),
                             str(parent), str(parent / 'cgroup.procs'), str(parent / 'cgroup.threads'),
                             str(parent / 'cgroup.subtree_control'), str(parent / 'cgroup.kill')], 20)
    parent_fd = os.open(parent, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    assert (os.fstat(parent_fd).st_dev, os.fstat(parent_fd).st_ino) == parent_identity
    parent_kill_fd = os.open('cgroup.kill', os.O_WRONLY | os.O_NOFOLLOW, dir_fd=parent_fd)
    os.close(parent_kill_fd)  # Permission preflight; no write or signal.
    assert (parent / 'cgroup.type').read_text().strip() == 'domain'
    assert (parent / 'cgroup.procs').read_text().strip() == ''
    # Configure only this new exclusive group. Refuse the descendant controls
    # without a kernel PID ceiling; never modify another group's configuration.
    assert (parent / 'cgroup.controllers').read_text().split().count('pids') == 1
    (parent / 'cgroup.subtree_control').write_text('+pids\n')
    assert (parent / 'pids.max').exists()
    stage('delegate-pid-ceiling', ['sudo', 'chown', str(os.getuid()) + ':' + str(os.getgid()), str(parent / 'pids.max')], 20)
    (parent / 'pids.max').write_text('300\n')
    receipt['delegatedPidCeiling'] = 300
    receipt['executionPrivilege'] = 'fixed-harness-root-cgroup-migration'
    stage('lifecycle-controls', ['sudo', '--', str(Path(shutil.which('timeout')).resolve()), '--kill-after=10s', '360s', str(Path(shutil.which('dotnet')).resolve()),
                                 str(output / 'harness/B3AlcGate.dll'), '--lifecycle-controls',
                                 '--solver', str(solver), '--solver-sha256', solver_sha,
                                 '--cgroup-parent', str(parent), '--receipt', str(output / 'lifecycle.json')], 380)
    controls = json.loads((output / 'lifecycle.json').read_text())
    required = ['missing-delegation', 'wrong-solver-pin', 'sealed-version', 'sealed-mutation',
                'creator-thread-exit', 'binary-streams-eof', 'relay-broken-pipe',
                'timeout-reparented-descendant', 'live-descendant-cap',
                'refused-late-launch', 'malformed-completion']
    assert controls['schemaVersion'] == 1 and controls['scope'] == 'prototype/native-lifecycle-controls'
    assert controls['effectiveUid'] == 0, 'This fixed CI gate requires explicit root migration privilege'
    assert controls['noProductLoaded'] is True and controls['invokesVerification'] is False
    assert controls['passed'] and [c['name'] for c in controls['controls']] == required
    assert all(c['passed'] and c['noProductLoaded'] and c['zeroOwnedMembers'] for c in controls['controls']), 'Every fixed lifecycle control must pass'
    assert all(c['ownedLeafRemoved'] for c in controls['controls'][2:]), 'Every launched control must remove its owned leaf'
    assert all(not c['ownedLeafRemoved'] and c['facts']['ownedLeafDisposition']=='never-created-preflight-rejection'
               for c in controls['controls'][:2]), 'Preflight controls must not fabricate leaf removal'
    assert controls['fixtureSourceSha256'] == receipt['fixtureSourceSha256']
    assert controls['fixtureSha256'] == receipt['syntheticFixtureSha256']
    assert controls['fixtureBuildReceiptSha256'] == receipt['fixtureBuildReceiptSha256']
    assert controls['sourceManifestSha256'] == receipt['sourceManifestSha256']
    assert controls['harnessAssemblySha256'] == receipt['harnessAssemblySha256']
    assert controls['solverSha256'] == solver_sha
    assert not any(p.is_dir() for p in parent.iterdir()), 'The harness retained owned leaves'
    receipt['passed'] = True
except Exception as error:
    receipt['failure'] = type(error).__name__ + ': ' + str(error)
    print(receipt['failure'], flush=True)
finally:
    # Anchor the exclusive group created by this caller. Parent-wide kill also
    # drains a controlled process that migrated out of its harness-owned leaf.
    # Any caller intervention retains a failed infrastructure verdict.
    if parent_created:
        try:
            if parent_fd is None:
                # No native process can have launched before delegation finished.
                # Refuse signaling without the caller's open directory handle.
                assert not parent.is_symlink() and (parent.stat().st_dev, parent.stat().st_ino) == parent_identity
                subprocess.run(['sudo', 'rmdir', str(parent)], check=True, timeout=20)
            else:
                def read_owned(directory, name):
                    fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=directory)
                    try:
                        value = os.read(fd, 65537)
                        assert len(value) <= 65536, 'Owned cgroup state exceeded its bound'
                        return value.decode('ascii')
                    finally:
                        os.close(fd)

                leaves = os.listdir(parent_fd)
                leaves = [name for name in leaves
                          if stat.S_ISDIR(os.stat(name, dir_fd=parent_fd, follow_symlinks=False).st_mode)]
                assert all(name.startswith('alc-') for name in leaves), 'Unexpected exclusive-parent subgroup'
                parent_members = read_owned(parent_fd, 'cgroup.procs').split()
                parent_populated = 'populated 1' in read_owned(parent_fd, 'cgroup.events')
                if leaves or parent_members or parent_populated:
                    receipt['passed'] = False
                    receipt['callerDrainRequired'] = True
                    receipt['failure'] = 'The harness retained owned groups or parent members; caller drain was required.'
                    kill_fd = os.open('cgroup.kill', os.O_WRONLY | os.O_NOFOLLOW, dir_fd=parent_fd)
                    try:
                        assert os.write(kill_fd, b'1\n') == 2
                    finally:
                        os.close(kill_fd)
                    deadline = time.monotonic() + 5
                    while 'populated 1' in read_owned(parent_fd, 'cgroup.events') and time.monotonic() < deadline:
                        time.sleep(0.05)
                assert not read_owned(parent_fd, 'cgroup.procs').split(), 'Caller parent still has owned members'
                assert 'populated 1' not in read_owned(parent_fd, 'cgroup.events'), 'Caller parent still populated'
                for name in leaves:
                    leaf_fd = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent_fd)
                    try:
                        assert not read_owned(leaf_fd, 'cgroup.procs').split(), 'Owned leaf still has members'
                        assert 'populated 1' not in read_owned(leaf_fd, 'cgroup.events'), 'Owned leaf still populated'
                    finally:
                        os.close(leaf_fd)
                    os.rmdir(name, dir_fd=parent_fd)
                assert (parent.stat().st_dev, parent.stat().st_ino) == parent_identity, 'Caller parent identity changed'
                subprocess.run(['sudo', 'rmdir', '--', str(parent)], check=True, timeout=20)
            receipt['callerDelegationRemoved'] = True
        except Exception as error:
            receipt['passed'] = False
            receipt['callerCleanupFailure'] = type(error).__name__ + ': ' + str(error)
        finally:
            if parent_fd is not None:
                os.close(parent_fd)
    # The fixed privileged harness creates private evidence. After its bounded
    # process stage and exclusive-group cleanup, make only its newly created
    # evidence subtree traversable by this caller's artifact uploader.
    try:
        evidence_dirs = [p for p in output.iterdir() if p.name.startswith('lifecycle-evidence-')]
        assert len(evidence_dirs) <= 1 and all(p.is_dir() and not p.is_symlink() for p in evidence_dirs)
        for evidence in evidence_dirs:
            subprocess.run(['sudo', 'chown', '-R', '--no-dereference',
                            str(os.getuid())+':'+str(os.getgid()), str(evidence.resolve())], check=True, timeout=20)
        receipt['privateEvidenceMadeReadable'] = True
    except Exception as error:
        receipt['passed'] = False
        receipt['evidenceOwnershipFailure'] = type(error).__name__ + ': ' + str(error)
    (output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write('Native lifecycle prerequisites: ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
        for item in receipt['stages']:
            summary.write('- ' + item['stage'] + ': exit ' + str(item['exitCode']) + '\n')
        summary.write('\nThese fixed privileged CI runtime controls do not establish unprivileged cgroup delegation, do not invoke Dafny verification or establish native proof-cost parity. Expected failures are recorded with exit zero; inspect summary.json and lifecycle.json.\n')
