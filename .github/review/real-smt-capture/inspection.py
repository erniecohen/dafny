#!/usr/bin/env python3
"""Artifact-only inspection of frozen diagnostic37244861245. Never runs a verifier."""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import signal
import stat
import sys
import types
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
INPUTS_SHA = 'cdf93dd00803a9964f988e92c25a8fabfd1779efd229737f749c60fa1c62f19d'
MAX_ARCHIVE = MAX_EXPANDED = 1024 * 1024 * 1024
MAX_FILE = 256 * 1024 * 1024
MAX_SOURCE = 1024 * 1024
MAX_RECEIPT = 4 * 1024 * 1024
MAX_WEAK_BYTES = 16 * 1024 * 1024


def require(condition, message):
    if not condition: raise ValueError(message)


def sha(data): return hashlib.sha256(data).hexdigest()


def opened_regular(path, maximum, allow_empty=False):
    require(path.parent.resolve(strict=True) == path.parent, 'Input parent traverses a symlink')
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and (0 if allow_empty else 1) <= before.st_size <= maximum, 'Bounded regular input required')
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC)
    try:
        pinned = os.fstat(fd)
        require((pinned.st_dev,pinned.st_ino) == (before.st_dev,before.st_ino) and stat.S_ISREG(pinned.st_mode), 'Input identity changed')
        return os.fdopen(fd,'rb'),pinned
    except BaseException:
        os.close(fd); raise


def metadata(info):
    return info.st_dev,info.st_ino,info.st_size,info.st_mtime_ns,info.st_ctime_ns


def read(path, maximum=MAX_RECEIPT, allow_empty=False):
    stream,pin = opened_regular(path,maximum,allow_empty)
    with stream:
        data = stream.read(maximum+1)
        require(len(data) <= maximum and len(data) == pin.st_size and metadata(os.fstat(stream.fileno())) == metadata(pin), 'Input changed or exceeded bound')
        return data


def stream_hash(stream, maximum):
    stream.seek(0); h = hashlib.sha256(); count = 0
    while block := stream.read(65536):
        count += len(block); require(count <= maximum, 'Stream byte bound'); h.update(block)
    return h.hexdigest(),count


def bootstrap():
    source = read(HERE/'coordinator.py',MAX_SOURCE)
    declared = json.loads(read(HERE/'source-manifest.json',65536))['files']['coordinator.py']
    require(len(source) == declared['bytes'] and sha(source) == declared['sha256'], 'Coordinator source differs from seal')
    module = types.ModuleType('b3_artifact_inspection_source'); module.__file__ = str(HERE/'coordinator.py')
    exec(compile(source,module.__file__,'exec'),module.__dict__)
    hashes = module.seal(); require(hashes['coordinator.py'] == sha(source), 'Captured coordinator changed')
    return module,hashes


def inputs():
    raw = read(HERE/'inspection-inputs.json',65536); require(sha(raw) == INPUTS_SHA, 'Inspection input pins differ')
    value = json.loads(raw)
    require(value['schemaVersion'] == 1 and value['diagnosticOnly'] is True and len(value['cases']) == 8, 'Inspection denominator differs')
    inventory_raw = read(HERE/'inspection-artifact-inventory.json',MAX_SOURCE)
    require(sha(inventory_raw) == value['inventorySha256'], 'Exported artifact inventory pin differs')
    inventory = json.loads(inventory_raw)
    historical = read(HERE/'historical-source-manifest.json',65536)
    require(sha(historical) == value['historicalSourceSealSha256'], 'Historical source pin differs')
    return value,inventory,json.loads(historical)


