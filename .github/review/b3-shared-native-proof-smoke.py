"""Fixed six-call coordinator source. No forwarded native arguments or proof parity claim."""
import sys
sys.dont_write_bytecode=True
import hashlib,importlib.util,json,os,shutil,signal,stat,time,uuid,urllib.request,zipfile
from pathlib import Path
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

def pinned_bytes(path, expected_sha, expected_bytes=None, bound=16777216):
    assert path.is_file() and not path.is_symlink(), 'Pinned input is not a regular file'
    with path.open('rb') as source:
        value = source.read(bound + 1)
    assert len(value) <= bound, 'Pinned input exceeded its bound'
    assert expected_bytes is None or len(value) == expected_bytes, 'Pinned input size changed'
    assert hashlib.sha256(value).hexdigest() == expected_sha, 'Pinned input bytes changed'
    return value

def file_pin(path):
    assert path.is_file() and not path.is_symlink(), 'Built input is not a regular file'
    with path.open('rb') as source:
        value = source.read(16777217)
    assert len(value) <= 16777216, 'Built input exceeded its bound'
    return {'path': str(path.resolve()), 'sha256': hashlib.sha256(value).hexdigest(), 'bytes': len(value)}

def module(name, path, pin):
    assert sys.dont_write_bytecode and name not in sys.modules
    # Compile exactly the checked source capture. SourceFileLoader.exec_module
    # would be allowed to consume an existing .pyc even with bytecode writes off.
    captured = pinned_bytes(path, pin['sha256'], pin['bytes'])
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    try:
        exec(compile(captured, str(path), 'exec', dont_inherit=True), value.__dict__)
    except BaseException:
        sys.modules.pop(name, None)
        raise
    receipt['importedReviewedSources'].append({'path': str(path), 'sha256': pin['sha256'],
        'bytes': len(captured), 'capturedSourceCompiled': True, 'cachedPythonBodyUsed': False})
    return value

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

def post_build_inputs(host_pin, compiled_manifest, archived_pins, build_failed):
    # Preserve the SDK/ownership failure rather than replacing it with a missing
    # output-file assertion. Source/host checks run independently of SDK outputs.
    failures = []
    try:
        recheck_boundary('after-build-sources', host_pin, archived_pins=archived_pins)
    except BaseException as error:
        failures.append(error)
    compiled = {'buildStageFailed': build_failed, 'hashVerified': False}
    receipt['compiledManifestAfterBuild'] = compiled
    try:
        if compiled_manifest.exists() or compiled_manifest.is_symlink():
            compiled['state'] = 'present'
            value = pinned_bytes(compiled_manifest, SOURCE_SHA)
            assert value == pinned_bytes(SOURCES / 'source-manifest.json', SOURCE_SHA)
            compiled.update({'hashVerified': True, 'sha256': SOURCE_SHA, 'bytes': len(value)})
        else:
            compiled['state'] = 'absent'
            if not build_failed:
                raise AssertionError('Successful build has no compiled source manifest')
    except BaseException as error:
        compiled['failure'] = type(error).__name__ + ': ' + str(error)
        failures.append(error)
    if failures:
        receipt['postBuildInputFailures'] = [type(error).__name__ + ': ' + str(error) for error in failures]
        if not build_failed:
            raise failures[0]

class CoordinatorCancelled(BaseException):
    pass

def cancel(signum, frame):
    global cancelled
    receipt['passed'] = False
    receipt['cancellationRequested'] = True
    cleanup_active = owned.record_cancellation('Coordinator received signal ' + str(signum)) if owned is not None else False
    if not cancelled:
        cancelled = True
        if not cleanup_active and not coordinator_cleanup_active:
            raise CoordinatorCancelled('Coordinator received signal ' + str(signum))

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

