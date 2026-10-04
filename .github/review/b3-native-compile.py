import hashlib, json, os, pathlib, subprocess
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
  ('host',['bash','Source/DafnyB3Host.Test/run-tests.sh',worker,solver]),
  ('corpus',['python3','Scripts/check-b3-integration.py','Binaries/net8.0/Dafny.dll','build/b3-host-tests/package/DafnyB3Host.dll',solver,'--output',str(output/'corpus')]),
  ('language-server',['env','DAFNY_TEST_SOLVER_PATH='+solver,'dotnet','test','Source/DafnyLanguageServer.Test/DafnyLanguageServer.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~B3CacheVerificationTest|FullyQualifiedName~B3ProjectMigrationTest|FullyQualifiedName~IdeStateObserverRetirementTest|FullyQualifiedName~CounterExampleCapabilityTest|FullyQualifiedName~ProjectManagerDatabaseTest|FullyQualifiedName~ProjectFilesTest|FullyQualifiedName~MultipleFilesProjectTest|FullyQualifiedName~CompetingProjectFilesTest|FullyQualifiedName~AdditionalAxiomsTest|FullyQualifiedName~CounterexamplesStillWorksIfNothingHasBeenVerified','--results-directory',str(output/'language-server'),'--logger','trx;LogFileName=result.trx','--nologo']),
  ('regressions',['dotnet','test','Source/IntegrationTests/IntegrationTests.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~git-issue-118.dfy|FullyQualifiedName~git-issue-120.dfy|FullyQualifiedName~git-issue-126.dfy|FullyQualifiedName~git-issue-126-capabilities.dfy','--results-directory',str(output/'regressions'),'--logger','trx;LogFileName=result.trx','--nologo']),
  ('map-theory',['dotnet','run','--project','Source/DafnyB3MapTheory.TestRunner','-c','Release','--','--worker',str(pathlib.Path('build/b3-host-tests/package/DafnyB3Host.dll').resolve()),'--solver',solver,'--solver-sha256','PINNED_SOLVER_DIGEST','--emit-directory',str(output/'map-requests')])]
results=[]
for name,command in commands:
 with (output/(name+'.txt')).open('w') as log:
  try:
   if name == 'map-theory':
    command[command.index('PINNED_SOLVER_DIGEST')]=hashlib.sha256(pathlib.Path(solver).read_bytes()).hexdigest()
   result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=1800)
   code=result.returncode
   if code == 0 and name == 'regressions':
    tree=ET.parse(output/'regressions/result.trx')
    tests=tree.findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    if len(tests) != 4 or any(test.attrib['outcome'] != 'Passed' for test in tests):
     log.write('Expected four passing B3 and neutral integration regressions\n');code=1
  except subprocess.TimeoutExpired:
   log.write('Stage exceeded its safety timeout\n');code=124
 results.append({'stage':name,'exitCode':code,'command':command})
 print(name,code,flush=True)
 if code!=0:
  print((output/(name+'.txt')).read_text()[-12000:],flush=True)
  # These terminal checks share only successfully built inputs; record every independent verdict.
  if name not in {'corpus','language-server','regressions','map-theory'}:
   break
passed=len(results)==len(commands) and all(r['exitCode']==0 for r in results)
(output/'summary.json').write_text(json.dumps({'passed':passed,'fullGate':full_gate,'head':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'stages':results},indent=2)+'\n')
with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
 summary.write('B3 native compilation probe: '+('PASS' if passed else 'NOT GREEN')+'\n\n')
 for result in results: summary.write('- '+result['stage']+': exit '+str(result['exitCode'])+'\n')
 summary.write('\nThis scratch probe records expected failures and exits zero. Inspect summary.json; job success alone is not acceptance. ' + ('The full gate verifies the pinned B3 library and runs the real Dafny corpus with Z3 5.1.0.' if full_gate else 'No Dafny verifier workload runs in this probe.') + '\n')
