import csv, hashlib, json, os, pathlib, re, subprocess
import xml.etree.ElementTree as ET
output=pathlib.Path('out/b3-native-compile');output.mkdir(parents=True,exist_ok=True)
commands=[
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
    'target/java/src/dfyconfig.toml','--output',str((output/'worker-java/B3').resolve())]),
  ('host',['bash','Source/DafnyB3Host.Test/run-tests.sh',worker,solver]),
  ('corpus',['python3','Scripts/check-b3-integration.py','Binaries/net8.0/Dafny.dll','build/b3-host-tests/package/DafnyB3Host.dll',solver,'--output',str(output/'corpus')]),
  ('language-server',['env','DAFNY_TEST_SOLVER_PATH='+solver,'dotnet','test','Source/DafnyLanguageServer.Test/DafnyLanguageServer.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~B3CacheVerificationTest|FullyQualifiedName~B3ProjectMigrationTest|FullyQualifiedName~IdeStateObserverRetirementTest|FullyQualifiedName~CounterExampleCapabilityTest|FullyQualifiedName~ProjectManagerDatabaseTest|FullyQualifiedName~ProjectFilesTest|FullyQualifiedName~MultipleFilesProjectTest|FullyQualifiedName~CompetingProjectFilesTest|FullyQualifiedName~AdditionalAxiomsTest|FullyQualifiedName~CounterexamplesStillWorksIfNothingHasBeenVerified','--results-directory',str(output/'language-server'),'--logger','trx;LogFileName=result.trx','--nologo']),
  ('regressions',['dotnet','test','Source/IntegrationTests/IntegrationTests.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','DisplayName~git-issue-118.dfy|DisplayName~git-issue-120.dfy|DisplayName~git-issue-126.dfy|DisplayName~git-issue-126-capabilities.dfy','--results-directory',str(output/'regressions'),'--logger','trx;LogFileName=result.trx','--nologo']),
  ('map-theory',['dotnet','run','--project','Source/DafnyB3MapTheory.TestRunner','-c','Release','--','--worker',str(pathlib.Path('build/b3-host-tests/package/DafnyB3Host.dll').resolve()),'--solver',solver,'--solver-sha256','PINNED_SOLVER_DIGEST','--emit-directory',str(output/'map-requests')])]
expected_tests={'contracts':26,'normalizer':183,'protocol':79,'language-server':40,'regressions':4}
proof_receipt=None
results=[]
def validate(name):
 global proof_receipt
 if name in expected_tests:
  tests=ET.parse(output/name/'result.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
  assert len(tests)==expected_tests[name] and all(t.get('outcome')=='Passed' for t in tests), 'Incomplete '+name+' denominator'
 elif name=='worker-bootstrap':
  rows=list(csv.DictReader((output/'worker/library/resources.csv').open()))
  assert rows and all(r['TestResult.Outcome']=='Passed' for r in rows), 'Incomplete library proof receipt'
  verified=re.findall(r'Dafny program verifier finished with (\d+) verified, 0 errors', (output/'worker-bootstrap.txt').read_text())
  assert len(verified)==1 and int(verified[0])==len(rows), 'Library proof count differs from verifier output'
  proof_receipt={'sourceManifestSha256':hashlib.sha256(pathlib.Path('ThirdParty/B3/source-manifest.json').read_bytes()).hexdigest(),
    'librarySha256':hashlib.sha256((output/'worker/library/B3Library.dll').read_bytes()).hexdigest(),
    'batchCount':len(rows),'resourceCount':sum(int(r['TestResult.ResourceCount']) for r in rows),
    'maximumBatchResources':max(int(r['TestResult.ResourceCount']) for r in rows)}
 elif name=='worker-runtime':
  text=(output/'worker-runtime.txt').read_text()
  assert len(re.findall(r'PASSED$',text,re.M))==27 and not re.search(r'FAILED|HALT',text), 'Expected all 27 runtime controls'
 elif name=='worker-java':
  archive=output/'worker-java/B3.jar'
  assert archive.is_file() and archive.stat().st_size>0, 'Java compiler produced no jar'
 elif name=='host':
  totals=re.findall(r'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)',(output/'host.txt').read_text())
  assert totals==[('0','79','0','79'),('0','35','0','35')], 'Expected complete protocol and host checks'
 elif name=='corpus':
  rows=json.loads((output/'corpus/summary.json').read_text())['results']
  assert len(rows)==46 and all(r['passed'] for r in rows), 'Expected 46 actual corpus controls'
 elif name=='map-theory':
  rows=[json.loads(line) for line in (output/'map-theory.txt').read_text().splitlines() if line.startswith('{')]
  cases=[r for r in rows if r.get('kind')=='map-theory-case']
  assert len(cases)==17 and all(r['matched'] for r in cases), 'Expected 17 exact map controls'
  assert rows[-1]=={'kind':'map-theory-summary','matched':17,'total':17,'passed':True}
for name,command in commands:
 with (output/(name+'.txt')).open('w') as log:
  try:
   if name == 'map-theory':
    command[command.index('PINNED_SOLVER_DIGEST')]=hashlib.sha256(pathlib.Path(solver).read_bytes()).hexdigest()
   result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=1800,cwd="ThirdParty/B3" if name in {"worker-runtime","worker-java"} else None)
   code=result.returncode
   if code == 0:
    log.flush()
    validate(name)
  except subprocess.TimeoutExpired:
   log.write('Stage exceeded its safety timeout\n');code=124
  except Exception as error:
   log.write(type(error).__name__+': '+str(error)+'\n');code=1
 results.append({'stage':name,'exitCode':code,'command':command})
 print(name,code,flush=True)
 if code!=0:
  print((output/(name+'.txt')).read_text()[-12000:],flush=True)
  # These terminal checks share only successfully built inputs; record every independent verdict.
  if name not in {'worker-java','corpus','language-server','regressions','map-theory'}:
   break
passed=len(results)==len(commands) and all(r['exitCode']==0 for r in results)
(output/'summary.json').write_text(json.dumps({'passed':passed,'fullGate':full_gate,'head':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'stages':results,'libraryProof':proof_receipt,'expectedTestCounts':expected_tests},indent=2)+'\n')
with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
 summary.write('B3 native compilation probe: '+('PASS' if passed else 'NOT GREEN')+'\n\n')
 for result in results: summary.write('- '+result['stage']+': exit '+str(result['exitCode'])+'\n')
 summary.write('\nThis scratch probe records expected failures and exits zero. Inspect summary.json; job success alone is not acceptance. ' + ('The full gate verifies the pinned B3 library and runs the real Dafny corpus with Z3 5.1.0.' if full_gate else 'No Dafny verifier workload runs in this probe.') + '\n')