def preflight(entries, expected, inventory):
    # All headers/paths/types/modes/bounds are checked before extraction output.
    layout = expected['archiveMembers']; require(0 < len(entries) <= 20000 and len(entries) == sum(layout.values()), 'Artifact member count differs')
    seen = set(); observed = {root:0 for root in layout}; rows = []; total = 0
    for entry in entries:
        name = entry.filename; path = PurePosixPath(name)
        require(0 < len(name.encode()) <= 4096 and len(path.parts) <= 64 and not path.is_absolute() and '\\' not in name and '\x00' not in name and
          all(p not in ('','.','..') for p in name.rstrip('/').split('/')) and path.as_posix() == name.rstrip('/'), 'Artifact path is noncanonical')
        canonical = path.as_posix(); require(canonical not in seen, 'Duplicate canonical artifact member'); seen.add(canonical)
        root = '/'.join(path.parts[:2]); require(root in layout and len(path.parts) > 2, 'Unknown artifact root')
        observed[root] += 1
        require(not entry.is_dir() and not entry.flag_bits & 1 and entry.compress_type in (0,8), 'Unsupported artifact entry')
        mode = entry.external_attr >> 16
        require(stat.S_ISREG(mode) and stat.S_IMODE(mode) in (0o600,0o644,0o755) and 0 <= entry.file_size <= MAX_FILE, 'Artifact type/mode/size bound')
        total += entry.file_size; require(total <= MAX_EXPANDED, 'Artifact aggregate bound')
        require(canonical in inventory, 'Unexpected exported artifact member')
        pin = inventory[canonical]
        require(entry.file_size == pin['bytes'] and oct(mode) == pin['archiveModeOctal'] and re.fullmatch('[0-9a-f]{64}',pin['sha256']) is not None, 'Exported member header differs')
        rows.append((entry,path))
    require(observed == layout and seen == set(inventory), 'Artifact exported inventory differs')
    for name in seen:
        parent = PurePosixPath(name).parent
        while parent != PurePosixPath('.'):
            require(parent.as_posix() not in seen, 'Artifact file is an ancestor'); parent = parent.parent
    return rows


def extract(archive, destination, expected, inventory):
    require(not destination.exists(), 'Artifact destination must be fresh')
    stream,pin = opened_regular(archive,MAX_ARCHIVE)
    with stream:
        digest,size = stream_hash(stream,MAX_ARCHIVE)
        require(size == expected['bytes'] and digest == expected['sha256'] and metadata(os.fstat(stream.fileno())) == metadata(pin), 'Exact artifact bytes differ')
        stream.seek(0)
        with zipfile.ZipFile(stream) as zipped:
            rows = preflight(zipped.infolist(),expected,inventory)
            destination.mkdir(); observed = {}
            for entry,path in rows:
                target = destination.joinpath(*path.parts); target.parent.mkdir(parents=True,exist_ok=True)
                h = hashlib.sha256(); count = 0
                with zipped.open(entry) as source,target.open('xb') as output:
                    while block := source.read(65536):
                        count += len(block); require(count <= entry.file_size, 'Artifact member exceeded bound')
                        h.update(block); output.write(block)
                item = {'bytes':count,'sha256':h.hexdigest(),'archiveModeOctal':oct(entry.external_attr >> 16)}
                require(item == inventory[path.as_posix()], 'Actual exported member bytes differ')
                observed[path.as_posix()] = item
        digest,size = stream_hash(stream,MAX_ARCHIVE)
        require(size == expected['bytes'] and digest == expected['sha256'] and metadata(os.fstat(stream.fileno())) == metadata(pin), 'Artifact changed during extraction')
    return observed


def validate_request(raw, expected):
    require(len(raw) == expected['requestBytes'] and sha(raw) == expected['requestSha256'], 'Frozen case request bytes differ')
    request = json.loads(raw)
    require((request['requestId'],request['unitId'],request['programHash']) ==
      (expected['requestId'],expected['unitId'],expected['programHash']), 'Frozen request identity differs')
    require([x['id'] for x in request['obligations']] == expected['checkIds'], 'Static original goals differ')
    return request


def postprocess_attempts(replay, expected):
    # Separate new evidence. Never mutate or overwrite the historical host fields.
    completion = replay['completion']; attempts = completion['attempts']
    require(0 < len(attempts) <= 128, 'Attempt evidence bound')
    actual = []
    for item in attempts:
        crumbs = item['breadcrumbs']
        require(type(item['sequence']) is int and 0 <= item['sequence'] < 128 and isinstance(item['obligationId'],str) and
          isinstance(crumbs,list) and len(crumbs) <= 64 and all(isinstance(x,str) and len(x) <= 512 for x in crumbs), 'Malformed attempt identity')
        actual.append({key:item[key] for key in ['sequence','obligationId','breadcrumbs']})
    matched = actual == expected['attemptSchedule']
    mathematical = matched and completion['traversalCompleted'] is True and completion['error'] is None and completion['outcome'] == expected['expected'] and (
      all(x['outcome'] == 'Verified' for x in attempts) if expected['expected'] == 'Verified' else any(x['outcome'] == 'Failed' for x in attempts))
    return {'historicalHostFields':{key:replay[key] for key in ['exactCheckCoverage','mathematicalMatched']},
      'reviewedAttemptScheduleMatched':matched,'mathematicalOutcomeAndScheduleMatched':mathematical,
      'expectedAttemptSchedule':expected['attemptSchedule'],'observedAttemptSchedule':actual}


