import json, os, pathlib, subprocess
output=pathlib.Path('out/b3-native-compile');output.mkdir(parents=True,exist_ok=True)
commands=[
 ('packages',['sh','Scripts/fetch-boogie-packages.sh']),
 ('compiler',['dotnet','build','Source/Dafny/Dafny.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--nologo']),
 ('contracts',['dotnet','test','Source/DafnyCore.Test/DafnyCore.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~VerificationContractsTest|FullyQualifiedName~B3BackendSelectionTest|FullyQualifiedName~B3WorkItemTest','--results-directory',str(output/'contracts'),'--logger','trx;LogFileName=result.trx','--nologo']),
 ('normalizer',['dotnet','test','Source/DafnyB3Normalizer.Test/DafnyB3Normalizer.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--results-directory',str(output/'normalizer'),'--logger','trx;LogFileName=result.trx','--nologo']),
 ('protocol',['dotnet','test','Source/DafnyB3Protocol.Test/DafnyB3Protocol.Test.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--results-directory',str(output/'protocol'),'--logger','trx;LogFileName=result.trx','--nologo']),
 ('terminator-control',['python3','.github/review/b3-newline-control.py'])]
results=[]
for name,command in commands:
 with (output/(name+'.txt')).open('w') as log:
  result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
 results.append({'stage':name,'exitCode':result.returncode,'command':command})
 print(name,result.returncode,flush=True)
 if result.returncode!=0:
  print((output/(name+'.txt')).read_text()[-12000:],flush=True)
  break
passed=len(results)==len(commands) and all(r['exitCode']==0 for r in results)
(output/'summary.json').write_text(json.dumps({'passed':passed,'stages':results},indent=2)+'\n')
with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as summary:
 summary.write('B3 native compilation probe: '+('PASS' if passed else 'NOT GREEN')+'\n\n')
 for result in results: summary.write('- '+result['stage']+': exit '+str(result['exitCode'])+'\n')
 summary.write('\nThis scratch probe records expected failures and exits zero. Inspect summary.json; job success alone is not acceptance. No Dafny verifier workload runs in this probe.\n')
