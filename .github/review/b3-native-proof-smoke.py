"""Fixed native proof smoke; job success never substitutes for strict receipts."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import time
import tarfile
import urllib.request
import uuid

output = Path('out/b3-native-compile')
output.mkdir(parents=True, exist_ok=True)
sources = Path('.github/review/alc-controls')
receipt = {'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'scope': 'six fixed native proof smoke controls; no full parity claim', 'passed': False, 'stages': []}
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



def package_inputs():
    baseline = output / 'baseline'
    stage('baseline-input', ['gh', 'run', 'download', '37182760834', '-R', 'erniecohen/dafny',
                             '-n', 'baseline-dafny', '-D', str(baseline)], 180)
    archive = baseline / 'baseline.tar.gz'
    assert sha(archive) == '679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae'
    with tarfile.open(archive) as reader:
        members = reader.getmembers()
        assert len(members) <= 8192 and sum(m.size for m in members) <= 1073741824
        names = set()
        for member in members:
            name = member.name.rstrip('/')
            assert name == 'dafny' or name.startswith('dafny/')
            assert member.isfile() or member.isdir()
            assert not any(p in ('', '.', '..') for p in name.split('/'))
            assert name not in names and member.size <= 268435456
            names.add(name)
            assert (baseline / name).resolve().is_relative_to(baseline.resolve())
        reader.extractall(baseline)
    assert sha(baseline / 'dafny/DafnyCore.dll') == '9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9'
    stage('packages', ['sh', 'Scripts/fetch-boogie-packages.sh'])
    stage('candidate-build', ['dotnet', 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release',
                              '-m:1', '-p:UseSharedCompilation=false', '--nologo'])
    candidate = Path('Binaries/net8.0').resolve()
    pins = []
    for path in sorted(candidate.rglob('*')):
        assert not path.is_symlink()
        if path.is_file():
            assert len(pins) < 4096 and path.stat().st_size <= 268435456
            pins.append({'path': path.relative_to(candidate).as_posix(), 'sha256': sha(path), 'bytes': path.stat().st_size})
    package_manifest = output / 'candidate-package-manifest.json'
    package_manifest.write_text(json.dumps({'schemaVersion': 1, 'sourceCommit': receipt['head'],
        'coreInformationalVersion': '4.11.0+' + receipt['head'], 'coreSha256': sha(candidate / 'DafnyCore.dll'),
        'files': pins}, indent=2) + '\n')
    receipt['candidatePackageManifestSha256'] = sha(package_manifest)
    receipt['candidateCoreSha256'] = sha(candidate / 'DafnyCore.dll')
    receipt['baselineArchiveSha256'] = sha(archive)
    return baseline, archive, candidate, package_manifest


def prior_inputs():
    prior = output / 'previous-lifecycle'
    stage('lifecycle-prerequisite-input', ['gh', 'run', 'download', '37215797270', '-R', 'erniecohen/dafny',
                                         '-n', 'b3-native-compile', '-D', str(prior)], 180)
    old = prior / 'out/b3-native-compile'
    summary = json.loads((old / 'summary.json').read_text())
    assert summary['head'] == '875e97e70198459993803a3ac5f41db703eb8938'
    assert summary['passed'] and summary['callerDelegationRemoved'] and not summary.get('callerDrainRequired', False)
    lifecycle = old / 'lifecycle.json'
    old_manifest = old / 'harness/source-manifest.json'
    assert sha(lifecycle) == '60992bd42a47fbc9e3fca7294253d182947dd8ddfd4977d76361818bac2576bd'
    assert sha(old_manifest) == 'e25b41f0d0fb6c29708d64cccc5c4b945bd64f177054156db667688243cf9a42'
    assert sha(old / 'harness/B3AlcGate.dll') == 'ec2e3910a0cdc4871a1c1c52b540c6e302b09fd83d1e47e509ab1d91855db680'
    controls = json.loads(lifecycle.read_text())
    names = ['missing-delegation', 'wrong-solver-pin', 'sealed-version', 'sealed-mutation',
             'creator-thread-exit', 'binary-streams-eof', 'relay-broken-pipe', 'timeout-reparented-descendant',
             'live-descendant-cap', 'refused-late-launch', 'malformed-completion']
    assert controls['passed'] and controls['effectiveUid'] == 0 and not controls['invokesVerification']
    assert controls['noProductLoaded'] and [c['name'] for c in controls['controls']] == names
    assert all(c['passed'] and c['noProductLoaded'] and c['zeroOwnedMembers'] for c in controls['controls'])
    old_files = {item['path']: item for item in json.loads(old_manifest.read_text())['files']}
    new_files = {item['path']: item for item in json.loads((sources / 'source-manifest.json').read_text())['files']}
    assert len(old_files) == 19 and all(new_files[name] == pin for name, pin in old_files.items())
    solver_source = old / 'z3'
    solver = (output / 'z3').resolve()
    solver_sha = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
    assert sha(solver_source) == solver_sha
    shutil.copyfile(solver_source, solver)
    solver.chmod(0o755)
    assert sha(solver) == solver_sha
    origin = sources / 'solver-origin-technical-evidence.json'
    technical = json.loads(origin.read_text())
    assert technical['sourceCommit'] == '0b6cdcdbc65da25ef0f73ac9da210574d0f66cf8'
    assert technical['solverSha256'] == solver_sha
    for item in technical['sourceFiles']:
        url = 'https://raw.githubusercontent.com/Z3Prover/z3/' + technical['sourceCommit'] + '/' + item['path']
        with urllib.request.urlopen(url, timeout=30) as response:
            data = response.read(1048577)
        assert len(data) <= 1048576 and hashlib.sha256(data).hexdigest() == item['sha256']
    receipt['solverSha256'] = solver_sha
    receipt['priorLifecycleReceiptSha256'] = sha(lifecycle)
    receipt['priorLifecycleSourceManifestSha256'] = sha(old_manifest)
    receipt['solverOriginEvidenceSha256'] = sha(origin)
    return lifecycle, old_manifest, solver, solver_sha, origin


def inspect_proofs(controls):
    assert controls['schemaVersion'] == 1 and controls['scope'] == 'prototype/six-fixed-native-proof-smoke-controls'
    assert controls['passed'] and controls['failureCode'] is None and controls['effectiveUid'] == 0
    assert not controls['ordinaryProofCliEnabled'] and not controls['nativeQueryOrResourceParityEstablished']
    assert not controls['remainingDirectChildren']
    assert controls['sourceManifestSha256'] == receipt['sourceManifestSha256']
    assert controls['harnessAssemblySha256'] == receipt['harnessAssemblySha256']
    assert controls['solverSha256'] == receipt['solverSha256'] and controls['solverVersion'] == '5.1.0'
    expected = ['baseline/true', 'candidate/true', 'baseline/reachable-false', 'candidate/reachable-false',
                'baseline/fuel', 'candidate/fuel']
    assert controls['fixedCaseOrder'] == expected and [c['name'] for c in controls['runs']] == expected
    assert controls['smokeBudgets'] == {'cores': 1, 'verificationTimeLimitSeconds': 20,
                                      'resourceLimit': 200000, 'invocationSafetySeconds': 60}
    for case in controls['runs']:
        assert case['passed'] and case['failureCode'] is None
        run, proof = case['run'], case['evidence']
        assert run['failure'] is None and run['contextCollected'] and not run['remainingDirectChildren']
        expected_exit = 4 if 'reachable-false' in case['name'] else 0
        assert run['exitCode'] == case['expectedExitCode'] == expected_exit
        assert proof['batches'] and proof['targetRoutineBatchCount'] > 0 and proof['assertionCount'] > 0
        assert proof['resourceCount'] > 0 and proof['finalRawVcResourceRepliesMatchJsonMultiset']
        assert not proof['nativeQueryOrCostParityClaimed']
        assert proof['versionProbeCount'] > 0 and proof['proofCheckCount'] >= proof['proofVcGroupCount'] == len(proof['batches'])
        assert len(proof['csvRows']) == len(proof['batches'])
        cleanup = run['proofCleanup']
        assert cleanup['recordedSolverGroups'] > 0 and cleanup['liveOwnedProcesses'] == 0
        path, digest = cleanup['evidence'].rsplit(' sha256=', 1)
        path = Path(path)
        assert path.resolve().is_relative_to(output.resolve()) and sha(path) == digest == proof['cleanupSha256']
        data = json.loads(path.read_text())
        assert data['liveOwnedProcesses'] == 0 and not data['populated'] and not data['finalMembers']
        assert data['leafRemoved'] and data['monitorStopped'] and data['admissionClosed']
        assert not data['fallbackRequests'] and not data['failures']
        assert len(data['streamReceipts']) == cleanup['recordedSolverGroups'] == len(proof['solverLaunches'])
        for stream in data['streamReceipts']:
            folder = path.parent / 'launches' / stream['launch']
            complete_path = folder / 'complete.json'
            assert sha(complete_path) == stream['completionSha256']
            assert complete_path.stat().st_size == stream['completionBytes']
            complete = json.loads(complete_path.read_text())
            assert not complete['errors'] and complete['token'] == data['token']
            for bound in stream['streams']:
                file = folder / (bound['name'] + '.bin')
                captured = complete['streams'][bound['name']]
                digest, size = sha(file), file.stat().st_size
                assert bound['valid'] and bound['sha256'] == digest and bound['bytes'] == size
                assert captured['readSha256'] == captured['writtenSha256'] == digest
                assert captured['readBytes'] == captured['writtenBytes'] == size
        assert all(b['outcome'] == 'Valid' for b in proof['batches']) if expected_exit == 0 else any(b['outcome'] == 'Invalid' for b in proof['batches'])
    receipt['nativeProofRuns'] = [{'name': case['name'], 'batches': len(case['evidence']['batches']),
                                  'resourceCount': case['evidence']['resourceCount']} for case in controls['runs']]


def smoke_before_delegation():
    manifest = json.loads((sources / 'source-manifest.json').read_text())
    inventory = {p.relative_to(sources).as_posix() for p in sources.rglob('*')
                 if p.is_file() and p.name != 'source-manifest.json'}
    assert {f['path'] for f in manifest['files']} == inventory
    for item in manifest['files']:
        path = sources / item['path']
        assert path.stat().st_size == item['bytes'] and sha(path) == item['sha256']
    receipt['sourceManifestSha256'] = sha(sources / 'source-manifest.json')
    baseline, archive, candidate, package_manifest = package_inputs()
    lifecycle, old_manifest, solver, solver_sha, origin = prior_inputs()
    stage('harness-build', ['dotnet', 'build', str(sources / 'B3AlcGate.csproj'), '-c', 'Release',
                           '-m:1', '-p:UseSharedCompilation=false',
                           '-p:StartupObject=B3AlcGate.NativeProofSmokeProgram',
                           '--output', str(output / 'harness'), '--nologo'])
    receipt['harnessAssemblySha256'] = sha(output / 'harness/B3AlcGate.dll')
    return baseline, archive, candidate, package_manifest, lifecycle, old_manifest, solver, solver_sha, origin


try:
    baseline, archive, candidate, package_manifest, lifecycle, old_manifest, solver, solver_sha, origin = smoke_before_delegation()
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
    input_file = output / 'proof-smoke-inputs.json'
    def pinned(path):
        return {'path': str(path.resolve()), 'sha256': sha(path)}
    inputs = {'baselineDirectory': str((baseline / 'dafny').resolve()), 'candidateDirectory': str(candidate),
              'baselineArchive': str(archive.resolve()), 'candidatePackageManifest': pinned(package_manifest),
              'candidateSourceCommit': receipt['head'], 'lifecycleReceipt': pinned(lifecycle),
              'lifecycleSourceManifest': pinned(old_manifest), 'solverOriginEvidence': pinned(origin),
              'solver': str(solver), 'solverSha256': solver_sha, 'cgroupParent': str(parent),
              'receipt': str((output / 'proof-smoke.json').resolve())}
    input_file.write_text(json.dumps(inputs, indent=2)+'\n')
    receipt['fixedInputsSha256'] = sha(input_file)
    stage('native-proof-smoke', ['sudo', '--', str(Path(shutil.which('timeout')).resolve()),
                                 '--kill-after=10s', '420s', str(Path(shutil.which('dotnet')).resolve()),
                                 str((output / 'harness/B3AlcGate.dll').resolve()),
                                 '--inputs', str(input_file.resolve())], 440)
    receipt['proofStageCompleted'] = True

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

                # Inspection is diagnostic; it must never suppress draining this
                # already-exclusive fd-anchored parent if that inspection fails.
                leaves = []
                requires_drain = True
                diagnostic_error = None
                try:
                    leaves = [name for name in os.listdir(parent_fd)
                              if stat.S_ISDIR(os.stat(name, dir_fd=parent_fd, follow_symlinks=False).st_mode)]
                    assert all(name.startswith('alc-') for name in leaves), 'Unexpected exclusive-parent subgroup'
                    parent_members = read_owned(parent_fd, 'cgroup.procs').split()
                    parent_populated = 'populated 1' in read_owned(parent_fd, 'cgroup.events')
                    requires_drain = bool(leaves or parent_members or parent_populated)
                except Exception as error:
                    diagnostic_error = type(error).__name__ + ': ' + str(error)
                    receipt['callerCleanupDiagnosticFailure'] = diagnostic_error
                    receipt['passed'] = False
                finally:
                    if requires_drain:
                        receipt['passed'] = False
                        receipt['callerDrainRequired'] = True
                        receipt.setdefault('failure', 'Owned state remained or could not be inspected; caller drain was required.')
                        try:
                            kill_fd = os.open('cgroup.kill', os.O_WRONLY | os.O_NOFOLLOW, dir_fd=parent_fd)
                            try:
                                assert os.write(kill_fd, b'1\n') == 2
                            finally:
                                os.close(kill_fd)
                        except Exception as error:
                            receipt['callerKillFailure'] = type(error).__name__ + ': ' + str(error)
                        # A failed kill still requires authoritative empty checks.
                        # Record both state reads independently, then fail closed.
                        state_errors = []
                        try:
                            deadline = time.monotonic() + 5
                            while 'populated 1' in read_owned(parent_fd, 'cgroup.events') and time.monotonic() < deadline:
                                time.sleep(0.05)
                        except Exception as error:
                            state_errors.append('wait: ' + type(error).__name__ + ': ' + str(error))
                        for name in ('cgroup.procs', 'cgroup.events'):
                            try:
                                value = read_owned(parent_fd, name)
                                assert (not value.split() if name == 'cgroup.procs' else 'populated 1' not in value), name + ' retained owned state'
                            except Exception as error:
                                state_errors.append(name + ': ' + type(error).__name__ + ': ' + str(error))
                        if state_errors:
                            receipt['callerEmptyCheckFailures'] = state_errors
                        assert not receipt.get('callerKillFailure') and not state_errors, 'Caller drain/empty checks failed'
                # Names only govern deletion after the unconditional drain path.
                assert diagnostic_error is None, diagnostic_error
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
        evidence_dirs = [p for p in output.iterdir() if p.name.startswith('proof-smoke-evidence-')]
        assert len(evidence_dirs) <= 1 and all(p.is_dir() and not p.is_symlink() for p in evidence_dirs)
        for evidence in evidence_dirs:
            subprocess.run(['sudo', 'chown', '-R', '--no-dereference',
                            str(os.getuid())+':'+str(os.getgid()), str(evidence.resolve())], check=True, timeout=20)
        receipt['privateEvidenceMadeReadable'] = True
    except Exception as error:
        receipt['passed'] = False
        receipt['evidenceOwnershipFailure'] = type(error).__name__ + ': ' + str(error)
    # Proof evidence is root-owned and private until the bounded native stage has
    # ended, the exclusive parent has drained/been removed, and only the new
    # evidence subtree has been transferred to the artifact caller. Never turn a
    # cleanup/intervention failure into success after checking proof logs.
    if (receipt.get('proofStageCompleted') and receipt.get('callerDelegationRemoved') and
            receipt.get('privateEvidenceMadeReadable') and not receipt.get('callerDrainRequired') and
            not any(receipt.get(key) for key in ('failure', 'callerCleanupFailure', 'evidenceOwnershipFailure'))):
        try:
            inspect_proofs(json.loads((output / 'proof-smoke.json').read_text()))
            receipt['passed'] = True
        except Exception as error:
            receipt['passed'] = False
            receipt['proofEvidenceFailure'] = type(error).__name__ + ': ' + str(error)
    (output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write('Fixed native proof smoke: ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
        for item in receipt['stages']:
            summary.write('- ' + item['stage'] + ': exit ' + str(item['exitCode']) + '\n')
        summary.write('\nThese six fixed privileged CI controls require actual ordinary native proof batches, raw solver evidence, owned cleanup and actual ALC collection. They do not establish unprivileged deployment or full native query/resource parity. Expected failures are recorded with exit zero; inspect summary.json and proof-smoke.json.\n')
