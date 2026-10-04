"""Scratch compiler/contracts or fresh complete-library BV integration receipt.

No verified-library artifact is reused. Expected failures exit zero and remain
NOT GREEN in summary.json. Complete backend/default compatibility is outside
this selected gate, even when every declared stage passes.
"""
import csv, ctypes, hashlib, json, os, pathlib, re, runpy, signal, subprocess, time
from pathlib import Path
import xml.etree.ElementTree as ET
output=pathlib.Path('out/b3-native-compile');output.mkdir(parents=True,exist_ok=True)
commands=[
 ('source-head',['git','rev-parse','HEAD']),
 ('packages',['sh','Scripts/fetch-boogie-packages.sh']),
 ('compiler',['dotnet','build','Source/Dafny/Dafny.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--nologo']),
 ('contracts',['dotnet','test','Source/DafnyCore.Test/DafnyCore.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~VerificationContractsTest|FullyQualifiedName~B3BackendSelectionTest|FullyQualifiedName~B3WorkItemTest','--results-directory',str(output/'contracts'),'--logger','trx;LogFileName=result.trx','--nologo']),
 ('language-server-build',['dotnet','build','Source/DafnyLanguageServer.Test/DafnyLanguageServer.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--nologo']),
 ('normalizer',['dotnet','test','Source/DafnyB3Normalizer.Test/DafnyB3Normalizer.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--results-directory',str(output/'normalizer'),'--logger','trx;LogFileName=result.trx','--nologo']),
 ('protocol',['dotnet','test','Source/DafnyB3Protocol.Test/DafnyB3Protocol.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--results-directory',str(output/'protocol'),'--logger','trx;LogFileName=result.trx','--nologo']),
 ('terminator-control',['python3','.github/review/b3-newline-control.py'])]
full_gate = os.environ.get('B3_FULL_GATE', '').lower() == 'true'
if full_gate:
 solver=str((output/'inputs'/os.environ['Z3_LINUX_X64']/'bin/z3').resolve())
 worker=str((output/'worker/library/B3Library.dll').resolve())
 commands += [
  ('pinned-inputs',['python3','.github/review/b3-public-inputs.py']),
  ('worker-bootstrap',['env','DOTNET_GCHeapHardLimit=C0000000','bash','Scripts/build-b3-worker.sh',str(output/'inputs/bootstrap/dafny.tar.gz'),solver,str(output/'worker')]),
  ('worker-runtime',['env','PATH='+str(pathlib.Path(solver).parent)+os.pathsep+os.environ['PATH'],
    'DOTNET_GCHeapHardLimit=C0000000','dotnet',str((output/'worker/compiler/dafny/Dafny.dll').resolve()),
    'test','--no-verify','test/worker/dfyconfig.toml','--output',str((output/'worker-runtime/tests').resolve())]),
  ('worker-java',['env','DOTNET_GCHeapHardLimit=C0000000','dotnet',
    str((output/'worker/compiler/dafny/Dafny.dll').resolve()),'build','--no-verify','--target=java',
    'target/java/src/dfyconfig.toml','--output',str((output/'worker-java/b3').resolve())]),
  ('host',['bash','Source/DafnyB3Host.Test/run-tests.sh',worker,solver]),
  ('corpus',['python3','Scripts/check-b3-integration.py','Binaries/net8.0/Dafny.dll','build/b3-host-tests/package/DafnyB3Host.dll',solver,'--output',str(output/'corpus')]),
  ('language-server',['env','DAFNY_TEST_SOLVER_PATH='+solver,'dotnet','test','Source/DafnyLanguageServer.Test/DafnyLanguageServer.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~B3CacheVerificationTest|FullyQualifiedName~B3ProjectMigrationTest|FullyQualifiedName~IdeStateObserverRetirementTest|FullyQualifiedName~CounterExampleCapabilityTest|FullyQualifiedName~ProjectManagerDatabaseTest|FullyQualifiedName~ProjectFilesTest|FullyQualifiedName~MultipleFilesProjectTest|FullyQualifiedName~CompetingProjectFilesTest|FullyQualifiedName~AdditionalAxiomsTest|FullyQualifiedName~CounterexamplesStillWorksIfNothingHasBeenVerified','--results-directory',str(output/'language-server'),'--logger','trx;LogFileName=result.trx','--nologo']),
  ('regressions',['dotnet','test','Source/IntegrationTests/IntegrationTests.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','DisplayName~git-issue-118.dfy|DisplayName~git-issue-120.dfy|DisplayName~git-issue-126.dfy|DisplayName~git-issue-126-capabilities.dfy','--results-directory',str(output/'regressions'),'--logger','trx;LogFileName=result.trx','--nologo']),
  ('map-theory',['dotnet','run','--project','Source/DafnyB3MapTheory.TestRunner','-c','Release','--','--worker',str(pathlib.Path('build/b3-host-tests/package/DafnyB3Host.dll').resolve()),'--solver',solver,'--solver-sha256','PINNED_SOLVER_DIGEST','--emit-directory',str(output/'map-requests')])]
 commands += [('visibility',['dotnet','run','--project','Source/DafnyB3Visibility.TestRunner','-c','Release','--',
   '--worker',str(pathlib.Path('build/b3-host-tests/package/DafnyB3Host.dll').resolve()),'--solver',solver,
   '--solver-sha256','PINNED_SOLVER_DIGEST','--compiler-version','EXACT_COMPILER_VERSION'])]
