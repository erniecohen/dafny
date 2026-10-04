#!/usr/bin/env python3
"""Sealed typed-source diagnosis against one compiled public archive, never a verification gate."""
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import signal
import stat
import sys
import types
import zipfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
MAX_FILE=64*1024*1024
MAX_LOG=32*1024*1024
MAX_RECEIPT=8*1024*1024
EXPECTED_FILES={'Program.cs','UnsignedSourceProbe.csproj','nuget.config','fixture.dfy','inputs.json',
                'owned-process.py','coordinator.py','gate.py','README.md','.gitattributes'}
EXPECTED_ROOT_FILES={'.github/workflows/review.yml'}

def require(condition,message):
    if not condition: raise ValueError(message)

def read(path,maximum=MAX_FILE,allow_empty=False):
    path=Path(path); before=path.lstat()
    require(stat.S_ISREG(before.st_mode) and (0 if allow_empty else 1)<=before.st_size<=maximum,'Bounded regular input required: '+path.name)
    fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
    try:
        pinned=os.fstat(fd); require((pinned.st_dev,pinned.st_ino)==(before.st_dev,before.st_ino),'Input identity changed at open')
        blocks=[]; size=0
        while block:=os.read(fd,min(65536,maximum+1-size)):
            blocks.append(block); size+=len(block); require(size<=maximum,'Input actual byte bound')
        after=os.fstat(fd)
        require(size==pinned.st_size and
          (pinned.st_dev,pinned.st_ino,pinned.st_size,pinned.st_mtime_ns,pinned.st_ctime_ns)==
          (after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns,after.st_ctime_ns),'Input changed during read')
        return b''.join(blocks)
    finally: os.close(fd)

def sha(data): return hashlib.sha256(data).hexdigest()

def seal():
    manifest=json.loads(read(HERE/'source-manifest.json',65536))
    require(manifest['schemaVersion']==1 and manifest['diagnosticOnly'] is True,'Source seal schema')
    require(set(manifest['files'])==EXPECTED_FILES and set(manifest['rootFiles'])==EXPECTED_ROOT_FILES,'Exact source inventory required')
    digests={}
    for folder,entries in [(HERE,manifest['files']),(ROOT,manifest['rootFiles'])]:
        for name,entry in entries.items():
            require(not Path(name).is_absolute() and '..' not in Path(name).parts,'Unsafe sealed path')
            data=read(folder/name,1024*1024)
            require(len(data)==entry['bytes'] and sha(data)==entry['sha256'],'Source seal changed: '+name)
            digests[name]=entry['sha256']
    return digests

def load_source(name,digest):
    path=HERE/name; captured=read(path,1024*1024)
    require(sha(captured)==digest,'Captured Python module changed before loading')
    module=types.ModuleType('unsigned_source_'+name.replace('.','_')); module.__file__=str(path)
    exec(compile(captured,str(path),'exec'),module.__dict__)
    return module

def filtered_environment():
    allowed={'PATH','HOME','DOTNET_ROOT','TMPDIR','LANG','LC_ALL','TZ','CI'}
    env={name:value for name,value in os.environ.items() if name in allowed}
    env.update({'DOTNET_PROCESSOR_COUNT':'1','DOTNET_GCHeapHardLimit':'40000000',
      'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER':'1','MSBUILDDISABLENODEREUSE':'1',
      'UseSharedCompilation':'false','DOTNET_CLI_TELEMETRY_OPTOUT':'1','DOTNET_NOLOGO':'1'})
    return env

def validate_inputs(inputs):
    require(inputs['schemaVersion']==1 and inputs['diagnosticOnly'] is True and inputs['expectedUnits']==3,'Frozen input schema')
    require(inputs['publicHead']=='7b60f93c2cf81486178eb7fcd6fdc35f7e868c71' and inputs['publicRun']==37236538953 and
      inputs['publicArtifact']==11315592727 and inputs['archiveBytes']==10380775 and
      inputs['archiveSha256']=='599958e1acbb7229345ef32bbf6a5ef260a322edb2ceba831bd66ced503406e5','Exact original archive required')
    fixture=read(HERE/'fixture.dfy',4096)
    require(inputs['fixtureUri']=='file:///B3VisibilityTests.dfy' and len(fixture)==inputs['fixtureBytes']==201 and
      sha(fixture)==inputs['fixtureSha256']=='7a88712dd0e3f04549981d39986dd16f81898d9f431e4c24c2122d8f9e994b29','Exact original fixture required')
    files=inputs['assemblyFiles'];require(len(files)==89,'Exact flat captured compiler input denominator')
    required={'Dafny.dll','DafnyCore.dll','DafnyB3Protocol.dll','DafnyPrelude.bpl','Dafny.deps.json','Dafny.runtimeconfig.json'}
    require(required<=set(files) and sum(name.startswith('Boogie.') and name.endswith('.dll') for name in files)==13,'Captured compiler/Boogie input inventory')
    for name,entry in files.items():
        require('/' not in name and '\\' not in name and name not in ('','.', '..') and
          (name.endswith('.dll') or name.endswith('.deps.json') or name.endswith('.runtimeconfig.json') or name=='DafnyPrelude.bpl') and
          0<entry['bytes']<=MAX_FILE and len(entry['sha256'])==64,'Captured compiler file bound/name')