SOURCES=Path('.github/review/alc-controls')
OUTPUT=Path('out/b3-native-compile')
SOURCE_SHA='b2e0e676f8c279e25358330ac3a91e740a12ddce319fd9aaedc5f1bd508dbc90'
QUALIFIED_SOURCE_SHA='4230adf2ba624d42574d59ed95d4af1e28346d3260ffb79bf4249f1c80097a05'
QUALIFIED_ZIP_SHA='4f4b07ddac0ca8d5d8876a349ce6871d12d0dcf774c757971dabe29bf8ce7a29'
QUALIFIED_SUMMARY_SHA='74950dca1707fc774add3ee067ea4939ec76dfeb16c1edb2fa1f5af894b8688b'
BASELINE_ARCHIVE_SHA='679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae'
CANDIDATE_SOURCE='a6132c6758c863ec4a897e4ac1534dc727f0b006'
CANDIDATE_MANIFEST_SHA='3d592286e3a08229a3b4fca3a987a05725482604968d1c1d4709fce6496f1c96'
QUALIFIED_CONTROL_SHAS=['25205d9db1cf088d937a374ba8044725cfc82d3f886ccb3de9a009375f24b0b1',
 'aca632876dbb7b9b05e5625b122d93d5982c4d40954bc3513673cabbf1d7cadc',
 '09ddb7180ba82ce709cffc64da49c7fa62d872226cca0bfb6256e0493cf8546b']
SOLVER_SHA='b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
OUTPUT.mkdir(parents=True,exist_ok=True)
receipt={'schemaVersion':2,'scope':'fixed-six-qualified-shared-native-smoke-no-default-parity',
 'passed':False,'stages':[],'immutableInputChecks':[],'importedReviewedSources':[],
 'pythonBytecodeWritesDisabled':True,'ordinaryProofCliEnabled':False,'nativeQueryOrResourceParityEstablished':False}
owned=None;cancelled=False;coordinator_cleanup_active=False
parent=None;parent_fd=None;parent_identity=None;parent_created=False;dotnet_pin=None;bundle=None
packet=Path('out/b3-shared-qualified.zip');qualified=None;qualified_controls=[];native_inputs=None


def frozen_source_pins(expected_coordinator_sha=None):
    manifest=json.loads(pinned_bytes(SOURCES/'source-manifest.json',SOURCE_SHA))
    pins={f['path']:f for f in manifest['files']}
    assert len(pins)==len(manifest['files']) and 38<len(pins)<=64
    for relative,pin in pins.items():
        assert not Path(relative).is_absolute() and '\\' not in relative and all(p not in ['', '.', '..'] for p in relative.split('/'))
        pinned_bytes(SOURCES/relative,pin['sha256'],pin['bytes'])
    boundary=manifest['qualifiedSharedNativeSmokeBoundary']
    assert boundary['fixedProofCases']==6 and boundary['runtimeQualified'] is False
    assert boundary['normalProgramProofModeEnabled'] is False and boundary['completeMetadataAvailability'] is False
    outer_path=Path('.github/review/b3-shared-native-coordinator-manifest.json')
    outer_bytes=read_bounded(outer_path,1048576);outer_sha=hashlib.sha256(outer_bytes).hexdigest()
    assert expected_coordinator_sha is None or outer_sha==expected_coordinator_sha
    outer=json.loads(outer_bytes)
    assert outer['sourceManifestSha256']==SOURCE_SHA and outer['schemaVersion']==1
    expected=['.github/review/B3-SHARED-NATIVE-SMOKE.md','.github/review/b3-owned-process.py',
      '.github/review/b3-shared-native-proof-smoke.py','.github/review/base','.github/workflows/review.yml']
    assert [f['path'] for f in outer['files']]==expected
    for f in outer['files']:pinned_bytes(Path(f['path']),f['sha256'],f['bytes'])
    assert Path('.github/review/base').read_bytes()==b'v4.11.0 8333daa60e2f2ee456068369f94c141898cde875\n'
    return manifest,outer,outer_sha


def read_bounded(path,bound):
    path=Path(path)
    assert path.is_file() and not path.is_symlink()
    with path.open('rb') as source:value=source.read(bound+1)
    assert len(value)<=bound
    return value


