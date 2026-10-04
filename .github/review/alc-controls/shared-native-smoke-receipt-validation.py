"""Read-only strict six-case inspector. It never executes a solver or rewrites SMT."""
import hashlib
import json
from collections import Counter
from pathlib import Path
from urllib.parse import unquote, urlsplit

NAMES = ['baseline/true','candidate/true','baseline/reachable-false','candidate/reachable-false','baseline/fuel','candidate/fuel']
FIXTURES = {
 'true': ('true.dfy','NativeTrue','bf2c7f7498a9221291dbc5244c3725f0fb9af93b741638595bdc11f3b83d24e6',0),
 'reachable-false': ('false.dfy','NativeFalse','a50e9615c8b49eefe55a458f6f4df613f11164406074b9a011f7f389aad2dc8e',4),
 'fuel': ('fuel.dfy','NativeFuel','55a2d36861839f955e222ab8b2ae6f838645ef013e8610b588f391bbe61c3206',0)}
SOLVER_SHA = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
CSV_HEADER = 'TestResult.DisplayName,TestResult.Outcome,TestResult.Duration,TestResult.ResourceCount,RandomSeed'


def read(path, bound=8388608):
    path=Path(path)
    assert path.is_file() and not path.is_symlink()
    with path.open('rb') as f: value=f.read(bound+1)
    assert len(value)<=bound
    return value


def sha(path): return hashlib.sha256(read(path,268435456)).hexdigest()
def digest(value): return hashlib.sha256(value).hexdigest()
def load(path,bound=8388608): return json.loads(read(path,bound))


def smt_forms(data):
    # Structural framing retains original byte offsets. No parsed form is emitted.
    data.decode('utf-8',errors='strict')
    index=0; values=0
    def space():
        nonlocal index
        while index<len(data):
            if data[index] in [9,10,13,32]: index+=1; continue
            if data[index]==59:
                while index<len(data) and data[index]!=10: index+=1
                continue
            break
    def value(depth):
        nonlocal index,values
        values+=1
        assert depth<=512 and values<=2000000 and index<len(data)
        start=index
        if data[index]==40:
            index+=1; count=0; first=None; argument=None; space()
            while index<len(data) and data[index]!=41:
                item=value(depth+1)
                if count==0:first=item
                if count==1:argument=item
                count+=1;space()
            assert index<len(data) and data[index]==41
            index+=1
            return {'start':start,'end':index,'list':True,'atom':None,
                    'head':None if first is None else first['atom'],
                    'arg':None if argument is None else argument['atom'],
                    'arghead':None if argument is None else argument['head']}
        assert data[index]!=41
        if data[index] in [34,124]:
            quote=data[index];index+=1;ended=False
            while index<len(data):
                b=data[index];index+=1
                if b!=quote:continue
                if quote==34 and index<len(data) and data[index]==quote:index+=1;continue
                ended=True;break
            assert ended
        else:
            while index<len(data) and data[index] not in [9,10,13,32,40,41,59]:index+=1
        assert 0<index-start<=65536
        return {'start':start,'end':index,'list':False,'atom':data[start:index].decode('utf-8'),
                'head':None,'arg':None,'arghead':None}
    result=[];space()
    while index<len(data):
        assert len(result)<100000
        result.append(value(0));space()
    return result