# Recounted from Fact/UnixFact/LinuxFact, InlineData, all finite MemberData rows,
# the source fixture manifests, and the included @Test methods. No count is a proof claim.
expected_tests={'contracts':26,'normalizer':348,'protocol':163,'language-server':40,'regressions':4}
expected_controls={'worker-runtime':37,'host':74,'corpus':69,'map-theory':17,
                   'visibility':29,'visibility-session-isolation':1,'visibility-isolation-launches':5}
proof_receipt=None
head=None
cleanup_poisoned=False
MAXIMUM_STAGE_LOG_BYTES=32 * 1024 * 1024
STAGE_TIMEOUT_SECONDS=1800
NATURAL_CHILD_GRACE_SECONDS=5
MAXIMUM_CHILD_DIAGNOSTICS=64
stage_environment={**os.environ,'GRADLE_OPTS':'-Dorg.gradle.daemon=false'}
source_receipt={}
results=[]
def validate(name):
 global proof_receipt, head
 if name=='source-head':
  head=(output/'source-head.txt').read_text().strip()
  assert re.fullmatch('[0-9a-f]{40}',head), 'Invalid source revision'
 elif name in expected_tests:
  tests=ET.parse(output/name/'result.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
  assert len(tests)==expected_tests[name] and all(t.get('outcome')=='Passed' for t in tests), 'Incomplete '+name+' denominator'
 elif name=='worker-bootstrap':
  rows=list(csv.DictReader((output/'worker/library/resources.csv').open()))
  assert rows and all(r['TestResult.Outcome']=='Passed' and r['RandomSeed']=='0' for r in rows), 'Incomplete library proof receipt'
  assert len({r['TestResult.DisplayName'] for r in rows})==len(rows), 'Duplicate library proof batch identity'
  resources=[int(r['TestResult.ResourceCount']) for r in rows]
  assert all(0 <= value <= 100000000 for value in resources), 'Invalid library proof resource count'
  verified=re.findall(r'Dafny program verifier finished with (\d+) verified, (\d+) errors?', (output/'worker-bootstrap.txt').read_text())
  assert verified==[(str(len(rows)),'0')], 'Library proof count differs from verifier output'
  receipt={'sourceManifestSha256':hashlib.sha256(pathlib.Path('ThirdParty/B3/source-manifest.json').read_bytes()).hexdigest(),
    'librarySha256':hashlib.sha256((output/'worker/library/B3Library.dll').read_bytes()).hexdigest(),
    'csvSha256':hashlib.sha256((output/'worker/library/resources.csv').read_bytes()).hexdigest(),
    'bootstrapLogSha256':hashlib.sha256((output/'worker-bootstrap.txt').read_bytes()).hexdigest(),
    'batchCount':len(rows),'resourceCount':sum(resources),'maximumBatchResources':max(resources),'randomSeeds':['0']}
  assert receipt['sourceManifestSha256']==source_receipt['sourceManifestSha256'], 'Fresh library source differs'
  proof_receipt=receipt
 elif name=='worker-runtime':
  text=(output/'worker-runtime.txt').read_text()
  assert len(re.findall(r'PASSED$',text,re.M))==expected_controls[name] and not re.search(r'FAILED|HALT',text), 'Incomplete runtime controls'
 elif name=='worker-java':
  archive=output/'worker-java/b3.jar'
  assert archive.is_file() and archive.stat().st_size>0, 'Java compiler produced no jar'
 elif name=='host':
  totals=re.findall(r'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)',(output/'host.txt').read_text())
  assert totals==[('0','163','0','163'),('0','74','0','74')], 'Expected complete protocol and host checks'
 elif name=='corpus':
  rows=json.loads((output/'corpus/summary.json').read_text())['results']
  assert len(rows)==expected_controls[name] and len({r['name'] for r in rows})==len(rows) and all(r['passed'] for r in rows), 'Incomplete actual corpus controls'
 elif name=='map-theory':
  rows=[json.loads(line) for line in (output/'map-theory.txt').read_text().splitlines() if line.startswith('{')]
  cases=[r for r in rows if r.get('kind')=='map-theory-case']
  assert len(cases)==17 and all(r['matched'] for r in cases), 'Expected 17 exact map controls'
  assert rows[-1]=={'kind':'map-theory-summary','matched':17,'total':17,'passed':True}
 elif name=='visibility':
  rows=[json.loads(line) for line in (output/'visibility.txt').read_text().splitlines() if line.startswith('{')]
  expected=json.loads(pathlib.Path('Source/DafnyB3Normalizer.Test/VisibilityInputs/cases.json').read_text())
  cases=[r for r in rows if r.get('kind')=='visibility-case']
  assert len(expected['cases'])==len(cases)==expected_controls[name], 'Incomplete visibility denominator'
  assert len({r['fixture'] for r in cases})==len(cases) and {r['fixture'] for r in cases}=={r['file'] for r in expected['cases']}
  fixtures=pathlib.Path('Source/DafnyB3Normalizer.Test/VisibilityInputs')
  assert all(r['fixtureSha256']==hashlib.sha256((fixtures/r['fixture']).read_bytes()).hexdigest() for r in cases), 'Visibility fixture bytes differ'
  assert all(r['matched'] and r['allStrict'] and r['attemptsMatched'] for r in cases), 'Non-strict visibility verdict'
  solver_digest=hashlib.sha256(pathlib.Path(solver).read_bytes()).hexdigest()
  assert all(r['compilerVersion']=='4.11.0+'+head and r['normalizerVersion']=='experimental-3' and
    r['workerSourceFingerprint']==source_receipt['sourceManifestSha256'] and
    r['b3Commit']=='ea6e8a18dfe9e317d313de769291f989957dc5f2' and
    r['bootstrapCompiler']=='4.11.0+fcb2042d.review.a171069d' and
    r['solverVersion']=='5.1.0' and r['solverSha256']==solver_digest for r in cases), 'Visibility input identities differ'
  assert all(len({r[key] for r in cases})==1 and re.fullmatch('[0-9a-f]{64}',cases[0][key])
    for key in ('compilerSha256','normalizerSha256','workerFingerprint')), 'Visibility binary identities differ'
  isolation=[r for r in rows if r.get('kind')=='visibility-session-isolation']
  assert len(isolation)==1 and isolation[0]['matched'] and len(isolation[0]['completions'])==5, 'Incomplete fresh isolation launches'
  assert isolation[0]['solverVersion']=='5.1.0' and isolation[0]['solverSha256']==solver_digest and isolation[0]['workerFingerprint']==cases[0]['workerFingerprint']
  assert len(rows)==31 and rows[-1]=={'kind':'visibility-summary','matched':30,'total':30,'isolationMatched':True,'passed':True}
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
 try:
  assert not cleanup_poisoned and not direct_children(), 'Require empty owned child scope before each stage'
  with log_path.open('wb') as log:
   process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,start_new_session=True,
     cwd="ThirdParty/B3" if name in {"worker-runtime","worker-java"} else None,env=stage_environment)
   deadline=time.monotonic()+STAGE_TIMEOUT_SECONDS
   while process.poll() is None:
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
   'failure':failure,'residualChildrenBeforeGrace':residual_before_grace,
   'residualChildrenAtCompletion':residual,'cleanupSignals':cleanup_count,
   'naturalChildGraceSeconds':NATURAL_CHILD_GRACE_SECONDS,'childrenBeforeCleanup':before_cleanup,
   'remainingAdoptedChildren':remaining,'cleanupPoisoned':cleanup_poisoned,
   'maximumLogBytes':MAXIMUM_STAGE_LOG_BYTES,'observedLogBytes':log_bytes,'logTruncated':truncated,
   'safetyDeadlineSeconds':STAGE_TIMEOUT_SECONDS,
   'hashedLogBytes':len(log_prefix),'logSha256':hashlib.sha256(log_prefix).hexdigest() if log_path.exists() else None}


