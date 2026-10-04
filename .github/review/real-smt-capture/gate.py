#!/usr/bin/env python3
"""Public-archive wrapper for the sealed Real SMT diagnostic, never an acceptance gate."""
import io
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import signal
import stat
import hashlib
import sys
import zipfile
import types

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
PUBLIC=[
 {'run':37232113837,'head':'a4d3ec13a2929dd45f2f27770d458aa869f6667e','artifact':11314207981,
  'bytes':201508453,'sha256':'37233c026b7744d1c66d20169a81a698cb70ca21dbe68dbc6403d19a4d1013f3','directory':'original'},
 {'run':37221479537,'head':'ee32faedf6968ffef9baf5e3ab18caa91180a670','artifact':11311016429,
  'bytes':145457636,'sha256':'76be6dadc245884a15c52b0b94d6eef8f16c96caed2d12ed23b3956c91e617fe','directory':'prerequisite'},
]


def bootstrap():
    # The outer caller binds this source/seal to the reviewed Git commit. Capture
    # the module bytes directly; neither import machinery nor pycache is consulted.
    path=HERE/'coordinator.py'
    def captured_regular(path,maximum):
        before=path.lstat()
        if not stat.S_ISREG(before.st_mode) or not 0<before.st_size<=maximum: raise ValueError('Bounded source file required')
        fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
        try:
            pinned=os.fstat(fd)
            if (pinned.st_dev,pinned.st_ino)!=(before.st_dev,before.st_ino): raise ValueError('Source identity changed')
            data=bytearray()
            while block:=os.read(fd,65536):
                data.extend(block)
                if len(data)>maximum: raise ValueError('Source read exceeded bound')
            after=os.fstat(fd)
            if (pinned.st_dev,pinned.st_ino,pinned.st_size,pinned.st_mtime_ns,pinned.st_ctime_ns)!=(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns,after.st_ctime_ns) or len(data)!=pinned.st_size: raise ValueError('Source changed during capture')
            return bytes(data)
        finally: os.close(fd)
    source=captured_regular(path,1024*1024)
    declared=json.loads(captured_regular(HERE/'source-manifest.json',65536))['files']['coordinator.py']
    if len(source)!=declared['bytes'] or hashlib.sha256(source).hexdigest()!=declared['sha256']: raise ValueError('Coordinator source does not match seal before loading')
    module=types.ModuleType('b3_capture_outer'); module.__file__=str(path)
    exec(compile(source,str(path),'exec'),module.__dict__)
    hashes=module.seal()
    module.require(module.sha(source)==hashes['coordinator.py'],'Captured coordinator source differs from seal')
    return module,hashes


def extract(inner,archive,destination):
    captured=inner.read(archive,256*1024*1024)
    require=inner.require; files={}; total=0
    require(not destination.exists(),'Archive destination must be fresh'); destination.mkdir()
    with zipfile.ZipFile(io.BytesIO(captured)) as zipped:
        entries=zipped.infolist(); require(0<len(entries)<=20000,'Archive inventory bound')
        seen=set()
        for entry in entries:
            path=PurePosixPath(entry.filename)
            require(not path.is_absolute() and '\\' not in entry.filename and
              all(x not in ('','.', '..') for x in entry.filename.rstrip('/').split('/')) and
              path.parts[:2]==('out','b3-native-compile'),'Unsafe/unexpected public archive path')
            require(entry.filename not in seen,'Duplicate public archive member'); seen.add(entry.filename)
            mode=entry.external_attr>>16
            require(entry.flag_bits&1==0 and not stat.S_ISLNK(mode),'Encrypted/symlink public archive member')
            target=destination.joinpath(*path.parts)
            if entry.is_dir():
                require(stat.S_IFMT(mode) in (0,stat.S_IFDIR),'Invalid directory mode'); target.mkdir(parents=True,exist_ok=True); continue
            require(stat.S_IFMT(mode) in (0,stat.S_IFREG) and 0<=entry.file_size<=256*1024*1024,'Public file mode/size bound')
            total+=entry.file_size; require(total<=1024*1024*1024,'Public archive aggregate bound')
            target.parent.mkdir(parents=True,exist_ok=True)
            hasher=inner.hashlib.sha256(); count=0
            with zipped.open(entry) as source, target.open('xb') as output:
                while data:=source.read(65536):
                    count+=len(data); require(count<=entry.file_size,'Archive file grew beyond declared size')
                    hasher.update(data); output.write(data)
            require(count==entry.file_size,'Archive file was incomplete')
            files[path.as_posix()]={'bytes':count,'sha256':hasher.hexdigest()}
    return destination/'out/b3-native-compile',files