def inspect_transcript(input_bytes,output_bytes):
    commands=smt_forms(input_bytes);replies=smt_forms(output_bytes)
    assert commands and all(c['list'] for c in commands)
    assert not any(r['head']=='error' or r['atom'] in ['unknown','unsupported'] for r in replies)
    pairs=[r for r in replies if r['atom'] in ['sat','unsat'] or r['head']==':rlimit']
    assert pairs and len(pairs)%2==0
    results=[];resources=[]
    for i in range(0,len(pairs),2):
        verdict,count=pairs[i:i+2]
        assert not verdict['list'] and verdict['atom'] in ['sat','unsat']
        assert count['list'] and count['head']==':rlimit' and count['arg'].isascii() and count['arg'].isdigit()
        resource=int(count['arg']);assert 0<=resource<=9223372036854775807
        results.append(verdict['atom']);resources.append(resource)
    checks=[];groups=[];reset=0;prefix=0;first=0;assertion=None
    def finish():
        nonlocal first
        group=checks[first:]
        if not group:return
        assert reset>0 and (group[0]['reply']=='sat' or len(group)==1)
        assert all(c['reply']=='sat' for c in group[:-1])
        groups.append({'resetOrdinal':reset,'outcome':'Invalid' if group[0]['reply']=='sat' else 'Valid',
          'finalRawResourceReply':group[-1]['resourceReply'],'initialNegatedVcSha256':group[0]['latestNegatedAssertionSha256'],
          'checkIndices':[c['index'] for c in group]})
        first=len(checks)
    for i,c in enumerate(commands):
        if c['head']=='reset':finish();reset+=1;prefix=c['start'];assertion=None
        if c['head']=='assert' and c['arghead']=='not':assertion=digest(input_bytes[c['start']:c['end']])
        if c['head'] in ['check-sat','check-sat-assuming']:
            assert c['head']=='check-sat' and assertion and reset>0 and i+3<len(commands)
            assert [(x['head'],x['arg']) for x in commands[i+1:i+4]]==[
                ('get-info',':name'),('get-info',':rlimit'),('get-info',':name')]
            ordinal=len(checks);assert ordinal<len(results) and ordinal<len(resources)
            checks.append({'index':ordinal,'commandIndex':i,'reply':results[ordinal],'resourceReply':resources[ordinal],
               'latestNegatedAssertionSha256':assertion,'exactQueryPrefixSha256':digest(input_bytes[prefix:c['end']])})
    finish()
    assert checks and len(checks)==len(results)==len(resources)
    assert sum(c['head']=='get-info' and c['arg']==':rlimit' for c in commands)==len(resources)
    assert [i for g in groups for i in g['checkIndices']]==[c['index'] for c in checks]
    return len(commands),reset,[input_bytes[c['start']:c['end']].decode('utf-8') for c in commands if c['head']=='set-option'],checks,groups


def same_source(logged,source):
    uri=urlsplit(logged)
    if uri.scheme=='file':
        assert uri.netloc in ['', 'localhost']
        logged=unquote(uri.path)
    if not Path(logged).is_absolute():return logged==Path(source).name
    return Path(logged)==Path(source)


