#!/usr/bin/env python3
"""Source-sealed Real SMT diagnostic: exact four replays plus four strict exploratory controls.

No workflow/download/build of verified library is performed here. The caller
supplies immutable public archives extracted in a fresh dedicated Linux job.
A zero exit only produces diagnostic evidence, including explicit incompleteness.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import resource
import shutil
import signal
import stat
import sys
import types

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
MAX_FILE = 256 * 1024 * 1024
MAX_TRACE = 64 * 1024 * 1024
MAX_LOG = 4 * 1024 * 1024
INPUTS_SHA = '7b08e4b29bf8f5b6d5756a8311bfb3bd4ecc2a5b3a1275c22a056af2a6a41511'
INNER_SEAL = '179b10f8bd33025dd62be724916b199ee8b62961b877a4350dcd8e29f2024864'
SOURCE_SHA = 'a6ecb742096ddf2f4a6dbcfefb847fdb17ff1fbda7bacf0fafd445f60d843976'
SOLVER_PATH = '/home/runner/work/dafny/dafny/out/b3-native-compile/public-prerequisite/out/b3-native-compile/inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'


def require(condition, message):
    if not condition: raise ValueError(message)


def read(path, maximum=MAX_FILE, allow_empty=False):
    info = path.lstat(); require(stat.S_ISREG(info.st_mode) and (0 if allow_empty else 1) <= info.st_size <= maximum, 'Expected bounded regular file: ' + path.name)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    try:
        pinned = os.fstat(fd); require((pinned.st_dev,pinned.st_ino) == (info.st_dev,info.st_ino), 'Input changed at open')
        chunks = []; size = 0
        while True:
            block = os.read(fd, min(65536,maximum + 1 - size))
            if not block: break
            chunks.append(block); size += len(block); require(size <= maximum, 'Read exceeded bound')
        after = os.fstat(fd)
        require(size == pinned.st_size and (after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns,after.st_ctime_ns) ==
          (pinned.st_dev,pinned.st_ino,pinned.st_size,pinned.st_mtime_ns,pinned.st_ctime_ns), 'Input changed while reading')
        return b''.join(chunks)
    finally: os.close(fd)


def sha(data): return hashlib.sha256(data).hexdigest()


def load_source(name, digest):
    path = HERE / name; captured = read(path, 1024 * 1024)
    require(sha(captured) == digest, 'Captured module source differs from source seal')
    module = types.ModuleType('b3_capture_' + name.replace('.', '_'))
    module.__file__ = str(path)
    exec(compile(captured, str(path), 'exec'), module.__dict__)
    return module


def seal():
    manifest = json.loads(read(HERE / 'source-manifest.json', 65536))
    require(manifest['schemaVersion'] == 1 and manifest['diagnosticOnly'] is True, 'Source seal schema')
    expected = {'Program.cs','Replay.csproj','coordinator.py','capture.py','owned-process.py','inputs.json','DESIGN.md','README.md','test_capture.py','gate.py','.gitattributes'}
    expected |= {'baseline/' + x + '-' + y for x in ['real-conversion','real-irrational'] for y in ['unit-0.request.json','unit-0.program.json']}
    expected |= {'baseline/real-universal-unit-' + str(i) + '.' + suffix for i in [0,1] for suffix in ['request.json','program.json']}
    require(set(manifest['files']) == expected, 'Source-sealed inventory differs')
    captured = {}
    for name, entry in manifest['files'].items():
        require(Path(name).is_absolute() is False and '..' not in Path(name).parts, 'Invalid sealed path')
        data = read(HERE / name, 1024 * 1024)
        require(len(data) == entry['bytes'] and sha(data) == entry['sha256'], 'Source seal changed: ' + name)
        captured[name] = entry['sha256']
    return captured


def filtered_environment():
    allowed = {'PATH','HOME','DOTNET_ROOT','TMPDIR','LANG','LC_ALL','TZ','CI'}
    result = {key:value for key,value in os.environ.items() if key in allowed}
    result.update({'UseSharedCompilation':'false','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_NOLOGO':'1'})
    return result


def live_images(owned, expected, result):
    def observe(entry):
        if owned.exited(entry['pidfd']): return
        pin = entry['identity']; pid = pin['pid']
        try:
            before = owned.identity(pid); require(before['startTime'] == pin['startTime'], 'Image PID changed')
            with open('/proc/' + str(pid) + '/cmdline','rb',buffering=0) as stream: argv_bytes = stream.read(65537)
            require(len(argv_bytes) <= 65536, 'Exec argv bound')
            argv = [x.decode('utf-8',errors='strict') for x in argv_bytes.split(b'\0') if x]
            if argv != [SOLVER_PATH,'-in','-smt2']: return
            path = '/proc/' + str(pid) + '/exe'
            fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK)
            try:
                info = os.fstat(fd); require(stat.S_ISREG(info.st_mode) and 0 < info.st_size <= MAX_FILE, 'Solver image bound')
                key = (pid,pin['startTime'],info.st_dev,info.st_ino)
                if any(tuple(x['imageKey']) == key for x in result): return
                h = hashlib.sha256(); count = 0
                while True:
                    data = os.read(fd,65536)
                    if not data: break
                    count += len(data); require(count <= MAX_FILE, 'Solver image read bound'); h.update(data)
                after_image = os.fstat(fd); current_image = os.stat(path)
                after = owned.identity(pid)
                if owned.exited(entry['pidfd']) or before != after: return
                require(count == info.st_size and
                  (info.st_dev,info.st_ino,info.st_size,info.st_mtime_ns,info.st_ctime_ns) ==
                  (after_image.st_dev,after_image.st_ino,after_image.st_size,after_image.st_mtime_ns,after_image.st_ctime_ns) and
                  (current_image.st_dev,current_image.st_ino) == (info.st_dev,info.st_ino), 'Live image binding changed')
                require(h.hexdigest() == expected, 'Interactive solver image digest differs')
                require(len(result) < 16, 'Live image evidence bound')
                result.append({'identity':dict(pin),'argv':argv,'imageKey':list(key),'sha256':h.hexdigest(),
                               'bytes':count,'kernelImageObservedWhilePidfdLive':True})
            finally: os.close(fd)
        except (FileNotFoundError,ProcessLookupError):
            # A missed fast image remains unqualified. No later same-PID observation substitutes.
            return
    return observe


def stage(owned, rows, name, command, output, env, timeout, observe=None, trace=None):
    log_path = output / (name + '.log')
    def bounds():
        require(log_path.stat().st_size <= MAX_LOG, 'Stage log bound')
        if trace is not None and trace.exists(): require(trace.stat().st_size <= MAX_TRACE, 'Trace file bound')
    with log_path.open('xb') as log:
        receipt = owned.run_owned([str(x) for x in command],log,env,timeout,observe=observe,check_bounds=bounds)
    receipt.update({'stage':name,'logSha256':sha(read(log_path,MAX_LOG)) if log_path.stat().st_size else None})
    rows.append(receipt)
    require(receipt['passed'], 'Owned stage failed: ' + name)
    return receipt


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--captured-real-artifacts',required=True,type=Path)
    parser.add_argument('--prerequisite',required=True,type=Path)
    parser.add_argument('--dotnet',required=True,type=Path)
    parser.add_argument('--strace',required=True,type=Path)
    parser.add_argument('--output',required=True,type=Path)
    args = parser.parse_args()
    require(all(x.is_absolute() for x in [args.captured_real_artifacts,args.prerequisite,args.dotnet,args.strace,args.output]), 'All diagnostic paths must be absolute')
    require(not args.output.exists(), 'Diagnostic output must be fresh')
    args.output.mkdir(parents=True)
    receipt = {'diagnosticOnly':True,'acceptanceClaimed':False,'instrumentedAcceptanceClaimed':False,
      'stages':[],'cases':[],'mathematicalExpectationsWaived':False,'libraryRebuilt':False,'solverFlagsChanged':False}
    owned = None
    try:
        hashes = seal(); manifest = json.loads(read(HERE/'inputs.json',65536))
        require(hashes['inputs.json'] == INPUTS_SHA, 'Frozen input manifest changed')
        receipt['sourceSealSha256'] = sha(read(HERE/'source-manifest.json',65536))
        owned = load_source('owned-process.py',hashes['owned-process.py'])
        capture = load_source('capture.py',hashes['capture.py'])
        receipt['ownership'] = owned.enable_subreaper()
        def interrupted(signum, frame):
            if not owned.record_cancellation('Coordinator cancellation signal ' + str(signum)): raise InterruptedError('Coordinator interrupted')
        signal.signal(signal.SIGINT,interrupted); signal.signal(signal.SIGTERM,interrupted)
        # Reuse only the reviewed source-pinned prerequisite validator, not a bytecode cache.
        triage_manifest_path = ROOT/'.github/review/real-triage/source-manifest.json'
        require(sha(read(triage_manifest_path,65536)) == INNER_SEAL, 'Original diagnostic source seal changed')
        triage_seal = json.loads(read(triage_manifest_path,65536))
        for name, entry in triage_seal['files'].items():
            data = read(ROOT/name,1024*1024)
            require(len(data) == entry['bytes'] and sha(data) == entry['sha256'], 'Original diagnostic payload changed')
        triage_data = read(ROOT/'.github/review/b3-real-triage.py',1024*1024)
        inner = types.ModuleType('b3_capture_prerequisite'); inner.__file__ = str(ROOT/'.github/review/b3-real-triage.py')
        exec(compile(triage_data,inner.__file__,'exec'),inner.__dict__)
        # Source inventory/patch/three consumers and exact560 passed bootstrap receipts are checked here.
        require(sha(read(args.captured_real_artifacts/'summary.json',4*1024*1024)) == manifest['innerReceiptSha256'], 'Captured Real summary differs')
        original_summary = json.loads(read(args.captured_real_artifacts/'summary.json',4*1024*1024))
        require(original_summary['compilerVersion'] == '4.11.0+' + manifest['publicHead'] and
          original_summary['cleanupPoisoned'] is False and original_summary['fullGateAcceptanceClaimed'] is False,
          'Original diagnostic identity/boundary differs')
        receipt['originalSummarySha256'] = manifest['innerReceiptSha256']
        for case in manifest['baseline']:
            data = read(HERE/case['request'],1024*1024)
            require(len(data) == case['requestBytes'] and sha(data) == case['requestSha256'], 'Baseline request seal differs')
            q = json.loads(data)
            require(q['configuration']['solverExecutable'] == SOLVER_PATH and q['configuration']['resourceLimit'] == 200000 and
              q['configuration']['timeoutMilliseconds'] == 20000 and q['configuration']['solverArguments'] == ['-in','-smt2'], 'Exact baseline configuration differs')
            source_case,unit = case['name'].rsplit('-unit-',1)
            require(q['programHash'] == case['programHash'] and q['unitId'] == case['unitId'] and
              q['requestId'] == case['requestId'] and [x['id'] for x in q['obligations']] == case['checkIds'], 'Baseline identity/goal ledger differs')
            captured_path = args.captured_real_artifacts/'original-requests'/source_case/('unit-'+unit+'.rlimit-200000.request.json')
            require(read(captured_path,1024*1024) == data, 'Supplied public capture does not contain exact baseline bytes')
        source_solver = args.prerequisite/'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
        solver_bytes = read(source_solver); require(sha(solver_bytes) == manifest['solverSha256'], 'Solver bytes differ')
        receipt['prerequisite'] = inner.prerequisite(args.prerequisite,source_solver)
        solver = Path(SOLVER_PATH)
        if solver.exists(): require(read(solver) == solver_bytes, 'Absolute frozen solver staging path already differs')
        else:
            solver.parent.mkdir(parents=True,exist_ok=True)
            require(solver.parent.resolve() == solver.parent, 'Solver staging directory traverses a symlink')
            with solver.open('xb') as stream: stream.write(solver_bytes)
            solver.chmod(0o755)
        require(os.access(solver,os.X_OK) and sha(read(solver)) == manifest['solverSha256'], 'Exact solver staging failed')
        package = args.prerequisite/'worker/package'
        package_manifest_bytes = read(package/'b3-worker-manifest.json',65536)
        require(sha(package_manifest_bytes) == manifest['workerFingerprint'], 'Worker manifest fingerprint differs')
        package_manifest = json.loads(package_manifest_bytes)
        require(set(package_manifest['files']) == {'B3Library.dll','DafnyB3Host.dll','DafnyB3Host.deps.json',
          'DafnyB3Host.runtimeconfig.json','DafnyB3Protocol.dll'}, 'Exact five-file worker package inventory differs')
        require(sum((package/name).lstat().st_size for name in package_manifest['files']) <= 512*1024*1024, 'Package aggregate bound')
        package_hashes = {name:sha(read(package/name)) for name in package_manifest['files']}
        require(package_hashes == package_manifest['files'], 'Worker package file hash differs')
        # Copy exact package bytes once into fresh output, preserving filenames and dependency identity.
        worker = args.output/'package'; worker.mkdir()
        for name in ['b3-worker-manifest.json',*package_hashes]:
            require(Path(name).name == name, 'Worker manifest path is not flat')
            with (worker/name).open('xb') as stream: stream.write(read(package/name))
        protocol_source = args.captured_real_artifacts/'runner/DafnyB3Protocol.dll'
        protocol_bytes = read(protocol_source,32*1024*1024)
        require(sha(protocol_bytes) == manifest['protocolAssemblySha256'], 'Captured protocol assembly differs')
        protocol = args.output/'DafnyB3Protocol.dll'; protocol.write_bytes(protocol_bytes)
        dotnet=args.dotnet.resolve(strict=True); tracer=args.strace.resolve(strict=True)
        receipt['dotnetExecutableSha256']=sha(read(dotnet)); receipt['straceExecutableSha256']=sha(read(tracer))
        env=filtered_environment(); receipt['environmentKeys']=sorted(env)
        receipt['environmentOverrides']={key:env[key] for key in ['UseSharedCompilation','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_NOLOGO']}
        stage(owned,receipt['stages'],'sdk-version',[dotnet,'--version'],args.output,env,30)
        require((args.output/'sdk-version.log').read_text().strip().startswith('8.'), 'Require .NET8 SDK')
        stage(owned,receipt['stages'],'strace-version',[tracer,'--version'],args.output,env,30)
        require('version 6.8' in (args.output/'strace-version.log').read_text(), 'Parser supports reviewed strace6.8 only')
        runner=args.output/'runner'
        require(seal()==hashes,'Diagnostic source changed before SDK build')
        stage(owned,receipt['stages'],'replay-build',[dotnet,'build',HERE/'Replay.csproj','--nologo','-c','Release','-o',runner,
          '-p:B3CaptureProtocolAssembly='+str(protocol)],args.output,env,240)
        require(seal()==hashes,'Diagnostic source changed after SDK build')
        executable=runner/'Replay.dll'
        require(sha(read(runner/'DafnyB3Protocol.dll',32*1024*1024)) == manifest['protocolAssemblySha256'], 'Built replay loaded protocol differs')
        runner_files=list(runner.iterdir())
        require(len(runner_files)<=32 and all(p.is_file() for p in runner_files),'Replay output file inventory bound')
        require(sum(p.lstat().st_size for p in runner_files)<=512*1024*1024,'Replay output aggregate bound')
        runner_hashes={p.name:sha(read(p)) for p in runner_files}
        receipt['runnerFileHashes']=runner_hashes
        controls=args.output/'controls'
        stage(owned,receipt['stages'],'prepare-controls',[dotnet,executable,'--mode','prepare','--baseline',HERE/'baseline','--output',controls],args.output,env,30)
        generated=json.loads(read(controls/'controls.json',65536))['rows']
        require([(x['name'],x['expected'],x['checkIds']) for x in generated] ==
          [(x['name'],x['expected'],x['checkIds']) for x in manifest['exploratory']], 'Exploratory fixture denominator differs')
        cases=[{**x,'path':HERE/x['request'],'exploratory':False} for x in manifest['baseline']]
        cases += [{**x,'path':controls/x['request'],'exploratory':True} for x in generated]
        require(len(cases)==8, 'Fixed diagnostic denominator differs')
        original_limit=resource.getrlimit(resource.RLIMIT_FSIZE)
        resource.setrlimit(resource.RLIMIT_FSIZE,(MAX_TRACE,min(original_limit[1],MAX_TRACE) if original_limit[1] != resource.RLIM_INFINITY else MAX_TRACE))
        receipt['instrumentationFileCapBytes']=MAX_TRACE
        for case in cases:
            row={'name':case['name'],'exploratory':case['exploratory'],'expected':case['expected'],'captureComplete':False,'mathematicalMatched':False}
            receipt['cases'].append(row); images=[]
            trace=args.output/(case['name']+'.trace'); result=args.output/(case['name']+'.result.json')
            try:
                require(seal()==hashes,'Diagnostic source changed before replay')
                require(sha(read(case['path'],1024*1024))==case['requestSha256'], 'Request changed before replay')
                require(sha(read(dotnet))==receipt['dotnetExecutableSha256'] and sha(read(tracer))==receipt['straceExecutableSha256'], 'Invocation executable changed before replay')
                for name,digest in runner_hashes.items(): require(sha(read(runner/name))==digest, 'Runner changed before replay')
                for name,digest in package_hashes.items(): require(sha(read(worker/name))==digest, 'Worker changed before replay')
                require(sha(read(worker/'b3-worker-manifest.json'))==manifest['workerFingerprint'] and sha(read(solver))==manifest['solverSha256'], 'Manifest/solver changed before replay')
                trace=args.output/(case['name']+'.trace'); result=args.output/(case['name']+'.result.json')
                command=[tracer,'--kill-on-exit','-I','1','-f','-ttt','-yy','-xx','-s','256',
                  '-e','trace=%process,read,readv,write,writev,pipe,pipe2,dup,dup2,dup3,fcntl,close,close_range',
                  '-e','read=0','-e','write=1','-o',trace,'--',dotnet,executable,'--mode','replay',
                  '--request',case['path'],'--request-sha256',case['requestSha256'],'--worker',worker/'DafnyB3Host.dll',
                  '--dotnet',dotnet,'--expected',case['expected'],'--output',result]
                ownership=stage(owned,receipt['stages'],case['name'],command,args.output,env,40,
                  observe=live_images(owned,manifest['solverSha256'],images),trace=trace)
                verdict=json.loads(read(result,1024*1024)); row['replay']=verdict
                row['mathematicalMatched']=verdict['mathematicalMatched']; row['actualProofResourceCount']=None
                row['traceSha256']=sha(read(trace,MAX_TRACE)); row['liveImages']=images
                row['missedDescendantPinObservations']=ownership['transientDescendantObservations']
                analysis=capture.analyze(read(trace,MAX_TRACE),ownership,images,SOLVER_PATH,manifest['solverSha256'],case['checkIds'],str(worker/'DafnyB3Host.dll'),str(dotnet),
                  read(case['path'],1024*1024),verdict['completion'])
                for stream_key,suffix in [('commandBytes','stdin.smt2'),('responseBytes','stdout.smt2')]:
                    data=analysis.pop(stream_key); path=args.output/(case['name']+'.'+suffix)
                    path.write_bytes(data); analysis[stream_key+'Sha256']=sha(data)
                row['capture']=analysis; row['captureComplete']=True
            except Exception as error:
                row['failure']=type(error).__name__+': '+str(error)
                row['liveImages']=images
                if any(not x['passed'] for x in receipt['stages']):
                    receipt['executionPoisoned']=True; break
            finally:
                if trace.exists() and 0 < trace.stat().st_size <= MAX_TRACE:
                    row['traceSha256']=sha(read(trace,MAX_TRACE))
                    try:
                        observations=capture.weak_observations(read(trace,MAX_TRACE),SOLVER_PATH)
                        for index,observation in enumerate(observations):
                            for stream_key,suffix in [('commandBytes','stdin'),('responseBytes','stdout')]:
                                data=observation.pop(stream_key)
                                (args.output/(case['name']+'.unqualified-'+str(index)+'-'+suffix+'.bytes')).write_bytes(data)
                                observation[stream_key+'Sha256']=sha(data)
                        row['unqualifiedExecLineObservations']=observations
                    except Exception as error:
                        row['unqualifiedObservationError']=type(error).__name__+': '+str(error)
                row['weakerPathAndImmutableFileObservation']={'solverPath':SOLVER_PATH,'solverSha256':manifest['solverSha256'],
                  'kernelImageQualified':row['captureComplete'],'signedAttestationClaimed':False}
                require(sha(read(dotnet))==receipt['dotnetExecutableSha256'] and sha(read(tracer))==receipt['straceExecutableSha256'], 'Invocation executable changed after replay')
                for name,digest in runner_hashes.items(): require(sha(read(runner/name))==digest, 'Runner changed after replay')
                for name,digest in package_hashes.items(): require(sha(read(worker/name))==digest, 'Worker changed after replay')
                require(sha(read(solver))==manifest['solverSha256'], 'Solver changed after replay')
                require(sha(read(worker/'b3-worker-manifest.json',65536))==manifest['workerFingerprint'], 'Worker manifest changed after replay')
                require(sha(read(case['path'],1024*1024))==case['requestSha256'], 'Exact request changed after replay')
                require(seal()==hashes,'Diagnostic source changed after replay')
        require(seal()==hashes, 'Diagnostic source changed after execution')
        receipt['diagnosticDenominatorComplete']=len(receipt['cases'])==8
        receipt['allCapturesComplete']=len(receipt['cases'])==8 and all(x['captureComplete'] for x in receipt['cases'])
        receipt['allMathematicalExpectationsMatched']=len(receipt['cases'])==8 and all(x['mathematicalMatched'] for x in receipt['cases'])
    except Exception as error:
        receipt['failure']=type(error).__name__+': '+str(error)
    finally:
        if owned is not None:
            try:
                remaining=owned.child_ids(os.getpid()); receipt['remainingDirectChildren']=remaining
                if remaining:
                    receipt['cleanupFailure']='Owned supervisor did not empty the scope; mandatory poisoned drain'
                    receipt['emergencyDrain']=owned.drain_exclusive_children(10)
                    receipt['remainingDirectChildren']=owned.child_ids(os.getpid())
            except Exception as error: receipt['cleanupFailure']=type(error).__name__+': '+str(error)
        encoded=(json.dumps(receipt,indent=2)+'\n').encode()
        if len(encoded)>4*1024*1024:
            encoded=(json.dumps({'diagnosticOnly':True,'acceptanceClaimed':False,'receiptBoundExceeded':True,
              'allCapturesComplete':False,'allMathematicalExpectationsMatched':False,
              'failure':'Aggregate receipt exceeded4MiB; raw per-case files retained without aggregate qualification'},indent=2)+'\n').encode()
        (args.output/'summary.json').write_bytes(encoded)
        print('Real SMT diagnostic: capture',receipt.get('allCapturesComplete',False),'strict math',receipt.get('allMathematicalExpectationsMatched',False),'acceptance False',flush=True)
    return 0


if __name__=='__main__': raise SystemExit(main())