def validate_binaries(folder,files,exact=False):
    if exact: require({p.name for p in folder.iterdir()}==set(files),'Flat compiler input inventory changed')
    for name,entry in files.items():
        data=read(folder/name)
        require(len(data)==entry['bytes'] and sha(data)==entry['sha256'],'Captured compiler input changed: '+name)

def extract(archive,destination,inputs):
    require(not destination.exists(),'Extraction destination must be fresh'); destination.mkdir()
    seen=set(); flat={}; total=0; receipt=None
    with zipfile.ZipFile(io.BytesIO(read(archive,16*1024*1024))) as zipped:
        entries=zipped.infolist();require(0<len(entries)<=20000,'Public archive inventory bound')
        for entry in entries:
            path=PurePosixPath(entry.filename); mode=entry.external_attr>>16
            require(not path.is_absolute() and '\\' not in entry.filename and
              all(part not in ('','.', '..') for part in entry.filename.rstrip('/').split('/')),'Unsafe public archive path')
            require(entry.filename not in seen and entry.flag_bits&1==0 and not stat.S_ISLNK(mode),'Duplicate/encrypted/symlink archive member');seen.add(entry.filename)
            require(path.parts[:2] in [('Binaries','net8.0'),('out','b3-native-compile')], 'Unexpected public archive root')
            if entry.is_dir(): require(stat.S_IFMT(mode) in (0,stat.S_IFDIR),'Directory archive mode');continue
            require(stat.S_IFMT(mode) in (0,stat.S_IFREG) and 0<=entry.file_size<=MAX_FILE,'Public file mode/size bound')
            total+=entry.file_size;require(total<=256*1024*1024,'Public archive aggregate bound')
            selected=len(path.parts)==3 and path.parts[:2]==('Binaries','net8.0') and path.name in inputs['assemblyFiles']
            if len(path.parts)==3 and path.parts[:2]==('Binaries','net8.0') and (path.name.endswith('.dll') or path.name.endswith('.deps.json') or path.name.endswith('.runtimeconfig.json') or path.name=='DafnyPrelude.bpl'):
                require(selected,'Unpinned flat compiler input')
            if not selected and entry.filename!='out/b3-native-compile/summary.json': continue
            with zipped.open(entry) as source:
                data=source.read(MAX_FILE+1);require(len(data)==entry.file_size<=MAX_FILE,'Archive member actual byte bound')
            if selected:
                expected=inputs['assemblyFiles'][path.name]
                require(len(data)==expected['bytes'] and sha(data)==expected['sha256'],'Archive compiler bytes differ')
                (destination/path.name).write_bytes(data);(destination/path.name).chmod(0o444);flat[path.name]=expected
            else:
                require(sha(data)==inputs['originalReceiptSha256'],'Original failed receipt bytes differ');receipt=data
    require(set(flat)==set(inputs['assemblyFiles']) and receipt is not None,'Complete compiled input and original receipt required')
    original=json.loads(receipt)
    require(original['head']==inputs['publicHead'] and original['passed'] is False and original['fullGate'] is False and
      original['completeLibraryVerified'] is False and original['libraryBinaryProduced'] is False,'Original failed-gate boundary differs')
    compiler=[row for row in original['stages'] if row['stage']=='compiler']
    require(len(compiler)==1 and compiler[0]['exitCode']==compiler[0]['actualProcessExitCode']==0 and
      compiler[0]['failure'] is None and compiler[0]['cleanupPoisoned'] is False,'Original compiler stage not accepted')
    return receipt