def inspect_common(controls,qualified,constants):
    common=controls['commonClosure'];products=controls['products']
    assert common['scope']=='exact-common-managed-inventory-with-explicit-unavailable-metadata-and-runtime-denial'
    assert common['completeMetadataInventory'] is True and common['completeMetadataAvailability'] is False
    assert common['sameAssemblyObjectsRequired'] is True and common['ordinaryPackageResolverSelectionsRequired'] is True
    assert common['persistentBoogieState'] is True and common['freshBoogieStateEstablished'] is False
    assert common['nativeQueryOrCostParityEstablished'] is False
    assert common['requiredLaterControls']==['repeat','interleaved','reversed-order']
    assert common['runtimeDemandState']=={'poisoned':False,'failures':[],'unavailableDemands':[]}
    assert products==qualified['products']
    assert common['frameworkFiles']==qualified['commonClosure']['frameworkFiles']
    assert common['commonFiles']==qualified['commonClosure']['commonFiles']
    assert common['unavailableMetadataReferences']==qualified['commonClosure']['unavailableMetadataReferences']
    assert common['metadataReferences']==qualified['commonClosure']['metadataReferences']
    assert common['metadataOwners']==qualified['commonClosure']['metadataOwners']
    assert common['packageMetadataCatalogs']==qualified['commonClosure']['packageMetadataCatalogs']
    assert common['roots']==sorted(constants.BOOGIE_ROOTS)
    product_map={p['label']:p for p in products}
    inventories={}
    for p in products:
        files={f['path']:f for f in p['files']}
        assert len(files)==len(p['files'])==(329 if p['label']=='baseline' else 335)
        base=Path(p['directory']);actual=set()
        for file in base.rglob('*'):
            assert not file.is_symlink()
            if file.is_file():
                rel=file.relative_to(base).as_posix();assert rel in files
                assert file.stat().st_size==files[rel]['bytes'] and sha(file)==files[rel]['sha256']
                actual.add(rel)
        assert actual==set(files);inventories[p['label']]=files
    shared={f['name']:f for f in common['commonFiles']};framework={f['name']:f for f in common['frameworkFiles']}
    assert len(shared)==18 and len(framework)==168 and set(shared).isdisjoint(framework)
    assert constants.UNAVAILABLE_NAME not in shared and constants.UNAVAILABLE_NAME not in framework
    for pin in list(shared.values())+list(framework.values()):
        assert Path(pin['path']).stat().st_size==pin['bytes'] and sha(pin['path'])==pin['sha256']
    phases=['initial-framework-only','complete-metadata-inventory-before-common-load','common-closure-preloaded-before-product-load']
    for ordinal,name in enumerate(NAMES,1):
        product=name.split('/')[0]
        phases+=['before-native-run-'+product+'-'+str(ordinal),'private-context-final-loader-audit-'+product,
                 'after-weak-collection-'+product+'-'+str(ordinal),'after-native-evidence-'+name]
    phases+=['fixed-sequence-complete']
    assert len(phases)==28 and [a['phase'] for a in common['defaultAudits']]==phases
    slots={};prior=set()
    for ordinal,audit in enumerate(common['defaultAudits']):
        current=set();simple_names=set();commons=set()
        for e in audit['assemblies']:
            slot=e['objectSlot'];simple=e['identity'].split(',')[0]
            assert type(slot) is int and slot>0 and slot not in current and simple not in simple_names
            current.add(slot);simple_names.add(simple)
            assert not simple.lower().startswith('dafny') and simple!=constants.UNAVAILABLE_NAME
            fact=(e['kind'],e['identity'],e['path'],e['sha256'])
            assert slot not in slots or slots[slot]==fact;slots[slot]=fact
            if e['kind']=='harness':
                assert simple=='B3AlcGate' and e['sha256']==controls['harnessAssemblySha256']
                assert e['path']==controls['nonproofQualification']['currentBuiltHarnessFiles']['B3AlcGate.dll']['path']
            else:
                assert e['kind'] in ['shared-common','shared-tpa']
                pin=(shared if e['kind']=='shared-common' else framework)[simple]
                assert fact[1:]==(pin['identity'],pin['path'],pin['sha256'])
                if e['kind']=='shared-common':commons.add(simple)
        assert prior.issubset(current) and commons==(set() if ordinal<2 else set(shared));prior=current
        assert sum(e['kind']=='harness' for e in audit['assemblies'])==1
        private=ordinal>=3 and ordinal<27 and (ordinal-3)%4==1
        assert [c['kind'] for c in audit['contexts']]==(['default','owned-private'] if private else ['default'])
        for c in audit['contexts']:
            assert c['isCollectible']==(c['kind']=='owned-private')
            if c['kind']=='default':assert c['name']=='Default'
        if private:
            assert audit['contexts'][1]['name']=='b3-shared-common-native-run-'+str((ordinal-3)//4+1)
    events=common['runtimeAssemblyLoads']
    assert 13<=len(events)<=1024 and [e['sequence'] for e in events]==list(range(1,len(events)+1))
    for event in events:
        simple=event['identity'].split(',')[0]
        assert event['validated'] is True and event['failure'] is None and simple!=constants.UNAVAILABLE_NAME
        if event['kind']=='private-package':
            context=event['context'];assert context.startswith('b3-shared-common-native-run-')
            ordinal=int(context.removeprefix('b3-shared-common-native-run-'));assert 1<=ordinal<=6
            p=product_map[NAMES[ordinal-1].split('/')[0]]
            rel=Path(event['path']).relative_to(p['directory']).as_posix()
            assert inventories[p['label']][rel]['sha256']==event['sha256']
        else:
            assert event['kind'] in ['shared-common','shared-tpa'] and event['context']=='Default'
            pin=(shared if event['kind']=='shared-common' else framework)[simple]
            assert (event['identity'],event['path'],event['sha256'])==(pin['identity'],pin['path'],pin['sha256'])
    return product_map,shared,framework


def inspect_prerequisites(controls,outer,output):
    prior=output/'archived-candidate-input/out/b3-native-compile/previous-lifecycle/out/b3-native-compile'
    old_manifest=load(prior/'harness/source-manifest.json',131072)
    assert sha(prior/'harness/source-manifest.json')=='e25b41f0d0fb6c29708d64cccc5c4b945bd64f177054156db667688243cf9a42'
    assert sha(prior/'lifecycle.json')=='60992bd42a47fbc9e3fca7294253d182947dd8ddfd4977d76361818bac2576bd'
    old=load(prior/'lifecycle.json',1048576)
    assert old['passed'] is True and old['failureCode'] is None and old['noProductLoaded'] is True
    assert old['effectiveUid']==0 and old['delegatedPidCeiling']==300 and old['solverSha256']==SOLVER_SHA
    assert len(old['controls'])==11 and all(c['passed'] is True and c['zeroOwnedMembers'] is True for c in old['controls'])
    p=controls['prerequisites'];old_files=old_manifest['files']
    current_manifest=load(outer['harnessBundlePins']['source-manifest.json']['path'],262144)
    current={f['path']:f for f in current_manifest['files']}
    assert len(old_files)==19 and p['priorLifecycleSourceFiles']==sorted(old_files,key=lambda f:f['path'])
    assert all(current[f['path']]==f for f in old_files)
    assert p['lifecycleReceiptSha256']==sha(prior/'lifecycle.json')
    assert p['lifecycleSourceManifestSha256']==sha(prior/'harness/source-manifest.json')
    assert p['priorLifecycleHarnessAssemblySha256']==old['harnessAssemblySha256']=='ec2e3910a0cdc4871a1c1c52b540c6e302b09fd83d1e47e509ab1d91855db680'
    components=['NativeProofSupervisor.cs','native-solver-wrapper.py','NativeLifecycleControls.cs',
                'lifecycle-control-fixtures.json','lifecycle-seal-probe.py','native-lifecycle-fixture.c']
    assert p['matchedComponents']=={name:current[name]['sha256'] for name in components}
    assert p['evidenceKind']=='exact-image-and-public-source-inspection-not-signed-origin'
    origin=load('.github/review/alc-controls/solver-origin-technical-evidence.json',65536)
    assert p['solverOriginEvidenceSha256']==sha('.github/review/alc-controls/solver-origin-technical-evidence.json')=='a64314c9ee010e48e19bdf9ede2ae6260b2dfbd857540a9b1c06010c5e6638af'
    assert p['solverOriginEvidence']==origin and origin['solverSha256']==SOLVER_SHA and origin['permittedArguments']==[['-version'],['-smt2','-in']]
    image=p['actualSolverImageInspection']
    assert image['sha256']==SOLVER_SHA and image['bytes']==(output/'shared-z3').stat().st_size
    assert all(image[k]==v for k,v in origin['expectedActualImageInspection'].items())


def inspect_proofs(controls,outer,qualified,constants,output):
    assert controls['schemaVersion']==2 and controls['scope']=='prototype/six-fixed-qualified-shared-native-proof-smoke-controls'
    assert controls['passed'] is True and controls['failureCode'] is None and controls['effectiveUid']==0
    assert controls['ordinaryProofCliEnabled'] is False and controls['nativeQueryOrResourceParityEstablished'] is False
    assert controls['persistentBoogieState'] is True and controls['freshBoogieStateEstablished'] is False
    assert controls['requiredLaterControls']==['repeat','interleaved','reversed-order']
    assert controls['onlyDefaultContextRemains'] is True and controls['remainingDirectChildren']==[]
    assert controls['sourceManifestSha256']==outer['sourceManifestSha256']
    assert controls['harnessAssemblySha256']==outer['harnessBundlePins']['B3AlcGate.dll']['sha256']
    assert controls['harnessAssemblySha256']!='f769dc26690a4175706a9758332b56438c4da32e690b3ed09fcef342e4678139'
    assert controls['solverSha256']==outer['solverSha256']==SOLVER_SHA and controls['solverVersion']=='5.1.0'
    assert controls['smokeBudgets']=={'cores':1,'verificationTimeLimitSeconds':20,'resourceLimit':200000,'invocationSafetySeconds':60}
    assert controls['fixedCaseOrder']==NAMES and [c['name'] for c in controls['runs']]==NAMES
    assert set(outer['harnessBundlePins'])=={'B3AlcGate.dll','B3AlcGate.deps.json','B3AlcGate.runtimeconfig.json','source-manifest.json'}
    for name,pin in outer['harnessBundlePins'].items():
        assert sha(pin['path'])==pin['sha256'] and Path(pin['path']).stat().st_size==pin['bytes']
    q=controls['nonproofQualification']
    assert q['currentBuildRehashedAtEveryBoundary'] is True
    assert q['currentBuiltHarnessFiles']==outer['harnessBundlePins']
    assert q=={'publicRun':37238572256,'sourceCommit':'71fc9dcff677f80d43ef3cb7c767806513c438a4','artifactId':11316129057,
      'zipSha256':'4f4b07ddac0ca8d5d8876a349ce6871d12d0dcf774c757971dabe29bf8ce7a29','zipBytes':80709394,
      'summarySha256':'74950dca1707fc774add3ee067ea4939ec76dfeb16c1edb2fa1f5af894b8688b',
      'sourceManifestSha256':'4230adf2ba624d42574d59ed95d4af1e28346d3260ffb79bf4249f1c80097a05',
      'coordinatorManifestSha256':'383c8bffa310bf0ede4322e8679fa7026cf3e3733504404ee987d08f1d262b81',
      'receiptSha256':['25205d9db1cf088d937a374ba8044725cfc82d3f886ccb3de9a009375f24b0b1','aca632876dbb7b9b05e5625b122d93d5982c4d40954bc3513673cabbf1d7cadc','09ddb7180ba82ce709cffc64da49c7fa62d872226cca0bfb6256e0493cf8546b'],
      'originalQualifiedSourceFilesUnchanged':36,'originalLifecycleSourceFilesUnchanged':19,
      'exact168ActualTpaFilesRehashed':True,'oldAssemblyObjectsOrSeedsComparedAcrossHosts':False,
      'qualifiedControls':constants.NAMES,'nativeProofPreviouslyEstablished':False,
      'currentBuiltHarnessFiles':outer['harnessBundlePins'],'currentBuildRehashedAtEveryBoundary':True}
    inspect_prerequisites(controls,outer,output)
    product_map,shared,framework=inspect_common(controls,qualified,constants)
    assert controls['commonFramework']['runtime']=='.NET 8.0.31'
    all_groups=[]
    for ordinal,case in enumerate(controls['runs'],1):
        product_name,fixture_name=case['name'].split('/')
        filename,routine,fixture_sha,expected_exit=FIXTURES[fixture_name]
        run,proof=case['run'],case['evidence'];p=product_map[product_name]
        assert case['passed'] is True and case['failureCode'] is None and case['product']==product_name
        assert case['fixture']=='proof-fixtures/'+filename and case['fixtureSha256']==fixture_sha
        assert run['failure'] is None and run['contextCollected'] is True and run['remainingDirectChildren']==[]
        assert run['exitCode']==case['expectedExitCode']==expected_exit and run['product']==product_name
        assert run['schedulerCleanup']=='LargeThreadScheduler.Dispose called; actual collection checked after leaving the frame'
        args=run['arguments'];source=Path(args[1])
        folder=Path(controls['evidenceRoot'])/case['name'].replace('/','-')
        assert args==['verify',str(source),'--cores','1','--verification-time-limit','20','--resource-limit','200000',
          '--solver-path',args[9],'--log-format','json;LogFileName='+str(folder/'batches.json'),
          '--log-format','csv;LogFileName='+str(folder/'batches.csv')]
        assert source==Path(outer['harnessBundlePins']['B3AlcGate.dll']['path']).parent/'proof-fixtures'/filename
        assert sha(source)==fixture_sha
        core=[e for e in run['loaderLedger'] if e['kind']=='private-loaded' and e['identity']==constants.CORE_IDENTITY]
        assert len(core)==1 and core[0]['sha256']==p['coreSha256'] and core[0]['informationalVersion']==p['coreInformationalVersion']
        assert any(e['kind']=='shared-common' and e['identity'].startswith('Boogie.') for e in run['loaderLedger'])
        assert any(e['kind']=='shared-common-loaded' and e['identity'].startswith('Boogie.Provers.SMTLib,') for e in run['loaderLedger'])
        for e in run['loaderLedger']:
            if e['kind'] in ['shared-common','shared-common-loaded','shared-tpa']:
                pin=(shared if e['kind'].startswith('shared-common') else framework)[e['identity'].split(',')[0]]
                assert (e['identity'],e['path'],e['sha256'])==(pin['identity'],pin['path'],pin['sha256'])
                assert e['context']=='Default'
            elif e['kind'].startswith('private-') or e['kind'].startswith('resolver-'):
                assert e['context']=='b3-shared-common-native-run-'+str(ordinal)
                rel=Path(e['path']).relative_to(p['directory']).as_posix()
                pin=next(f for f in p['files'] if f['path']==rel);assert e['sha256']==pin['sha256']
            else:raise AssertionError('Unknown private loader ledger kind')
        batches=[];raw_json=read(folder/'batches.json',4194304)
        for scope in json.loads(raw_json)['verificationResults']:
            assert scope['outcome'] in ['Correct','Errors'] and 'traversalCompleted' not in scope
            assert scope['vcResults'] and len(scope['vcResults'])<=64
            for b in scope['vcResults']:
                assert b['outcome'] in ['Valid','Invalid'] and 'workId' not in b
                assertions=[{'filename':a['filename'],'line':a['line'],'column':a['col'],'description':a['description']} for a in b['assertions']]
                assert assertions and len(assertions)<=256 and all(a['line']>0 and a['column']>=0 and same_source(a['filename'],str(source)) for a in assertions)
                assert type(b['resourceCount']) is int and 0<=b['resourceCount']<=9223372036854775807
                batches.append({'scope':scope['name'],'vcNum':b['vcNum'],'outcome':b['outcome'],'resourceCount':b['resourceCount'],'assertions':assertions})
            assert (scope['outcome']=='Errors')==any(b['outcome']=='Invalid' for b in scope['vcResults'])
        assert 0<len(batches)<=256 and len({(b['scope'],b['vcNum']) for b in batches})==len(batches)
        assert batches==proof['batches'] and proof['jsonSha256']==digest(raw_json)
        assert proof['assertionCount']==sum(len(b['assertions']) for b in batches)>0
        assert proof['targetRoutineBatchCount']==sum(routine in b['scope'] for b in batches)>0
        if expected_exit==0:assert all(b['outcome']=='Valid' for b in batches)
        else:
            assert 'assertion might not hold' in run['output']+run['errorOutput']
            assert any(routine in b['scope'] and b['outcome']=='Invalid' and any(a['line']==2 for a in b['assertions']) for b in batches)
            assert all(routine in b['scope'] for b in batches if b['outcome']=='Invalid')
        raw_csv=read(folder/'batches.csv',1048576);lines=raw_csv.decode('utf-8').splitlines()
        assert lines[0]==CSV_HEADER and len(lines)==len(batches)+1 and digest(raw_csv)==proof['csvSha256']
        rows=[]
        for line in lines[1:]:
            fields=line.split(',');assert len(fields)==5 and fields[0] and fields[1] in ['Passed','Failed'] and fields[3].isascii() and fields[3].isdigit()
            rows.append({'displayName':fields[0],'outcome':fields[1],'duration':fields[2],'resourceCount':int(fields[3]),'randomSeed':fields[4]})
        assert rows==proof['csvRows']
        assert Counter((r['outcome'],r['resourceCount']) for r in rows)==Counter(('Passed' if b['outcome']=='Valid' else 'Failed',b['resourceCount']) for b in batches)
        cleanup=run['proofCleanup'];path,digest_expected=cleanup['evidence'].rsplit(' sha256=',1);path=Path(path)
        assert path.resolve().is_relative_to(output.resolve()) and path.name=='cleanup.json'
        assert sha(path)==digest_expected==proof['cleanupSha256']
        data=load(path,4194304)
        assert cleanup['liveOwnedProcesses']==data['liveOwnedProcesses']==0 and data['populated'] is False and data['finalMembers']==[]
        assert all(data[k] is True for k in ['leafRemoved','monitorStopped','admissionClosed'])
        assert data['fallbackRequests']==[] and data['failures']==[] and data['liveCapObservation'] is None
        assert 0<len(data['launches'])==len(data['streamReceipts'])==len(proof['solverLaunches'])==cleanup['recordedSolverGroups']<=64
        config_bytes=read(path.parent/'config.json',65536);config=json.loads(config_bytes)
        assert config['token']==data['token'] and len(data['token'])==64 and all(c in '0123456789abcdef' for c in data['token'])
        assert config['product']==product_name and config['arguments']==args and config['solverSha256']==SOLVER_SHA
        assert Path(config['realSolver']).resolve()==(output/'shared-z3').resolve() and sha(config['realSolver'])==SOLVER_SHA
        assert config['leaf'].startswith('/sys/fs/cgroup/b3-alc-ci-') and Path(config['leaf']).name.startswith('alc-')
        assert not Path(config['leaf']).exists()
        host_stage=next(stage for stage in outer['stages'] if stage['stage']=='fixed-six-shared-native-smoke')
        assert host_stage['passed'] is True and host_stage['exitCode']==0 and host_stage['poisoned'] is False
        assert host_stage['signals']==[] and host_stage['signalCount']==0 and host_stage['failures']==[] and host_stage['remainingDirectChildren']==[]
        assert all(p['exitObserved'] is True and p['reaped'] is True for p in host_stage['ownedProcesses'])
        assert any(p['identity']==config['host'] for p in host_stage['ownedProcesses'])
        template=read(Path(outer['harnessBundlePins']['B3AlcGate.dll']['path']).parent/'native-solver-wrapper.py',65536)
        assert config['templateDigest']==digest(template)
        assert read(args[9],65536)==template.replace(b'__PYTHON_EXECUTABLE__',config['python'].encode('utf-8'))
        assert config['wrapperDigest']==sha(args[9]) and sha(config['python'])==config['pythonSha256']
        assert read(path.parent/'closed',128).decode('ascii')==data['token']
        assert not list(path.parent.glob('wrapper-failure-*.json')) and not (path.parent/'dispose-cleanup.json').exists()
        processes={(e['identity']['pid'],e['identity']['startTime']):e for e in data['processes']}
        assert len(processes)==len(data['processes']) and 0<len(processes)<=256
        for process in processes.values():
            identity=process['identity']
            assert set(identity)=={'pid','startTime','parent','group','session'}
            assert all(type(identity[k]) is int and identity[k]>0 for k in identity)
            assert process['kind'] in ['wrapper','solver']

        signal_keys=set()
        for signal in data['signalRequests']:
            key=(signal['identity']['pid'],signal['identity']['startTime'])
            assert processes[key]['identity']==signal['identity'] and processes[key]['kind']==signal['kind']
            assert signal['signal'] in [9,15] and type(signal['delivered']) is bool
            signal_key=key+(signal['signal'],);assert signal_key not in signal_keys;signal_keys.add(signal_key)
            assert signal['phase']=='after-cli-completion-or-recorded-invocation-failure'
        versions=0;case_groups=[];checks_count=0;seen_launches=set();owned_keys=set();ownership_receipts=[]
        for stream,launch_evidence in zip(data['streamReceipts'],proof['solverLaunches']):
            launch=stream['launch'];assert len(launch)==32 and all(c in '0123456789abcdef' for c in launch) and launch not in seen_launches
            seen_launches.add(launch);base=path.parent/'launches'/launch
            assert stream['valid'] is True and stream['headerValid'] is True and launch_evidence['launch']==launch
            complete_bytes=read(base/'complete.json',16384);complete=json.loads(complete_bytes)
            assert digest(complete_bytes)==stream['completionSha256']==launch_evidence['completionSha256']
            assert len(complete_bytes)==stream['completionBytes'] and complete['errors']==[] and complete['token']==data['token']
            ready_bytes=read(base/'ready.json',16384);ready=json.loads(ready_bytes)
            assert ready['token']==data['token'] and ready['solverSha256']==SOLVER_SHA
            assert read(base/'admitted',128).decode('ascii')==data['token']
            owned=[l for l in data['launches'] if l['wrapper']['pid']==ready['wrapper']['pid'] and l['wrapper']['startTime']==ready['wrapper']['startTime']]
            assert len(owned)==1;owned=owned[0]
            assert owned==ready and owned['solverSha256']==SOLVER_SHA and owned['arguments']==launch_evidence['arguments']
            wrapper,solver=owned['wrapper'],owned['solver']
            assert wrapper['parent']==config['host']['pid'] and wrapper['startTime']>=config['host']['startTime']
            assert wrapper['group']==wrapper['session']==wrapper['pid']
            assert solver['parent']==wrapper['pid'] and solver['startTime']>=wrapper['startTime']
            assert solver['group']==solver['session']==wrapper['pid']
            for identity,kind in [(wrapper,'wrapper'),(solver,'solver')]:
                key=(identity['pid'],identity['startTime'])
                assert key not in owned_keys and processes[key]=={'identity':identity,'kind':kind}
                owned_keys.add(key)
            ownership_receipts.append({'launch':launch,'readySha256':digest(ready_bytes),'wrapper':wrapper,'solver':solver,
                'validatedPidfdCaptureSource':'NativeProofSupervisor.cs/Track-before-atomic-admission',
                'configSha256':digest(config_bytes),'host':config['host']})
            image=owned['solverImage']
            assert image==complete['solverImage'] and image['sha256']==SOLVER_SHA and 0<image['bytes']<=268435456
            assert image['requiredSeals']==15 and image['seals']&15==15 and image['nativeElf'] is True and image['executableMemfd'] is True
            streams={}
            for bound in stream['streams']:
                name=bound['name'];assert name in ['stdin','stdout','stderr'] and name not in streams
                value=read(base/(name+'.bin'));captured=complete['streams'][name]
                assert bound['valid'] is True and digest(value)==bound['sha256']==captured['readSha256']==captured['writtenSha256']
                assert len(value)==bound['bytes']==captured['readBytes']==captured['writtenBytes']
                if name!='stdin':assert captured['end']=='eof'
                streams[name]=value
            assert set(streams)=={'stdin','stdout','stderr'} and streams['stderr']==b''
            for name,prefix in [('stdin','input'),('stdout','output'),('stderr','error')]:
                assert launch_evidence[prefix+'Sha256']==digest(streams[name]) and launch_evidence[prefix+'Bytes']==len(streams[name])
            args=owned['arguments'];exit_code=complete['solverExitCode'];assert exit_code==launch_evidence['solverExitCode']
            if args==['-version']:
                assert exit_code==0 and streams['stdin']==b'' and streams['stdout'].decode('utf-8').rstrip('\r\n')=='Z3 version 5.1.0 - 64 bit'
                assert launch_evidence['checks']==launch_evidence['vcGroups']==launch_evidence['actualSetOptions']==[]
                assert launch_evidence['commandCount']==launch_evidence['resets']==0;versions+=1
            else:
                assert args==['-smt2','-in'] and exit_code in [0,-15,-9]
                command_count,resets,options,checks,groups=inspect_transcript(streams['stdin'],streams['stdout'])
                assert command_count==launch_evidence['commandCount'] and resets==launch_evidence['resets']
                assert options==launch_evidence['actualSetOptions'] and checks==launch_evidence['checks'] and groups==launch_evidence['vcGroups']
                checks_count+=len(checks);case_groups+=groups
        assert owned_keys==set(processes) and set(p.name for p in (path.parent/'launches').iterdir())==seen_launches
        assert versions==proof['versionProbeCount']>0 and checks_count==proof['proofCheckCount']>=len(case_groups)==proof['proofVcGroupCount']==len(batches)>0
        assert Counter((g['outcome'],g['finalRawResourceReply']) for g in case_groups)==Counter((b['outcome'],b['resourceCount']) for b in batches)
        assert proof['finalRawVcResourceRepliesMatchJsonMultiset'] is True and proof['nativeQueryOrCostParityClaimed'] is False
        assert proof['resourceCount']==sum(b['resourceCount'] for b in batches)>0
        all_groups.append({'name':case['name'],'batches':len(batches),'checks':checks_count,'finalRawResourceReplies':[g['finalRawResourceReply'] for g in case_groups],'ownedLaunchReceipts':ownership_receipts})
    return all_groups