def recheck_boundary(name,host_pin,harness_pins=None,fresh_harness=None,archived_pins=None,compiled_manifest=None):
    check={'boundary':name,'passed':False};receipt['immutableInputChecks'].append(check)
    try:
        manifest,outer,outer_sha=frozen_source_pins(receipt['coordinatorManifestSha256'])
        pinned_bytes(Path(host_pin['path']),host_pin['sha256'],host_pin['bytes'])
        assert sha(packet)==QUALIFIED_ZIP_SHA and packet.stat().st_size==80709394
        assert sha(OUTPUT/'qualified-summary.json')==QUALIFIED_SUMMARY_SHA
        for i,name in enumerate(constants.NAMES):
            assert sha(OUTPUT/(name+'.json'))==QUALIFIED_CONTROL_SHAS[i]
        for product in qualified_controls[0]['products']:exact_package(Path(product['directory']),product['files'])
        assert sha(OUTPUT/'baseline-input/baseline.tar.gz')==BASELINE_ARCHIVE_SHA
        assert sha(OUTPUT/'archived-candidate-input/out/b3-native-compile/candidate-package-manifest.json')==CANDIDATE_MANIFEST_SHA
        # Historical bundle remains intact and distinct from the new proof build.
        for name,pin in qualified['harnessBundlePins'].items():
            pinned_bytes(Path(pin['path']),pin['sha256'],pin['bytes'])
        if fresh_harness is not None:assert not fresh_harness.exists()
        if compiled_manifest is not None:assert pinned_bytes(compiled_manifest,SOURCE_SHA)==pinned_bytes(SOURCES/'source-manifest.json',SOURCE_SHA)
        if harness_pins is not None:
            assert set(harness_pins)=={'B3AlcGate.dll','B3AlcGate.deps.json','B3AlcGate.runtimeconfig.json','source-manifest.json'}
            for pin in harness_pins.values():pinned_bytes(Path(pin['path']),pin['sha256'],pin['bytes'])
            assert harness_pins['source-manifest.json']['sha256']==SOURCE_SHA
            check['harnessBundleSha256']={k:p['sha256'] for k,p in harness_pins.items()}
        if 'solverSha256' in receipt:assert sha(OUTPUT/'shared-z3')==SOLVER_SHA
        if native_inputs is not None:assert sha(OUTPUT/'shared-proof-inputs.json')==receipt['fixedInputsSha256']
        check.update({'passed':True,'sourceManifestSha256':SOURCE_SHA,
          'declaredSourceFilesChecked':len(manifest['files']),'coordinatorManifestSha256':outer_sha,
          'declaredCoordinatorFilesChecked':len(outer['files']),'dotnetExecutableSha256':host_pin['sha256'],
          'nonproofQualificationZipSha256':QUALIFIED_ZIP_SHA,'completeArchivedPackagesRechecked':True})
    except BaseException as error:
        check['failure']=type(error).__name__+': '+str(error);raise