setup_stage='child-scope'
try:
 assert Path('/proc/self/task').exists() and hasattr(os,'pidfd_open') and hasattr(signal,'pidfd_send_signal'), 'Requires Linux child-subreaper/pidfd ownership cleanup'
 assert not direct_children() and ctypes.CDLL(None,use_errno=True).prctl(36,1,0,0,0)==0, 'Require an empty dedicated child-subreaper scope'
 setup_stage='source-inventory'
 # Reuse only the source inventory validator, never a compiled library or proof cache.
 runpy.run_path('.github/review/b3-bitvector-proof.py',run_name='b3_inventory')['validate_source'](source_receipt)
except Exception as error:
 results.append({'stage':setup_stage,'exitCode':1,'error':type(error).__name__+': '+str(error)})
 commands=[]
for name,command in commands:
 try:
  if name in {'map-theory','visibility'}:
   command[command.index('PINNED_SOLVER_DIGEST')]=hashlib.sha256(pathlib.Path(solver).read_bytes()).hexdigest()
  if name=='visibility':
   command[command.index('EXACT_COMPILER_VERSION')]='4.11.0+'+head
  result=run_stage(name,command)
 except Exception as error:
  result={'stage':name,'exitCode':1,'command':command,'error':type(error).__name__+': '+str(error)}
 results.append(result)
 code=result['exitCode']
 print(name,code,result.get('failure') or result.get('error') or '',flush=True)
 if code!=0:
  log_path=output/(name+'.txt')
  if log_path.exists():
   with log_path.open('rb') as captured:
    captured.seek(max(0,log_path.stat().st_size-12000))
    print(captured.read(12000).decode('utf-8',errors='replace'),flush=True)
  # Failed drain poisons this owned scope and prevents every subsequent stage.
  if cleanup_poisoned:
   break
  # These terminal checks share only successfully built inputs; record every independent verdict.
  if name not in {'worker-java','corpus','language-server','regressions','map-theory','visibility'}:
   break