def inspect_tree(tree, output, frozen, inventory, historical, capture):
    def member(path, maximum=MAX_RECEIPT):
        require(path in inventory, 'Inspection member missing from pinned inventory')
        raw = read(tree/Path(path),maximum,allow_empty=True); pin = inventory[path]
        require(len(raw) == pin['bytes'] and sha(raw) == pin['sha256'], 'Inspection member changed after extraction')
        return raw
    prefix = 'out/b3-native-compile/'; outer = json.loads(member(prefix+'summary.json'))
    inner = json.loads(member(prefix+'real-smt-capture/summary.json'))
    receipt = json.loads(member(prefix+'real-smt-capture/capture/summary.json'))
    for key,path in [('outer',prefix+'summary.json'),('inner',prefix+'real-smt-capture/summary.json'),('capture',prefix+'real-smt-capture/capture/summary.json')]:
        require(sha(member(path)) == frozen['receiptHashes'][key], 'Historical receipt hash differs')
    require(outer['approvedSourceManifest'] == historical and outer['sourceSealSha256'] == frozen['historicalSourceSealSha256'] and
      outer['routingSourceSha256'] == frozen['historicalOuterSha256'] and outer['workflowSourceSha256'] == frozen['historicalWorkflowSha256'] and
      outer['declaredCIHead'] == frozen['artifact']['head'], 'Historical source association differs')
    require(inner['sourceSealSha256'] == receipt['sourceSealSha256'] == frozen['historicalSourceSealSha256'] and
      inner['diagnosticOnly'] is True and receipt['diagnosticOnly'] is True and inner['acceptanceClaimed'] is False and receipt['acceptanceClaimed'] is False, 'Historical diagnostic source boundary differs')
    require(outer['diagnosticReceiptSha256'] == frozen['receiptHashes']['inner'] and inner['diagnosticReceiptSha256'] == frozen['receiptHashes']['capture'], 'Historical receipt chain differs')
    require(receipt['inheritedFileSizeLimitBefore'] == receipt['inheritedFileSizeLimitAfter'] == [-1,-1] and receipt['remainingDirectChildren'] == [], 'Historical lifecycle boundary differs')
    require(len(inner['stages']) == 8 and len(receipt['stages']) == 12 and len(receipt['cases']) == 8, 'Historical stage denominator differs')
    for stage in inner['stages']+receipt['stages']:
        require(stage['passed'] is True and stage['signalCount'] == 0 and stage['remainingDirectChildren'] == [] and
          all(x['exitObserved'] and x['reaped'] for x in stage['ownedProcesses']), 'Historical owned stage differs')
    old_cases = {x['name']:x for x in receipt['cases']}; require(len(old_cases) == 8, 'Historical duplicate case')
    package_path = prefix+'real-smt-capture/capture/package/'
    package_raw = member(package_path+'b3-worker-manifest.json',65536)
    require(sha(package_raw) == frozen['workerFingerprint'], 'Historical worker manifest differs')
    package = json.loads(package_raw)
    require(package['version'] == 2 and package['b3Commit'] == frozen['b3Commit'] and set(package['files']) ==
      {'B3Library.dll','DafnyB3Host.dll','DafnyB3Host.deps.json','DafnyB3Host.runtimeconfig.json','DafnyB3Protocol.dll'}, 'Historical fixed worker inventory differs')
    for name,digest in package['files'].items(): require(sha(member(package_path+name,MAX_FILE)) == digest, 'Historical worker bytes differ')
    solver_path = prefix+'real-smt-capture/prerequisite/out/b3-native-compile/inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
    require(sha(member(solver_path,MAX_FILE)) == frozen['solverSha256'], 'Historical solver bytes differ')
    rows = []; weak_bytes = 0
    for expected in frozen['cases']:
        name = expected['name']; old = old_cases[name]; replay = json.loads(member(expected['replayPath']))
        require(old['expected'] == expected['expected'] and old['exploratory'] is expected['exploratory'], 'Historical fixture classification differs')
        request_raw = member(expected['requestPath'],MAX_SOURCE); request = validate_request(request_raw,expected)
        require(replay == old['replay'] and replay['requestSha256'] == expected['requestSha256'] and request['workerFingerprint'] == frozen['workerFingerprint'], 'Historical replay association differs')
        completion = replay['completion']
        require((completion['requestId'],completion['unitId'],completion['programHash'],completion['b3Commit'],completion['workerFingerprint']) ==
          (expected['requestId'],expected['unitId'],expected['programHash'],frozen['b3Commit'],frozen['workerFingerprint']), 'Historical completion identity differs')
        original_equal = None
        if expected['originalCompletionPath'] is not None:
            original_equal = completion == json.loads(member(expected['originalCompletionPath']))
            require(original_equal, 'Current original completion differs from frozen372321')
        raw = member(expected['tracePath'],capture.MAX_TRACE); sink = old['traceSink']
        require(len(raw) == sink['prefixBytes'] == sink['observedBytes'] and sha(raw) == sink['prefixSha256'] == sink['completeTraceSha256'] == old['traceSha256'] and
          sink['complete'] and sink['eofObservedAfterOwnedExitAndReaping'] and not sink['overflowObserved'] and sink['failure'] is None, 'Historical complete sink bytes differ')
        row = {'name':name,'exploratory':expected['exploratory'],'historicalReplaySha256':sha(member(expected['replayPath'])),
          'historicalFailure':old.get('failure'),'originalCompletionExactlyEqual':original_equal,'captureComplete':False,
          'strongerPrefixQualificationClaimed':False,'actualProofResourceCount':None,'proofResourceStatus':'unavailable',
          **postprocess_attempts(replay,expected)}
        try:
            calls,pending = capture.calls_from_trace(raw)
            row['nonResultObservations'] = [dict(x) for x in calls if x.get('nonResult')]
            row['remainingUnfinishedObservationCount'] = len(pending)
            signal_calls = [x for x in calls if x['name'] in ('kill','tkill','tgkill')]
            require(len(signal_calls) <= 1024, 'Trace signal-observation bound')
            row['traceReportedSignalCalls'] = [{key:x[key] for key in ['tid','name','args','result','begin','end']} for x in signal_calls]
            row['traceSignalCallsOwnershipQualified'] = False
            stage = next(x for x in receipt['stages'] if x['stage'] == name)
            command = stage['command']; argv = command[command.index('--')+1:]
            try:
                require(row['reviewedAttemptScheduleMatched'], 'Physical query attempt schedule differs')
                result = capture.analyze(raw,stage,old['liveImages'],old['weakerPathAndImmutableFileObservation']['solverPath'],frozen['solverSha256'],
                  [x['obligationId'] for x in expected['attemptSchedule']],str(argv[argv.index('--worker')+1]),str(argv[0]),request_raw,completion,argv)
                # Full qualification only; unresolved selected I/O still rejects.
                row['captureComplete'] = result['captureComplete']
                row['physicalQueryBindings'] = [{**query,**expected['attemptSchedule'][index]}
                  for index,query in enumerate(result['emittedCheckQueries'])]
                row['unqueriedToolErrorAttempts'] = expected['attemptSchedule'][len(result['emittedCheckQueries']):]
            except ValueError as error: row['fullCaptureFailure'] = str(error)
            weak = capture.weak_observations(raw,old['weakerPathAndImmutableFileObservation']['solverPath'])
            for index,observation in enumerate(weak):
                for key,suffix in [('commandBytes','stdin'),('responseBytes','stdout')]:
                    data = observation.pop(key); weak_bytes += len(data); require(weak_bytes <= MAX_WEAK_BYTES,'Weak observation output aggregate bound')
                    path = output/(name+'.unqualified-'+str(index)+'-'+suffix+'.bytes'); path.write_bytes(data)
                    observation[key+'Sha256'] = sha(data); observation[key+'Length'] = len(data)
            row['unqualifiedObservations'] = weak
        except ValueError as error: row['inspectionFailure'] = str(error)
        rows.append(row)
    return {'diagnosticOnly':True,'acceptanceClaimed':False,'workerExecuted':False,'solverExecuted':False,'requestsSubmitted':0,
      'historicalReceiptHashes':frozen['receiptHashes'],
      'historicalOperationalSupervisorSignalCount':sum(x['signalCount'] for x in inner['stages']+receipt['stages']),
      'traceeSignalObservationsAreSupervisorSignals':False,'inspectionDenominatorComplete':len(rows)==8,'allCapturesComplete':all(x['captureComplete'] for x in rows),
      'mathematicalExpectationsWaived':False,'strongerPrefixQualificationClaimed':False,
      'allReviewedAttemptSchedulesMatched':all(x['reviewedAttemptScheduleMatched'] for x in rows),
      'allMathematicalOutcomesAndSchedulesMatched':all(x['mathematicalOutcomeAndScheduleMatched'] for x in rows),'cases':rows}