def inspect_qualification():
    global qualified,qualified_controls,constants
    assert packet.stat().st_size==80709394 and sha(packet)==QUALIFIED_ZIP_SHA
    with zipfile.ZipFile(packet) as archive:
        entries=archive.infolist()
        assert len(entries)==1192 and sum(i.file_size for i in entries)<=1073741824
        names=set()
        for item in entries:
            name=item.filename
            assert name.startswith('out/b3-native-compile/') and not item.is_dir()
            assert '\\' not in name and all(p not in ['', '.', '..'] for p in name.split('/'))
            assert name not in names and item.file_size<=268435456
            assert stat.S_IFMT(item.external_attr>>16) in [0,stat.S_IFREG]
            target=Path(name);assert not target.exists() and not target.is_symlink()
            assert target.resolve().is_relative_to(OUTPUT.resolve())
            names.add(name)
        archive.extractall(Path('.'))
    assert sha(OUTPUT/'summary.json')==QUALIFIED_SUMMARY_SHA
    shutil.copyfile(OUTPUT/'summary.json',OUTPUT/'qualified-summary.json')
    qualified=load_json(OUTPUT/'qualified-summary.json')
    assert qualified['head']=='71fc9dcff677f80d43ef3cb7c767806513c438a4' and qualified['passed'] is True
    assert qualified['sourceManifestSha256']==QUALIFIED_SOURCE_SHA
    assert qualified['coordinatorManifestSha256']=='383c8bffa310bf0ede4322e8679fa7026cf3e3733504404ee987d08f1d262b81'
    assert qualified['remainingCoordinatorChildren']==[] and qualified['nativeProofEnabled'] is False and qualified['solverExecuted'] is False
    old_source=load_json(OUTPUT/'harness/source-manifest.json')
    assert sha(OUTPUT/'harness/source-manifest.json')==QUALIFIED_SOURCE_SHA
    current={f['path']:f for f in load_json(SOURCES/'source-manifest.json')['files']}
    assert len(old_source['files'])==38
    assert all(current[f['path']]==f for f in old_source['files'] if f['path'] not in ['NativeProofSmokeControls.cs','SharedNativeRuns.cs'])
    constants=module('shared_native_qualified_nonproof_inspector',SOURCES/'unavailable-metadata-receipt-validation.py',
      current['unavailable-metadata-receipt-validation.py'])
    expected=['before-build','after-build-sources','after-build']
    expected += [prefix+n for n in constants.NAMES for prefix in ['before-','after-']]
    expected += ['final']
    assert [c['boundary'] for c in qualified['immutableInputChecks']]==expected
    assert all(c['passed'] is True for c in qualified['immutableInputChecks'])
    assert [c['name'] for c in qualified['controls']]==constants.NAMES
    for stage_data in qualified['stages']:
        assert stage_data['passed'] is True and stage_data['exitCode']==0 and stage_data['poisoned'] is False
        assert stage_data['failures']==[] and stage_data['signalCount']==0 and stage_data['signals']==[] and stage_data['remainingDirectChildren']==[]
        assert all(p['exitObserved'] is True and p['reaped'] is True for p in stage_data['ownedProcesses'])
    for i,name in enumerate(constants.NAMES):
        path=OUTPUT/(name+'.json')
        assert sha(path)==QUALIFIED_CONTROL_SHAS[i]==qualified['controls'][i]['receiptSha256']
        c=load_json(path)
        constants.inspect_unavailable_control(c,sha,QUALIFIED_SOURCE_SHA)
        process=next(s for s in qualified['stages'] if s['stage']==name)
        assert process['rejectObservedDescendants'] is True and len(process['ownedProcesses'])==1
        assert process['ownedProcesses'][0]['isRoot'] is True and process['transientDescendantObservations']==[]
        qualified_controls.append(c)
    assert qualified['dotnetExecutable']['path']==str(Path(shutil.which('dotnet')).resolve())
    pinned_bytes(Path(qualified['dotnetExecutable']['path']),qualified['dotnetExecutable']['sha256'],qualified['dotnetExecutable']['bytes'])
    for name,pin in qualified['harnessBundlePins'].items():pinned_bytes(Path(pin['path']),pin['sha256'],pin['bytes'])
    receipt['nonproofQualification']={'publicRun':37238572256,'artifactId':11316129057,'zipSha256':QUALIFIED_ZIP_SHA,
      'summarySha256':QUALIFIED_SUMMARY_SHA,'receiptSha256':QUALIFIED_CONTROL_SHAS,
      'allThreeStrictControlsInspected':True,'allTenIntegrityBoundariesPassed':True}


def prepare_solver():
    old=OUTPUT/'archived-candidate-input/out/b3-native-compile/previous-lifecycle/out/b3-native-compile'
    lifecycle=old/'lifecycle.json';old_manifest=old/'harness/source-manifest.json'
    assert sha(lifecycle)=='60992bd42a47fbc9e3fca7294253d182947dd8ddfd4977d76361818bac2576bd'
    assert sha(old_manifest)=='e25b41f0d0fb6c29708d64cccc5c4b945bd64f177054156db667688243cf9a42'
    old_pins=load_json(old_manifest)['files'];current={f['path']:f for f in load_json(SOURCES/'source-manifest.json')['files']}
    assert len(old_pins)==19 and all(current[f['path']]==f for f in old_pins)
    solver=OUTPUT/'shared-z3';assert not solver.exists()
    assert sha(old/'z3')==SOLVER_SHA
    shutil.copyfile(old/'z3',solver);solver.chmod(0o755);assert sha(solver)==SOLVER_SHA
    origin=SOURCES/'solver-origin-technical-evidence.json'
    assert sha(origin)=='a64314c9ee010e48e19bdf9ede2ae6260b2dfbd857540a9b1c06010c5e6638af'
    technical=load_json(origin)
    for pin in technical['sourceFiles']:
        url='https://raw.githubusercontent.com/Z3Prover/z3/'+technical['sourceCommit']+'/'+pin['path']
        with urllib.request.urlopen(url,timeout=30) as response:data=response.read(1048577)
        assert len(data)<=1048576 and hashlib.sha256(data).hexdigest()==pin['sha256']
    receipt['solverSha256']=SOLVER_SHA
    return lifecycle,old_manifest,solver,origin