passed=len(results)==len(commands) and all(r['exitCode']==0 for r in results)
(output/'summary.json').write_text(json.dumps({'passed':passed,'fullGate':full_gate,'head':head,'stages':results,
 'sourceInventory':source_receipt,'libraryProof':proof_receipt,'completeLibraryVerified':proof_receipt is not None,
 'libraryBinaryProduced':proof_receipt is not None,'expectedTestCounts':expected_tests,'expectedControlCounts':expected_controls,
 'defaultCompatibilityVerified':False,'cleanupPoisoned':cleanup_poisoned,
 'cleanupScope':'Dedicated Linux child-subreaper with pidfd-signalled validated owned adopted children',
 'stageEnvironmentOverrides':{'GRADLE_OPTS':stage_environment['GRADLE_OPTS']}},indent=2)+'\n')
with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
 summary.write('B3 native compilation probe: '+('PASS' if passed else 'NOT GREEN')+'\n\n')
 for result in results: summary.write('- '+result['stage']+': exit '+str(result['exitCode'])+'\n')
 summary.write('\nThis scratch probe records expected failures and exits zero. Inspect summary.json; job success alone is not acceptance. ' + ('The full selected gate freshly verifies the exact BV library and runs the declared runtime/corpus/visibility controls with Z3 5.1.0; no Real library cache is reused.' if full_gate else 'No Dafny verifier or solver proof workload runs in this compiler/contracts probe.') + ' Complete backend and default compatibility acceptance remain separate.\n')
