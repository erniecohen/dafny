"""Three fixed disposable nonproof controls; no solver/proof/parity acceptance.

The workflow records expected failures and exits zero. Acceptance requires this
summary, every exact control/ownership receipt, unchanged source pins and process
exit zero without cleanup intervention. Archived failed proof evidence stays failed.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import signal
import sys
import tarfile

SOURCES = Path('.github/review/alc-controls')
OUTPUT = Path('out/b3-native-compile')
SOURCE_SHA = '2fc847ab499ae08e398cbd748426d476589e1c74365348f7c6eb252e5c23ecfb'
BASELINE_ARCHIVE_SHA = '679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae'
CANDIDATE_SOURCE = 'a6132c6758c863ec4a897e4ac1534dc727f0b006'
CANDIDATE_MANIFEST_SHA = '3d592286e3a08229a3b4fca3a987a05725482604968d1c1d4709fce6496f1c96'
CANDIDATE_CORE_SHA = '92824cdc6b5247f7bc2418c78aa55dafe9c2a8f2572c9256e37daea8ea25a6d8'
NAMES = ['default-unavailable-demand', 'private-unavailable-demand', 'reactive-event-unavailable-demand']


def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def load_json(path, bound=8388608):
    with path.open('rb') as source:
        value = source.read(bound + 1)
    assert len(value) <= bound, 'JSON evidence exceeded its bound'
    return json.loads(value)


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def source_preflight():
    assert sys.platform == 'linux' and os.uname().machine == 'x86_64'
    assert sys.flags.optimize == 0, 'Receipt assertions must remain enabled'
    assert sha(SOURCES / 'source-manifest.json') == SOURCE_SHA
    manifest = load_json(SOURCES / 'source-manifest.json')
    pins = {f['path']: f for f in manifest['files']}
    assert len(pins) == len(manifest['files']) == 38
    actual = {p.relative_to(SOURCES).as_posix() for p in SOURCES.rglob('*') if p.is_file() and p.name != 'source-manifest.json'}
    assert set(pins) == actual
    for relative, pin in pins.items():
        path = SOURCES / relative
        assert not path.is_symlink() and path.stat().st_size == pin['bytes'] and sha(path) == pin['sha256']
    boundary = manifest['unavailableMetadataBoundary']
    assert boundary['controlCount'] == 3 and boundary['oneControlPerDisposableHost']
    assert not boundary['nativeProofEnabled'] and not boundary['negativeControlsExecuted']
    assert not boundary['completeMetadataAvailability'] and boundary['completeMetadataInventory']
    coordinator_manifest = load_json(Path('.github/review/b3-unavailable-coordinator-manifest.json'))
    assert coordinator_manifest['schemaVersion'] == 1 and coordinator_manifest['sourceManifestSha256'] == SOURCE_SHA
    expected = ['.github/review/B3-UNAVAILABLE-COORDINATOR.md', '.github/review/b3-owned-process.py',
                '.github/review/b3-unavailable-controls.py', '.github/review/base', '.github/workflows/review.yml']
    assert [f['path'] for f in coordinator_manifest['files']] == expected
    for pin in coordinator_manifest['files']:
        path = Path(pin['path'])
        assert path.stat().st_size == pin['bytes'] and sha(path) == pin['sha256']
    assert Path('.github/review/base').read_text().split() == ['v4.11.0', '8333daa60e2f2ee456068369f94c141898cde875']
    return manifest, coordinator_manifest


def safe_environment(download=False):
    allowed = ['PATH', 'HOME', 'TMPDIR', 'TMP', 'TEMP', 'LANG', 'LC_ALL', 'TZ', 'DOTNET_ROOT',
               'DOTNET_NOLOGO', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE',
               'DOTNET_PROCESSOR_COUNT', 'DOTNET_GCHeapHardLimit', 'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER',
               'MSBUILDDISABLENODEREUSE', 'NUGET_PACKAGES']
    result = {key: os.environ[key] for key in allowed if key in os.environ}
    result.update({'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER': '1', 'MSBUILDDISABLENODEREUSE': '1',
                   'DOTNET_PROCESSOR_COUNT': '1'})
    if download:
        assert os.environ.get('GH_TOKEN'), 'Public artifact download requires the workflow read token'
        result['GH_TOKEN'] = os.environ['GH_TOKEN']
    return result


def exact_package(directory, files):
    assert directory.is_dir() and not directory.is_symlink() and len(files) <= 4096
    pins = {f['path']: f for f in files}
    assert len(pins) == len(files)
    actual = set()
    for path in directory.rglob('*'):
        assert not path.is_symlink()
        if path.is_file():
            assert len(actual) < 4096
            relative = path.relative_to(directory).as_posix()
            assert relative in pins and path.stat().st_size == pins[relative]['bytes'] and sha(path) == pins[relative]['sha256']
            actual.add(relative)
    assert actual == set(pins)


OUTPUT.mkdir(parents=True, exist_ok=True)
receipt = {'schemaVersion': 1, 'scope': 'three fixed disposable nonproof unavailable-metadata controls',
           'passed': False, 'stages': [], 'controls': [], 'fixedControlOrder': NAMES,
           'nativeProofEnabled': False, 'solverExecuted': False, 'nativeQueryOrResourceParityEstablished': False}
owned = None
cancelled = False


class CoordinatorCancelled(BaseException):
    pass


def cancel(signum, frame):
    global cancelled
    receipt['passed'] = False
    receipt['cancellationRequested'] = True
    cleanup_active = owned.record_cancellation('Coordinator received signal ' + str(signum)) if owned is not None else False
    if not cancelled:
        cancelled = True
        if not cleanup_active:
            raise CoordinatorCancelled('Coordinator received signal ' + str(signum))
    # Repeated cancellation does not interrupt the bounded owned cleanup finally.


signal.signal(signal.SIGINT, cancel)
signal.signal(signal.SIGTERM, cancel)


def stage(name, command, timeout=120, download=False, reject_descendants=False):
    with (OUTPUT / (name + '.txt')).open('w') as log:
        evidence = owned.run_owned(command, log, safe_environment(download), timeout,
                                   reject_descendants=reject_descendants)
    evidence['stage'] = name
    receipt['stages'].append(evidence)
    print(name, evidence['exitCode'], 'PASS' if evidence['passed'] else 'NOT GREEN', flush=True)
    if not evidence['passed']:
        raise RuntimeError(name + ' failed its exact process/ownership contract')
    return evidence


try:
    source_manifest, coordinator_manifest = source_preflight()
    receipt['sourceManifestSha256'] = SOURCE_SHA
    receipt['coordinatorManifestSha256'] = sha(Path('.github/review/b3-unavailable-coordinator-manifest.json'))
    receipt['coordinatorSourcePins'] = coordinator_manifest['files']
    owned = module('b3_owned_process', Path('.github/review/b3-owned-process.py'))
    receipt['subreaper'] = owned.enable_subreaper()
    stage('source-head', ['git', 'rev-parse', 'HEAD'], 10)
    receipt['head'] = (OUTPUT / 'source-head.txt').read_text().strip()
    assert len(receipt['head']) == 40 and all(c in '0123456789abcdef' for c in receipt['head'])
    # The reviewed products are unchanged archived binaries. No candidate compiler
    # rebuild/version substitution occurs on this coordinator's source commit.
    baseline_input = OUTPUT / 'baseline-input'
    stage('baseline-input', ['gh', 'run', 'download', '37182760834', '-R', 'erniecohen/dafny',
                             '-n', 'baseline-dafny', '-D', str(baseline_input)], 180, download=True)
    archive = baseline_input / 'baseline.tar.gz'
    assert sha(archive) == BASELINE_ARCHIVE_SHA
    with tarfile.open(archive) as reader:
        members = reader.getmembers()
        assert len(members) <= 8192 and sum(m.size for m in members) <= 1073741824
        names = set()
        for member in members:
            name = member.name.rstrip('/')
            assert name == 'dafny' or name.startswith('dafny/')
            assert member.isfile() or member.isdir()
            assert not any(part in ['', '.', '..'] for part in name.split('/'))
            assert name not in names and 0 <= member.size <= 268435456
            assert (baseline_input / name).resolve().is_relative_to(baseline_input.resolve())
            names.add(name)
        reader.extractall(baseline_input, filter='data')
    baseline = (baseline_input / 'dafny').resolve()
    assert sha(baseline / 'DafnyCore.dll') == '9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9'
    archived = OUTPUT / 'archived-candidate-input'
    stage('archived-candidate-input', ['gh', 'run', 'download', '37221204349', '-R', 'erniecohen/dafny',
                                      '-n', 'b3-native-compile', '-D', str(archived)], 180, download=True)
    old = archived / 'out/b3-native-compile'
    historical = load_json(old / 'summary.json')
    assert historical['head'] == CANDIDATE_SOURCE and historical['passed'] is False
    assert historical['failure'] == 'RuntimeError: native-proof-smoke failed'
    assert historical['callerDelegationRemoved'] and not historical.get('callerDrainRequired', False)
    assert historical['candidatePackageManifestSha256'] == CANDIDATE_MANIFEST_SHA
    assert any(s['stage'] == 'candidate-build' and s['exitCode'] == 0 for s in historical['stages'])
    candidate_manifest = old / 'candidate-package-manifest.json'
    assert sha(candidate_manifest) == CANDIDATE_MANIFEST_SHA
    candidate_pin = load_json(candidate_manifest)
    assert candidate_pin['sourceCommit'] == CANDIDATE_SOURCE and candidate_pin['coreSha256'] == CANDIDATE_CORE_SHA
    assert candidate_pin['coreInformationalVersion'] == '4.11.0+' + CANDIDATE_SOURCE
    candidate = (archived / 'Binaries/net8.0').resolve()
    exact_package(candidate, candidate_pin['files'])
    assert sha(candidate / 'DafnyCore.dll') == CANDIDATE_CORE_SHA
    # Preserve the previous eleven-control qualification byte-for-byte as evidence
    # only; this new scope executes no lifecycle fixture, version probe or solver.
    lifecycle = old / 'previous-lifecycle/out/b3-native-compile'
    prior_summary = load_json(lifecycle / 'summary.json')
    assert prior_summary['head'] == '875e97e70198459993803a3ac5f41db703eb8938' and prior_summary['passed']
    assert prior_summary['callerDelegationRemoved'] and not prior_summary.get('callerDrainRequired', False)
    assert sha(lifecycle / 'lifecycle.json') == '60992bd42a47fbc9e3fca7294253d182947dd8ddfd4977d76361818bac2576bd'
    assert sha(lifecycle / 'harness/source-manifest.json') == 'e25b41f0d0fb6c29708d64cccc5c4b945bd64f177054156db667688243cf9a42'
    assert sha(lifecycle / 'harness/B3AlcGate.dll') == 'ec2e3910a0cdc4871a1c1c52b540c6e302b09fd83d1e47e509ab1d91855db680'
    old_files = {f['path']: f for f in load_json(lifecycle / 'harness/source-manifest.json')['files']}
    new_files = {f['path']: f for f in source_manifest['files']}
    assert len(old_files) == 19 and all(new_files[path] == pin for path, pin in old_files.items())
    receipt['archivedInputs'] = {'baselinePublicRun': 37182760834, 'baselineArchiveSha256': BASELINE_ARCHIVE_SHA,
        'candidatePublicRun': 37221204349, 'candidateSourceCommit': CANDIDATE_SOURCE,
        'candidatePackageManifestSha256': CANDIDATE_MANIFEST_SHA, 'candidateCoreSha256': CANDIDATE_CORE_SHA,
        'historicalCandidateProofSmokePassed': False, 'historicalCandidateSummarySha256': sha(old / 'summary.json'),
        'priorLifecycleReceiptSha256': sha(lifecycle / 'lifecycle.json'),
        'priorLifecycleSourceManifestSha256': sha(lifecycle / 'harness/source-manifest.json'),
        'oldQualifiedSourceFilesByteUnchanged': 19}
    dotnet = Path(shutil.which('dotnet')).resolve()
    receipt['dotnetExecutable'] = {'path': str(dotnet), 'sha256': sha(dotnet)}
    harness = OUTPUT / 'harness'
    stage('nonproof-harness-build', [str(dotnet), 'build', str(SOURCES / 'B3AlcGate.csproj'), '-c', 'Release',
                                  '-m:1', '-p:UseSharedCompilation=false',
                                  '-p:StartupObject=B3AlcGate.UnavailableMetadataProgram',
                                  '--output', str(harness), '--nologo'], 600)
    assert sha(harness / 'source-manifest.json') == SOURCE_SHA
    assert (harness / 'source-manifest.json').read_bytes() == (SOURCES / 'source-manifest.json').read_bytes()
    receipt['harnessAssemblySha256'] = sha(harness / 'B3AlcGate.dll')
    receipt['compiledSourceManifestSha256'] = sha(harness / 'source-manifest.json')
    receipt['harnessDependenciesSha256'] = sha(harness / 'B3AlcGate.deps.json')
    receipt['harnessRuntimeConfigSha256'] = sha(harness / 'B3AlcGate.runtimeconfig.json')
    inspector = module('b3_unavailable_receipt_validation', SOURCES / 'unavailable-metadata-receipt-validation.py')
    for name in NAMES:
        # Recheck all immutable inputs before each fresh disposable context host.
        assert sha(dotnet) == receipt['dotnetExecutable']['sha256']
        assert sha(harness / 'B3AlcGate.dll') == receipt['harnessAssemblySha256']
        assert sha(harness / 'source-manifest.json') == SOURCE_SHA
        assert sha(candidate_manifest) == CANDIDATE_MANIFEST_SHA and sha(archive) == BASELINE_ARCHIVE_SHA
        exact_package(candidate, candidate_pin['files'])
        input_file = OUTPUT / (name + '-inputs.json')
        control_file = OUTPUT / (name + '.json')
        assert not input_file.exists() and not control_file.exists()
        inputs = {'control': name, 'baselineDirectory': str(baseline), 'candidateDirectory': str(candidate),
                  'baselineArchive': str(archive.resolve()), 'candidatePackageManifest': {
                      'path': str(candidate_manifest.resolve()), 'sha256': CANDIDATE_MANIFEST_SHA},
                  'candidateSourceCommit': CANDIDATE_SOURCE, 'sourceManifest': {
                      'path': str((harness / 'source-manifest.json').resolve()), 'sha256': SOURCE_SHA},
                  'receipt': str(control_file.resolve())}
        input_file.write_text(json.dumps(inputs, indent=2) + '\n')
        process = stage(name, [str(dotnet), str((harness / 'B3AlcGate.dll').resolve()), '--inputs', str(input_file.resolve())],
                        50, reject_descendants=True)
        assert process['signalCount'] == 0 and process['signals'] == [] and not process['poisoned']
        assert len(process['ownedProcesses']) == 1 and process['ownedProcesses'][0]['isRoot']
        assert process['ownedProcesses'][0]['exitObserved'] and process['ownedProcesses'][0]['reaped']
        control = load_json(control_file)
        assert control['control'] == name and control['harnessAssemblySha256'] == receipt['harnessAssemblySha256']
        inspector.inspect_unavailable_control(control, sha, SOURCE_SHA)
        assert sha(harness / 'B3AlcGate.dll') == receipt['harnessAssemblySha256']
        receipt['controls'].append({'name': name, 'passed': True, 'receiptSha256': sha(control_file),
                                    'inputsSha256': sha(input_file), 'exactProcessStage': name})
    assert [c['name'] for c in receipt['controls']] == NAMES and not owned.child_ids(os.getpid())
    assert not cancelled
    receipt['passed'] = True
except BaseException as error:
    receipt['failure'] = type(error).__name__ + ': ' + str(error)
    print(receipt['failure'], flush=True)
finally:
    if owned is not None:
        try:
            receipt['remainingCoordinatorChildren'] = owned.child_ids(os.getpid())
            if receipt['remainingCoordinatorChildren']:
                receipt['passed'] = False
                receipt['failure'] = 'Coordinator retains children after bounded owned stage cleanup'
        except Exception as error:
            receipt['passed'] = False
            receipt['finalChildInspectionFailure'] = type(error).__name__ + ': ' + str(error)
    else:
        receipt['passed'] = False
    if cancelled:
        receipt['passed'] = False
    (OUTPUT / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write('Unavailable metadata nonproof controls: ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
        for item in receipt['stages']:
            summary.write('- ' + item['stage'] + ': exit ' + str(item['exitCode']) + '; process ownership ' +
                          ('PASS' if item['passed'] else 'NOT GREEN') + '\n')
        summary.write('\nOnly the three fixed disposable nonproof denial controls are in scope. The candidate package remains the exact archived a613 source; its earlier proof smoke remains NOT GREEN. No solver is executed, no native proof mode is enabled, and no native query/resource parity or fresh Boogie state is established. Job exit zero alone is not acceptance; inspect summary.json, the three control receipts and every owned-process ledger.\n')