def main(output_arg):
    output=Path(output_arg).resolve(); output.mkdir(parents=True,exist_ok=False)
    receipt={'diagnosticOnly':True,'acceptanceClaimed':False,'compiledSourceOnly':True,'originalGatePassed':False,
      'completeLibraryVerified':False,'libraryBinaryProduced':False,'verificationAttempted':False,
      'diagnosticReceiptProduced':False,'stages':[],'cleanupPoisoned':False}
    owned=None
    try:
        hashes=seal(); inputs=json.loads(read(HERE/'inputs.json',65536));validate_inputs(inputs)
        receipt['sourceSealSha256']=sha(read(HERE/'source-manifest.json',65536));receipt['publicInput']=inputs
        owned=load_source('owned-process.py',hashes['owned-process.py']);receipt['ownership']=owned.enable_subreaper()
        def interrupted(signum,frame):
            if not owned.record_cancellation('Unsigned source diagnostic cancelled '+str(signum)): raise InterruptedError('Diagnostic interrupted')
        signal.signal(signal.SIGTERM,interrupted);signal.signal(signal.SIGINT,interrupted)
        env=filtered_environment();download_env=dict(env)
        for name in ['GH_TOKEN','GITHUB_TOKEN']:
            if name in os.environ:download_env[name]=os.environ[name]
        receipt['downloadEnvironmentKeys']=sorted(download_env);receipt['sdkAndRunnerEnvironmentKeys']=sorted(env)
        gh=Path(shutil.which('gh') or '').resolve(strict=True);dotnet=Path(shutil.which('dotnet') or '').resolve(strict=True)
        receipt['executables']={}
        for path in [gh,dotnet]:
            captured=read(path)
            receipt['executables'][path.name]={'bytes':len(captured),'sha256':sha(captured)}
        def validate_executables():
            for path in [gh,dotnet]:
                captured=read(path);expected=receipt['executables'][path.name]
                require(len(captured)==expected['bytes'] and sha(captured)==expected['sha256'],
                        'SDK/download executable changed: '+path.name)
        def stage(name,command,log,maximum=MAX_LOG,timeout=120,environment=env,checks=None):
            require(seal()==hashes,'Source changed before stage');validate_executables()
            def bounds():
                require(log.stat().st_size<=maximum,'Stage output bound')
                if checks is not None:checks()
            try:
                with log.open('xb') as stream: row=owned.run_owned([str(x) for x in command],stream,environment,timeout,check_bounds=bounds)
            finally: validate_executables()
            row.update({'stage':name,'outputBytes':log.stat().st_size,'outputSha256':sha(read(log,maximum,allow_empty=True))})
            receipt['stages'].append(row)
            require(seal()==hashes,'Source changed after stage');require(row['passed'],'Owned stage failed: '+name)
        stage('public-run-metadata',[gh,'api','repos/erniecohen/dafny/actions/runs/'+str(inputs['publicRun'])],output/'run.json',262144,60,download_env)
        run=json.loads(read(output/'run.json',262144))
        require(run['id']==inputs['publicRun'] and run['head_sha']==inputs['publicHead'] and run['status']=='completed' and
          run['event']=='workflow_dispatch' and run['path']=='.github/workflows/review.yml' and run['conclusion']=='success','Original public diagnostic workflow metadata differs')
        stage('public-artifact-metadata',[gh,'api','repos/erniecohen/dafny/actions/artifacts/'+str(inputs['publicArtifact'])],output/'artifact.json',262144,60,download_env)
        artifact=json.loads(read(output/'artifact.json',262144))
        require(artifact['id']==inputs['publicArtifact'] and artifact['name']=='b3-native-compile' and artifact['expired'] is False and
          artifact['size_in_bytes']==inputs['archiveBytes'] and artifact['digest']=='sha256:'+inputs['archiveSha256'] and
          artifact['workflow_run']['id']==inputs['publicRun'] and artifact['workflow_run']['head_sha']==inputs['publicHead'],'Public artifact metadata differs')
        archive=output/'compiled-public.zip'
        stage('public-archive',[gh,'api','repos/erniecohen/dafny/actions/artifacts/'+str(inputs['publicArtifact'])+'/zip'],archive,16*1024*1024,120,download_env)
        require(len(read(archive,16*1024*1024))==inputs['archiveBytes'] and sha(read(archive,16*1024*1024))==inputs['archiveSha256'],'Public archive byte pin differs')
        binaries=output/'captured-compiler';original=extract(archive,binaries,inputs);(output/'original-failed-summary.json').write_bytes(original)
        validate_binaries(binaries,inputs['assemblyFiles'],exact=True)
        stage('sdk-info',[dotnet,'--info'],output/'sdk-info.log',262144,60)
        validate_binaries(binaries,inputs['assemblyFiles'],exact=True)
        stage('sdk-version',[dotnet,'--version'],output/'sdk-version.log',65536,60)
        require(read(output/'sdk-version.log',65536).decode().strip().startswith('8.'),'.NET8 SDK required')
        sdk_source=output/'sdk-source';sdk_source.mkdir()
        for name in ['Program.cs','UnsignedSourceProbe.csproj','nuget.config']:(sdk_source/name).write_bytes(read(HERE/name,1024*1024))
        probe=output/'probe'
        stage('diagnostic-sdk-build',[dotnet,'build',sdk_source/'UnsignedSourceProbe.csproj','-c','Release','-m:1',
          '-p:UseSharedCompilation=false','-p:ImportDirectoryBuildProps=false','-p:ImportDirectoryBuildTargets=false',
          '-p:B3CapturedDirectory='+str(binaries),'-o',probe,'--nologo','-p:RestoreConfigFile='+str(sdk_source/'nuget.config')],output/'sdk-build.log')
        validate_binaries(binaries,inputs['assemblyFiles'],exact=True)
        require(all(read(sdk_source/name,1024*1024)==read(HERE/name,1024*1024) for name in ['Program.cs','UnsignedSourceProbe.csproj','nuget.config']),'SDK source copy changed')
        # Copy/pin every captured flat runtime input. The runner uses its generated
        # net8 runtimeconfig, with original compiler deps/runtimeconfigs preserved separately.
        for name in inputs['assemblyFiles']:
            target=probe/name
            if target.exists():require(read(target)==read(binaries/name),'SDK copied different compiler assembly')
            else:shutil.copyfile(binaries/name,target)
            target.chmod(0o444)
        validate_binaries(probe,inputs['assemblyFiles'])
        runner=probe/'UnsignedSourceProbe.dll';runner_files={}
        for name in ['UnsignedSourceProbe.dll','UnsignedSourceProbe.deps.json','UnsignedSourceProbe.runtimeconfig.json']:
            captured=read(probe/name)
            runner_files[name]={'bytes':len(captured),'sha256':sha(captured)}
        receipt['runnerFiles']=runner_files;receipt['runnerSha256']=runner_files[runner.name]['sha256']
        validate_binaries(probe,runner_files)
        typed=output/'typed';typed.mkdir()
        def typed_bounds():
            paths=list(typed.iterdir());require(len(paths)<=20 and sum(path.stat().st_size for path in paths)<=MAX_RECEIPT,'Typed output aggregate8MiB/20file bound')
        try:
            stage('typed-source-capture',[dotnet,runner,HERE/'fixture.dfy',typed,inputs['fixtureSha256']],output/'typed-capture.log',timeout=120,checks=typed_bounds)
        finally:
            validate_binaries(binaries,inputs['assemblyFiles'],exact=True);validate_binaries(probe,inputs['assemblyFiles'])
            validate_binaries(probe,runner_files)
        typed_bounds();diagnostic=json.loads(read(typed/'typed-source.json',MAX_RECEIPT))
        require(diagnostic['diagnosticOnly'] is True and diagnostic['acceptanceClaimed'] is False and
          diagnostic['verificationAttempted'] is False and diagnostic['allUnitsCaptured'] is True and
          diagnostic['unitCount']==3 and len(diagnostic['units'])==3 and diagnostic['fixtureSha256']==inputs['fixtureSha256'],
          'Complete original three-unit diagnostic receipt required')
        require([unit['index'] for unit in diagnostic['units']]==list(range(3)) and
          all(unit['source']['rawBlockCount']>0 for unit in diagnostic['units']),'Unit/raw inventory differs')
        receipt['diagnosticReceiptSha256']=sha(read(typed/'typed-source.json',MAX_RECEIPT))
        receipt['normalizationOutcomes']=[{'index':unit['index'],'name':unit['name'],**unit['normalization']} for unit in diagnostic['units']]
        require(seal()==hashes,'Frozen source changed after capture')
        validate_executables()
        require(sha(read(archive,16*1024*1024))==inputs['archiveSha256'],'Public archive changed after capture')
        receipt['diagnosticReceiptProduced']=True
    except Exception as error:receipt['failure']=type(error).__name__+': '+str(error)
    finally:
        if owned is not None:
            try:
                children=owned.child_ids(os.getpid());receipt['remainingDirectChildren']=children
                if children:
                    receipt['cleanupPoisoned']=True;receipt['emergencyDrain']=owned.drain_exclusive_children(10)
                    receipt['remainingDirectChildren']=owned.child_ids(os.getpid())
            except Exception as error:receipt['cleanupPoisoned']=True;receipt['cleanupFailure']=type(error).__name__+': '+str(error)
        encoded=(json.dumps(receipt,indent=2)+'\n').encode()
        if len(encoded)>MAX_RECEIPT:encoded=(json.dumps({'diagnosticOnly':True,'acceptanceClaimed':False,'failure':'Outer receipt8MiB bound exceeded'})+'\n').encode()
        (output/'summary.json').write_bytes(encoded)
        print('Unsigned typed-source diagnostic produced:',receipt['diagnosticReceiptProduced'],'acceptance: False',flush=True)
        if os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
                summary.write('Unsigned source capture is diagnostic only; zero exit establishes no verification or acceptance.\n\n')
                summary.write('Complete three-unit receipt: '+str(receipt['diagnosticReceiptProduced'])+'\n\n')
                if 'failure' in receipt:summary.write(receipt['failure']+'\n')
    return 0