def main():
    require(len(sys.argv) == 2, 'Usage: inspection.py NEW_OUTPUT_DIRECTORY')
    output = Path(sys.argv[1]).resolve(); output.mkdir(parents=True,exist_ok=False)
    report = {'diagnosticOnly':True,'acceptanceClaimed':False,'workerExecuted':False,'solverExecuted':False,'requestsSubmitted':0,
      'SDKInvoked':False,'ReplayInvoked':False,'libraryRebuilt':False,'compilerProductRebuilt':False,'stages':[],'receiptProduced':False,'cleanupPoisoned':False}
    inner = owned = None
    try:
        require(sys.platform == 'linux' and platform.machine() == 'x86_64' and os.geteuid() != 0 and platform.python_implementation() == 'CPython' and sys.version_info[:2] == (3,12), 'Require non-root Linux x64 CPython3.12 inspection')
        inner,hashes = bootstrap(); frozen,inventory,historical = inputs()
        report['sourceSealSha256'] = sha(read(HERE/'source-manifest.json',65536)); report['historicalSourceSealSha256'] = frozen['historicalSourceSealSha256']
        report['pythonVersion'] = sys.version; python_path = Path(sys.executable).resolve(strict=True)
        report['pythonExecutableSha256'] = sha(read(python_path,MAX_FILE)); report['pythonExecutable'] = str(python_path)
        owned = inner.load_source('owned-process.py',hashes['owned-process.py']); capture = inner.load_source('capture.py',hashes['capture.py'])
        report['ownership'] = owned.enable_subreaper()
        def interrupted(signum,frame):
            if not owned.record_cancellation('Artifact inspection cancellation '+str(signum)): raise InterruptedError('Inspection interrupted')
        signal.signal(signal.SIGTERM,interrupted); signal.signal(signal.SIGINT,interrupted)
        env = inner.filtered_environment(); gh = Path(shutil.which('gh') or '').resolve(strict=True)
        report['ghExecutableSha256'] = sha(read(gh,MAX_FILE)); download_env = dict(env)
        for key in ['GH_TOKEN','GITHUB_TOKEN']:
            if key in os.environ: download_env[key] = os.environ[key]
        report['downloadEnvironmentKeys'] = sorted(download_env); report['nonDownloadEnvironmentKeys'] = sorted(env)
        def stage(name,command,path,maximum,timeout,environment=env):
            def bounds(): require(path.stat().st_size <= maximum,'Inspection stage output bound')
            with path.open('xb') as stream: row = owned.run_owned([str(x) for x in command],stream,environment,timeout,check_bounds=bounds)
            row['stage'] = name; report['stages'].append(row)
            if not row['passed']: report['cleanupPoisoned'] = True
            stream,pin = opened_regular(path,maximum,allow_empty=True)
            with stream:
                row['outputSha256'],count = stream_hash(stream,maximum)
                require(count == pin.st_size and metadata(os.fstat(stream.fileno())) == metadata(pin),'Stage output changed during hash')
            require(row['passed'],'Owned artifact inspection stage failed: '+name)
        public = frozen['artifact']; prefix = 'repos/erniecohen/dafny/actions/'
        run_file = output/'run.json'; stage('run-metadata',[gh,'api',prefix+'runs/'+str(public['run'])],run_file,262144,60,download_env)
        run = json.loads(read(run_file)); require((run['id'],run['head_sha'],run['head_branch'],run['event'],run['path'],run['status'],run['conclusion'],run['run_attempt']) ==
          (public['run'],public['head'],public['branch'],'workflow_dispatch','.github/workflows/review.yml','completed','success',1),'Frozen artifact run metadata differs')
        meta_file = output/'artifacts.json'; stage('artifact-metadata',[gh,'api',prefix+'runs/'+str(public['run'])+'/artifacts'],meta_file,262144,60,download_env)
        listing = json.loads(read(meta_file)); artifacts = listing['artifacts']
        require(listing['total_count'] == 1 and len(artifacts) == 1,'Frozen artifact denominator differs'); artifact = artifacts[0]
        require((artifact['id'],artifact['name'],artifact['size_in_bytes'],artifact['digest'],artifact['expired']) ==
          (public['artifact'],'b3-native-compile',public['bytes'],'sha256:'+public['sha256'],False) and
          artifact['workflow_run']['id'] == public['run'] and artifact['workflow_run']['head_sha'] == public['head'],'Frozen artifact metadata differs')
        archive = output/'artifact.zip'; stage('artifact-download',[gh,'api',prefix+'artifacts/'+str(public['artifact'])+'/zip'],archive,MAX_ARCHIVE,120,download_env)
        require(inner.seal() == hashes,'Inspection source changed before extraction')
        tree = output/'exported'; observed = extract(archive,tree,public,inventory)
        report['artifact'] = public; report['exportedInventorySha256'] = frozen['inventorySha256']; report['exportedFiles'] = len(observed)
        parser_log = output/'parser-controls.log'; stage('parser-controls',[sys.executable,'-B','-m','unittest','discover','-s',HERE,'-p','test_capture.py'],parser_log,MAX_RECEIPT,30)
        text = read(parser_log).decode(); require('Ran 45 tests' in text and text.rstrip().endswith('OK'),'Exact45 control denominator differs')
        report['parserControlsPassed'] = 45; report['controlsIncludeIntentionalOwnedNonverifierTimeout'] = True
        require(inner.seal() == hashes,'Inspection source changed after controls')
        result = inspect_tree(tree,output,frozen,inventory,historical,capture)
        raw = (json.dumps(result,indent=2)+'\n').encode(); require(len(raw) <= MAX_RECEIPT,'Postprocessing receipt byte bound')
        (output/'inspection.json').write_bytes(raw); report['inspectionReceiptSha256'] = sha(raw); report['receiptProduced'] = True
        require(inner.seal() == hashes,'Inspection source changed after postprocessing')
        for name,pin in inventory.items():
            stream,identity = opened_regular(tree/Path(name),MAX_FILE,allow_empty=True)
            with stream:
                digest,count = stream_hash(stream,MAX_FILE)
                require(digest == pin['sha256'] and count == pin['bytes'] and metadata(os.fstat(stream.fileno())) == metadata(identity), 'Exported artifact changed after inspection')
        require(sha(read(python_path,MAX_FILE)) == report['pythonExecutableSha256'] and
          sha(read(gh,MAX_FILE)) == report['ghExecutableSha256'],'Inspection executable changed after stages')
        report['exportedInventoryRechecked'] = True; report['inspectionCompleted'] = True
    except Exception as error: report['failure'] = type(error).__name__+': '+str(error)
    finally:
        if owned is not None:
            try:
                report['remainingDirectChildren'] = owned.child_ids(os.getpid())
                if report['remainingDirectChildren']:
                    report['cleanupPoisoned'] = True
                    report['cleanupFailure'] = 'Inspection scope not empty; mandatory poisoned drain'
                    report['emergencyDrain'] = owned.drain_exclusive_children(10); report['remainingDirectChildren'] = owned.child_ids(os.getpid())
            except Exception as error:
                report['cleanupPoisoned'] = True; report['cleanupFailure'] = type(error).__name__+': '+str(error)
        raw = (json.dumps(report,indent=2)+'\n').encode()
        if len(raw) > MAX_RECEIPT:
            report = {'diagnosticOnly':True,'acceptanceClaimed':False,'receiptProduced':False,'cleanupPoisoned':True,
              'workerExecuted':False,'solverExecuted':False,'requestsSubmitted':0,'SDKInvoked':False,'ReplayInvoked':False,
              'libraryRebuilt':False,'compilerProductRebuilt':False,'failure':'Outer inspection receipt exceeded bounded output',
              'remainingDirectChildren':report.get('remainingDirectChildren',[]),'priorFailure':str(report.get('failure',''))[:4096]}
            raw = (json.dumps(report,indent=2)+'\n').encode()
        (output/'summary.json').write_bytes(raw); print('Real artifact inspection receipt:',report['receiptProduced'],'acceptance False',flush=True)
    return 0


if __name__ == '__main__': raise SystemExit(main())