try:
    assert len(sys.argv)==1 and sys.platform=='linux' and os.uname().machine=='x86_64' and sys.flags.optimize==0
    source,outer,outer_sha=frozen_source_pins()
    actual={p.relative_to(SOURCES).as_posix() for p in SOURCES.rglob('*') if p.is_file() and p.name!='source-manifest.json'}
    assert actual=={f['path'] for f in source['files']}
    receipt['sourceManifestSha256']=SOURCE_SHA;receipt['coordinatorManifestSha256']=outer_sha
    outer_pins={f['path']:f for f in outer['files']}
    owned=module('shared_native_owned_process',Path('.github/review/b3-owned-process.py'),outer_pins['.github/review/b3-owned-process.py'])
    receipt['subreaper']=owned.enable_subreaper()
    signal.signal(signal.SIGINT,cancel);signal.signal(signal.SIGTERM,cancel)
    # The exact ZIP is retained; this stage alone receives the workflow read token.
    assert not packet.exists()
    with packet.open('xb') as log:
        download=owned.run_owned(['gh','api','repos/erniecohen/dafny/actions/artifacts/11316129057/zip'],log,safe_environment(True),180)
    download['stage']='qualified-nonproof-input';receipt['stages'].append(download)
    assert download['passed'] is True
    inspect_qualification()
    stage('shared-source-head',['git','rev-parse','HEAD'],10)
    receipt['head']=(OUTPUT/'shared-source-head.txt').read_text().strip()
    dotnet_pin=file_pin(Path(shutil.which('dotnet')).resolve());receipt['dotnetExecutable']=dotnet_pin
    lifecycle,old_manifest,solver,origin=prepare_solver()
    harness=OUTPUT/'shared-harness'
    recheck_boundary('before-build',dotnet_pin,fresh_harness=harness)
    build_failed=False
    try:
        stage('shared-harness-build',[dotnet_pin['path'],'build',str(SOURCES/'B3AlcGate.csproj'),'-c','Release','-m:1',
          '-p:UseSharedCompilation=false','-p:StartupObject=B3AlcGate.NativeProofSmokeProgram',
          '--output',str(harness),'--nologo'],600)
    except BaseException:build_failed=True;raise
    finally:post_build_inputs(dotnet_pin,harness/'source-manifest.json',None,build_failed)
    bundle={name:file_pin(harness/name) for name in ['B3AlcGate.dll','B3AlcGate.deps.json','B3AlcGate.runtimeconfig.json','source-manifest.json']}
    assert bundle['B3AlcGate.dll']['sha256']!=qualified['harnessAssemblySha256']
    receipt['harnessBundlePins']=bundle
    recheck_boundary('after-build',dotnet_pin,bundle)
    source_pins={f['path']:f for f in source['files']}
    inspector=module('shared_native_strict_smoke_inspector',SOURCES/'shared-native-smoke-receipt-validation.py',
      source_pins['shared-native-smoke-receipt-validation.py'])
    parent=Path('/sys/fs/cgroup')/('b3-alc-ci-'+uuid.uuid4().hex)
    assert not parent.exists()
    stage('shared-private-delegation',['sudo','mkdir','-m','700',str(parent)],20)
    parent_created=True;parent_identity=(parent.stat().st_dev,parent.stat().st_ino)
    stage('shared-delegate-owner',['sudo','chown',str(os.getuid())+':'+str(os.getgid()),str(parent),
      str(parent/'cgroup.procs'),str(parent/'cgroup.threads'),str(parent/'cgroup.subtree_control'),str(parent/'cgroup.kill')],20)
    parent_fd=os.open(parent,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW)
    assert (os.fstat(parent_fd).st_dev,os.fstat(parent_fd).st_ino)==parent_identity
    kill_fd=os.open('cgroup.kill',os.O_WRONLY|os.O_NOFOLLOW,dir_fd=parent_fd);os.close(kill_fd)
    assert (parent/'cgroup.type').read_text().strip()=='domain' and (parent/'cgroup.procs').read_text().strip()==''
    assert (parent/'cgroup.controllers').read_text().split().count('pids')==1
    (parent/'cgroup.subtree_control').write_text('+pids\n')
    stage('shared-delegate-ceiling',['sudo','chown',str(os.getuid())+':'+str(os.getgid()),str(parent/'pids.max')],20)
    (parent/'pids.max').write_text('300\n')
    receipt['delegatedPidCeiling']=300;receipt['executionPrivilege']='fixed-harness-root-cgroup-migration'
    def pinned(path):return {'path':str(path.resolve()),'sha256':sha(path)}
    products=qualified_controls[0]['products']
    native_inputs={'baselineDirectory':products[0]['directory'],'candidateDirectory':products[1]['directory'],
      'baselineArchive':str((OUTPUT/'baseline-input/baseline.tar.gz').resolve()),
      'candidatePackageManifest':pinned(OUTPUT/'archived-candidate-input/out/b3-native-compile/candidate-package-manifest.json'),
      'candidateSourceCommit':CANDIDATE_SOURCE,'lifecycleReceipt':pinned(lifecycle),'lifecycleSourceManifest':pinned(old_manifest),
      'solverOriginEvidence':pinned(origin),'solver':str(solver.resolve()),'solverSha256':SOLVER_SHA,
      'cgroupParent':str(parent),'receipt':str((OUTPUT/'shared-proof-smoke.json').resolve()),
      'nonproofQualification':pinned(packet)}
    input_file=OUTPUT/'shared-proof-inputs.json'
    input_file.write_text(json.dumps(native_inputs,indent=2)+'\n');receipt['fixedInputsSha256']=sha(input_file)
    recheck_boundary('before-proof',dotnet_pin,bundle)
    host_env=safe_environment()
    assert not any(name in host_env for name in ['GH_TOKEN','GITHUB_TOKEN','LD_PRELOAD','LD_LIBRARY_PATH','LD_AUDIT'])
    command=['sudo','--',str(Path(shutil.which('env')).resolve()),'-i']+[
      key+'='+host_env[key] for key in sorted(host_env)]+[dotnet_pin['path'],bundle['B3AlcGate.dll']['path'],'--inputs',str(input_file.resolve())]
    try:stage('fixed-six-shared-native-smoke',command,600)
    finally:recheck_boundary('after-proof',dotnet_pin,bundle)
    receipt['proofStageCompleted']=True