def main():
    if len(sys.argv)!=2: raise ValueError('Usage: gate.py NEW_OUTPUT_DIRECTORY')
    output=Path(sys.argv[1]).resolve(); output.mkdir(parents=True,exist_ok=False)
    receipt={'diagnosticOnly':True,'acceptanceClaimed':False,'publicInputs':PUBLIC,'stages':[],'cleanupPoisoned':False}
    inner=owned=None; inventories=[]
    try:
        inner,hashes=bootstrap(); owned=inner.load_source('owned-process.py',hashes['owned-process.py'])
        receipt['sourceSealSha256']=inner.sha(inner.read(HERE/'source-manifest.json',65536))
        receipt['ownership']=owned.enable_subreaper()
        def interrupted(signum,frame):
            if not owned.record_cancellation('Outer cancellation '+str(signum)): raise InterruptedError('Outer interrupted')
        signal.signal(signal.SIGTERM,interrupted); signal.signal(signal.SIGINT,interrupted)
        env=inner.filtered_environment()
        gh=Path(shutil.which('gh') or '').resolve(strict=True)
        inner.require(gh.is_file(),'Resolved gh executable required')
        receipt['ghExecutableSha256']=inner.sha(inner.read(gh))
        download_env=dict(env)
        for name in ['GH_TOKEN','GITHUB_TOKEN']:
            if name in os.environ: download_env[name]=os.environ[name]
        receipt['downloadEnvironmentKeys']=sorted(download_env)
        receipt['nonDownloadEnvironmentKeys']=sorted(env)
        def stage(name,command,path,maximum,timeout,environment=env):
            def bounds(): inner.require(path.stat().st_size<=maximum,'Outer stage output bound')
            with path.open('xb') as stream:
                row=owned.run_owned([str(x) for x in command],stream,environment,timeout,check_bounds=bounds)
            row['stage']=name; row['outputSha256']=inner.sha(inner.read(path,maximum)) if path.stat().st_size else None
            receipt['stages'].append(row)
            inner.require(row['passed'],'Outer owned stage failed: '+name)
        roots=[]
        for expected in PUBLIC:
            prefix=str(expected['run']); run_file=output/(prefix+'-run.json'); meta_file=output/(prefix+'-artifacts.json')
            stage(prefix+'-run-metadata',[gh,'api','repos/erniecohen/dafny/actions/runs/'+prefix],run_file,262144,60,download_env)
            run=json.loads(inner.read(run_file,262144))
            inner.require(run['id']==expected['run'] and run['head_sha']==expected['head'] and run['event']=='workflow_dispatch' and
              run['path']=='.github/workflows/review.yml' and run['status']=='completed' and run['conclusion']=='success', 'Public run metadata differs')
            stage(prefix+'-artifact-metadata',[gh,'api','repos/erniecohen/dafny/actions/runs/'+prefix+'/artifacts'],meta_file,262144,60,download_env)
            listing=json.loads(inner.read(meta_file,262144))
            inner.require(listing['total_count']==1 and len(listing['artifacts'])==1,'Public artifact inventory differs')
            artifact=listing['artifacts'][0]
            inner.require(artifact['id']==expected['artifact'] and artifact['name']=='b3-native-compile' and artifact['expired'] is False and
              artifact['size_in_bytes']==expected['bytes'] and artifact['digest']=='sha256:'+expected['sha256'] and
              artifact['workflow_run']['id']==expected['run'] and artifact['workflow_run']['head_sha']==expected['head'],'Public artifact metadata differs')
            archive=output/(prefix+'.zip')
            stage(prefix+'-download',[gh,'api','repos/erniecohen/dafny/actions/artifacts/'+str(expected['artifact'])+'/zip'],archive,256*1024*1024,240,download_env)
            inner.require(archive.stat().st_size==expected['bytes'] and inner.sha(inner.read(archive))==expected['sha256'],'Public archive bytes differ')
            root,inventory=extract(inner,archive,output/expected['directory']); roots.append(root)
            inventory_path=output/(prefix+'-inventory.json'); inventory_path.write_text(json.dumps(inventory,indent=2)+'\n')
            receipt.setdefault('extractedInventories',[]).append({'run':expected['run'],'inventorySha256':inner.sha(inner.read(inventory_path,4*1024*1024)),
              'files':len(inventory),'bytes':sum(x['bytes'] for x in inventory.values())})
            inventories.append((output/expected['directory'],inventory))
        # Tokens are excluded from every SDK/parser/trace scope and its descendants.
        dotnet=Path(shutil.which('dotnet') or '').resolve(strict=True)
        tracer=Path(shutil.which('strace') or '').resolve(strict=True)
        inner.require(dotnet.is_file() and tracer.is_file(),'Resolved .NET8/strace6.8 required')
        parser_log=output/'parser-controls.log'
        stage('parser-controls',[sys.executable,'-B','-m','unittest','discover','-s',HERE,'-p','test_capture.py'],parser_log,4*1024*1024,30)
        text=inner.read(parser_log,4*1024*1024).decode()
        inner.require('Ran 15 tests' in text and text.rstrip().endswith('OK'),'Parser control denominator differs')
        diagnostic=output/'capture'
        stage('real-smt-capture',[sys.executable,'-B',HERE/'coordinator.py','--captured-real-artifacts',roots[0]/'real-triage',
          '--prerequisite',roots[1],'--dotnet',dotnet,'--strace',tracer,'--output',diagnostic],output/'capture.log',4*1024*1024,900)
        summary=json.loads(inner.read(diagnostic/'summary.json',4*1024*1024))
        inner.require(summary['diagnosticOnly'] is True and summary['acceptanceClaimed'] is False and summary['libraryRebuilt'] is False,
                      'Diagnostic receipt boundary differs')
        receipt['diagnosticReceiptSha256']=inner.sha(inner.read(diagnostic/'summary.json',4*1024*1024)); receipt['diagnostic']=summary
        inner.require(inner.seal()==hashes,'Diagnostic source changed after stages')
        for directory,inventory in inventories:
            for name,entry in inventory.items():
                data=inner.read(directory/name,allow_empty=True)
                inner.require(len(data)==entry['bytes'] and inner.sha(data)==entry['sha256'],'Extracted public input changed')
        inner.require(inner.sha(inner.read(gh))==receipt['ghExecutableSha256'],'Download executable changed')
        receipt['diagnosticReceiptProduced']=True
    except Exception as error: receipt['failure']=type(error).__name__+': '+str(error)
    finally:
        if owned is not None:
            try:
                remaining=owned.child_ids(os.getpid()); receipt['remainingDirectChildren']=remaining
                if remaining:
                    receipt['cleanupPoisoned']=True; receipt['emergencyDrain']=owned.drain_exclusive_children(10)
                    receipt['remainingDirectChildren']=owned.child_ids(os.getpid())
            except Exception as error: receipt['cleanupPoisoned']=True; receipt['cleanupFailure']=type(error).__name__+': '+str(error)
        encoded=(json.dumps(receipt,indent=2)+'\n').encode()
        if len(encoded)>8*1024*1024:
            encoded=(json.dumps({'diagnosticOnly':True,'acceptanceClaimed':False,'receiptBoundExceeded':True,
              'failure':'Outer aggregate receipt exceeded8MiB; individual stage/input files retained'},indent=2)+'\n').encode()
        (output/'summary.json').write_bytes(encoded)
        print('Real SMT capture diagnostic receipt:',receipt.get('diagnosticReceiptProduced',False),'acceptance: False',flush=True)
        if os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
                summary.write('Real SMT capture: diagnostic evidence only; zero exit does not establish acceptance.\n\n')
                if 'failure' in receipt: summary.write(receipt['failure']+'\n')
                if 'diagnostic' in receipt:
                    for key in ['diagnosticDenominatorComplete','allCapturesComplete','allMathematicalExpectationsMatched']:
                        summary.write('- '+key+': '+str(receipt['diagnostic'].get(key,False))+'\n')
    return 0


if __name__=='__main__': raise SystemExit(main())