except BaseException as error:
    receipt['failure']=type(error).__name__+': '+str(error)
finally:
    coordinator_cleanup_active=True
    # FD-anchored exclusive caller drain, independently required even on failures.
    if parent_created:
        try:
            if parent_fd is None:
                assert not parent.is_symlink() and (parent.stat().st_dev,parent.stat().st_ino)==parent_identity
                stage('shared-parent-remove',['sudo','rmdir','--',str(parent)],20)
            else:
                def read_owned(directory,name):
                    fd=os.open(name,os.O_RDONLY|os.O_NOFOLLOW,dir_fd=directory)
                    try:
                        value=os.read(fd,65537);assert len(value)<=65536;return value.decode('ascii')
                    finally:os.close(fd)
                leaves=[];requires_drain=True;diagnostic_error=None
                try:
                    leaves=[n for n in os.listdir(parent_fd) if stat.S_ISDIR(os.stat(n,dir_fd=parent_fd,follow_symlinks=False).st_mode)]
                    assert all(n.startswith('alc-') for n in leaves)
                    requires_drain=bool(leaves or read_owned(parent_fd,'cgroup.procs').split() or 'populated 1' in read_owned(parent_fd,'cgroup.events'))
                except BaseException as error:
                    diagnostic_error=type(error).__name__+': '+str(error);receipt['callerCleanupDiagnosticFailure']=diagnostic_error
                finally:
                    if requires_drain:
                        receipt['callerDrainRequired']=True;receipt.setdefault('failure','Exclusive owned parent required failure drain.')
                        try:
                            kill_fd=os.open('cgroup.kill',os.O_WRONLY|os.O_NOFOLLOW,dir_fd=parent_fd)
                            try:assert os.write(kill_fd,b'1\n')==2
                            finally:os.close(kill_fd)
                        except BaseException as error:receipt['callerKillFailure']=type(error).__name__+': '+str(error)
                        faults=[]
                        try:
                            deadline=time.monotonic()+5
                            while 'populated 1' in read_owned(parent_fd,'cgroup.events') and time.monotonic()<deadline:time.sleep(.05)
                        except BaseException as error:faults.append(type(error).__name__+': '+str(error))
                        for name in ['cgroup.procs','cgroup.events']:
                            try:
                                value=read_owned(parent_fd,name);assert (not value.split() if name=='cgroup.procs' else 'populated 1' not in value)
                            except BaseException as error:faults.append(name+': '+type(error).__name__+': '+str(error))
                        if faults:receipt['callerEmptyCheckFailures']=faults
                        assert not faults and not receipt.get('callerKillFailure')
                assert diagnostic_error is None
                assert not read_owned(parent_fd,'cgroup.procs').split() and 'populated 1' not in read_owned(parent_fd,'cgroup.events')
                for name in leaves:
                    fd=os.open(name,os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW,dir_fd=parent_fd)
                    try:assert not read_owned(fd,'cgroup.procs').split() and 'populated 1' not in read_owned(fd,'cgroup.events')
                    finally:os.close(fd)
                    os.rmdir(name,dir_fd=parent_fd)
                assert (parent.stat().st_dev,parent.stat().st_ino)==parent_identity
                stage('shared-parent-remove',['sudo','rmdir','--',str(parent)],20)
            receipt['callerDelegationRemoved']=True
        except BaseException as error:receipt['callerCleanupFailure']=type(error).__name__+': '+str(error)
        finally:
            if parent_fd is not None:os.close(parent_fd)
    try:
        evidence_dirs=[p for p in OUTPUT.iterdir() if p.name.startswith('proof-smoke-evidence-')]
        assert len(evidence_dirs)<=1 and all(p.is_dir() and not p.is_symlink() for p in evidence_dirs)
        for folder in evidence_dirs:
            stage('shared-evidence-owner',['sudo','chown','-R','--no-dereference',str(os.getuid())+':'+str(os.getgid()),str(folder.resolve())],20)
        receipt['privateEvidenceMadeReadable']=True
    except BaseException as error:receipt['evidenceOwnershipFailure']=type(error).__name__+': '+str(error)
    try:
        if dotnet_pin is not None:recheck_boundary('final',dotnet_pin,bundle)
        if owned is not None:
            receipt['remainingCoordinatorChildren']=owned.child_ids(os.getpid())
            assert receipt['remainingCoordinatorChildren']==[]
        assert receipt.get('proofStageCompleted') and receipt.get('callerDelegationRemoved') and receipt.get('privateEvidenceMadeReadable')
        assert not cancelled and not receipt.get('callerDrainRequired')
        assert not any(receipt.get(k) for k in ['failure','callerCleanupFailure','evidenceOwnershipFailure','callerKillFailure','callerCleanupDiagnosticFailure'])
        assert [c['boundary'] for c in receipt['immutableInputChecks']]==['before-build','after-build-sources','after-build','before-proof','after-proof','final']
        assert all(c['passed'] is True for c in receipt['immutableInputChecks'])
        controls=load_json(OUTPUT/'shared-proof-smoke.json')
        receipt['nativeRuns']=inspector.inspect_proofs(controls,receipt,qualified_controls[0],constants,OUTPUT)
        receipt['proofReceiptSha256']=sha(OUTPUT/'shared-proof-smoke.json');receipt['passed']=True
    except BaseException as error:
        receipt['passed']=False;receipt['inspectionFailure']=type(error).__name__+': '+str(error)
    (OUTPUT/'summary.json').write_text(json.dumps(receipt,indent=2)+'\n')
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
            summary.write('Fixed six qualified shared-native smoke: '+('PASS' if receipt['passed'] else 'NOT GREEN')+'\n\n')
            summary.write('Exact archived a613 loader/smoke scope only. Persistent Boogie state; incomplete optional metadata availability; no full default query/resource parity or final integrated candidate qualification.\n')
