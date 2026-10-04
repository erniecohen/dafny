"""Proposed read-only inspector for fixed disposable nonproof control receipts.

No entrypoint/subprocess/write/solver operation. Coordinator source/package/build
pins and bounded outer-process lifetime remain independent required checks.
"""
from collections import Counter
from pathlib import Path

NAMES = ['default-unavailable-demand', 'private-unavailable-demand', 'reactive-event-unavailable-demand']
UNAVAILABLE_NAME = 'System.Runtime.InteropServices.WindowsRuntime'
UNAVAILABLE_IDENTITY = UNAVAILABLE_NAME + ', Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
OWNER_SHA256 = '19f0112cd1f5172ee2688e96ddd44ada39a4bb1cb2315a154b63e9064f6e3dc0'
DENIAL = 'declared-unavailable-assembly-demand-denied'
CORE_IDENTITY = 'DafnyCore, Version=4.11.0.0, Culture=neutral, PublicKeyToken=null'
TRIGGERS = {
    NAMES[0]: 'AssemblyLoadContext.Default.LoadFromAssemblyName(exact-unavailable-identity)',
    NAMES[1]: 'Owned SharedProductContext.LoadFromAssemblyName(exact-unavailable-identity)',
    NAMES[2]: 'System.Reactive.Linq.Observable.FromEventPattern(Type,string)',
}
BOOGIE_ROOTS = {
    'Boogie.AbstractInterpretation': '6c353eb690a35be73ec499c40535765b3a0e39320ece907a07a6c5fa38fa867c',
    'Boogie.BaseTypes': 'e77d97b447cc824edb938a769f6d6d043aae724db6fa4ab15da9b136cc19be7d',
    'Boogie.CodeContractsExtender': '2b261295b7d91f4ce1a126639f242cef34b0a7c1f7518d45f5b53a4cd6c81511',
    'Boogie.Concurrency': '3ef2f0a9472138be33da9b0df79c37d3420556b51138a844715b1c2f506801cc',
    'Boogie.Core': '0a3c1939aff2ec9f38012c83ed30aff32cbba634beb1c5397402dcfa443dbe0e',
    'Boogie.ExecutionEngine': '0fac5e00abadddccb6b1ce3feaebe0e15828593b98910c91e9fad726c4693182',
    'Boogie.Graph': 'b1ebd9c9c2cc4cea3e85c65ce8244a7f2d6d4424899b64cf979b901d3c8fcd44',
    'Boogie.Houdini': '87236e790306b0a1f14ffee7bffac02e664a34d4f2d51f37e34411fd6c4b621e',
    'Boogie.Model': '97a89874065fda019efb001c97a88d1d327552c0e60ecc7b9c83b32315c6582e',
    'Boogie.Provers.LeanAuto': '6672c832928406e2fb1bfd5939d528f0af8a53ec8fe6bfc4d35e00db0d652759',
    'Boogie.Provers.SMTLib': 'a97bf95e5002e50c1b85373be2da57c15c2413841bf62606ef6e95f8d4493824',
    'Boogie.VCExpr': 'cfb45433edfeff6beca744e51228a38935287aa6878b4062614270694fc26a0c',
    'Boogie.VCGeneration': '69154f9a1b3c8463be99ab9262187591e606373510cff7b64930674aecf2fc88',
}



def inspect_exception_chain(chain):
    assert isinstance(chain, dict) and chain['complete'] is True and chain['truncated'] is False
    assert chain['captureFailure'] is None and chain['rejectedAggregateInnerCount'] is None
    nodes = chain['nodes']
    assert isinstance(nodes, list) and 1 <= len(nodes) <= 8
    total = 0
    allowed = ['System.IO.FileNotFoundException', 'System.Reflection.TargetInvocationException', 'System.AggregateException']
    for depth, node in enumerate(nodes):
        terminal = depth == len(nodes) - 1
        assert node['depth'] == depth and type(node['depth']) is int
        assert node['knownFrameworkType'] is True and node['type'] in allowed
        assert type(node['hResult']) is int and -2147483648 <= node['hResult'] <= 2147483647
        assert node['innerDepth'] == (None if terminal else depth + 1)
        assert node['innerDepth'] is None or type(node['innerDepth']) is int
        aggregate = node['type'] == 'System.AggregateException'
        assert node['aggregateInnerCount'] == (1 if aggregate else None)
        assert node['aggregateInnerCount'] is None or type(node['aggregateInnerCount']) is int
        file_not_found = node['type'] == 'System.IO.FileNotFoundException'
        assert node['fileName'] == (UNAVAILABLE_IDENTITY if file_not_found else None)
        assert isinstance(node['type'], str) and isinstance(node['message'], str)
        type_bytes, message_bytes = len(node['type'].encode('utf-8')), len(node['message'].encode('utf-8'))
        file_bytes = 0 if node['fileName'] is None else len(node['fileName'].encode('utf-8'))
        assert type_bytes <= 512 and message_bytes <= 4096 and file_bytes <= 4096
        total += type_bytes + message_bytes + file_bytes
        assert total <= 65536
        if terminal:
            assert file_not_found and node['message'] == DENIAL
        else:
            assert node['message'] != DENIAL
    assert type(chain['textUtf8Bytes']) is int and chain['textUtf8Bytes'] == total


def inspect_unavailable_control(receipt, sha, expected_manifest):
    assert type(receipt['schemaVersion']) is int and receipt['schemaVersion'] == 2
    assert receipt['scope'] == 'prototype/fixed-disposable-nonproof-unavailable-metadata-denial-control'
    name = receipt['control']
    assert name in NAMES and receipt['expectedControlNames'] == NAMES
    assert receipt['passed'] and receipt['failure'] is None and receipt['hostMustTerminateAfterControl']
    assert not receipt['ordinaryProofCliEnabled'] and not receipt['nativeProofExecuted'] and not receipt['solverExecuted']
    assert not receipt['nativeQueryOrResourceParityEstablished']
    assert receipt['completeMetadataInventory'] and not receipt['completeMetadataAvailability']
    assert receipt['sourceManifestSha256'] == expected_manifest
    assert receipt['remainingDirectChildren'] == [] and receipt['onlyDefaultContextRemains']
    assert 0 <= receipt['stopwatchSafetyMilliseconds'] < 45000
    observation = receipt['observation']
    assert observation['denialExceptionObserved'] is True and observation['triggerApiInvoked'] is True and observation['contextCollected'] is True
    assert observation['triggerApi'] == TRIGGERS[name]
    assert isinstance(observation['exceptionSummary'], str) and len(observation['exceptionSummary']) <= 4096
    inspect_exception_chain(observation['exceptionChain'])
    assert observation['schedulerCleanup'].startswith('not initialized:')
    common = receipt['commonClosure']
    assert common['scope'] == 'exact-common-managed-inventory-with-explicit-unavailable-metadata-and-runtime-denial'
    assert common['completeMetadataInventory'] and not common['completeMetadataAvailability']
    assert common['sameAssemblyObjectsRequired'] and common['ordinaryPackageResolverSelectionsRequired']
    assert common['persistentBoogieState'] and not common['freshBoogieStateEstablished']
    assert not common['nativeQueryOrCostParityEstablished']
    assert common['requiredLaterControls'] == ['repeat', 'interleaved', 'reversed-order']
    assert common['roots'] == sorted(BOOGIE_ROOTS)
    state = common['runtimeDemandState']
    assert state['poisoned'] is True and state['failures'] == [DENIAL]
    assert len(state['unavailableDemands']) == 1
    demand = state['unavailableDemands'][0]
    assert demand['sequence'] == 1 and demand['denialCode'] == DENIAL and demand['exactDeclaredIdentity'] is True
    assert demand['requestedIdentity'] == UNAVAILABLE_IDENTITY and demand['metadataOwnerSha256'] == OWNER_SHA256
    assert demand['route'] == ('private-load' if name == NAMES[1] else 'default-resolving')
    assert demand['context'] == ('unavailable-private-negative-control' if name == NAMES[1] else 'Default')
    assert [p['label'] for p in receipt['products']] == ['baseline', 'candidate']
    products = {p['label']: p for p in receipt['products']}
    inventories = {label: {f['path']: f for f in product['files']} for label, product in products.items()}
    files = {p['name']: p for p in common['commonFiles']}
    framework = {p['name']: p for p in common['frameworkFiles']}
    assert len(files) == len(common['commonFiles']) and 13 <= len(files) <= 64
    assert len(framework) == len(common['frameworkFiles']) and 0 < len(framework) <= 512
    assert set(files).isdisjoint(framework) and UNAVAILABLE_NAME not in files and UNAVAILABLE_NAME not in framework
    base = Path(products['baseline']['directory'])
    for simple, pin in files.items():
        relative = Path(pin['path']).relative_to(base).as_posix()
        assert inventories['baseline'][relative] == inventories['candidate'][relative]
        assert pin['identity'].split(',')[0] == simple and not simple.lower().startswith('dafny')
        assert sha(Path(pin['path'])) == pin['sha256'] and Path(pin['path']).stat().st_size == pin['bytes']
        assert sha(Path(products['candidate']['directory']) / relative) == pin['sha256']
        if simple in BOOGIE_ROOTS:
            assert relative == simple + '.dll' and pin['sha256'] == BOOGIE_ROOTS[simple]
    runtime_directory = Path(receipt['commonFramework']['coreLibPath']).parent
    for simple, pin in framework.items():
        path = Path(pin['path'])
        assert path.parent == runtime_directory and path == path.resolve()
        assert pin['identity'].split(',')[0] == simple and sha(path) == pin['sha256'] and path.stat().st_size == pin['bytes']
    unresolved = common['unavailableMetadataReferences']
    assert len(unresolved) == 1
    edge = unresolved[0]
    assert edge['owner'] == 'System.Reactive' and edge['ownerSha256'] == files['System.Reactive']['sha256'] == OWNER_SHA256
    assert edge['ownerIdentity'] == files['System.Reactive']['identity'] == demand['metadataOwnerIdentity']
    assert edge['requestedIdentity'] == UNAVAILABLE_IDENTITY and edge['metadataFlags'] == 0
    assert edge['scopedTypeReferences'] == ['System.Runtime.InteropServices.WindowsRuntime.EventRegistrationToken']
    assert all(edge[field] is True for field in ['baselineResolverReturnedNull', 'candidateResolverReturnedNull',
           'baselineMetadataCatalogAbsent', 'candidateMetadataCatalogAbsent', 'actualTpaCatalogAbsent'])
    assert edge['publicPackageSha256'] == '8a703a9f0f425f483f01eca033026801118e63bf69962fe32c5e780b0794a83f'
    assert edge['publicPackageAsset'] == 'lib/netstandard2.0/System.Reactive.dll'
    assert edge['declaredPublicSourceCommit'] == '7fe23fedde3d0c462ea3dd851debcf9ccb0f52f6'
    assert edge['declarationKind'] == 'publisher-source-declaration-not-signed-build-origin'
    assert edge['sourceEvidenceSha256'] == '7166e6c53486b9ab3e77a7072328e9f841d7bf1797c550718e170b29913944cc'
    assert not any(field in edge for field in ['resolvedIdentity', 'objectSlot'])
    catalogs = common['packageMetadataCatalogs']
    assert [c['product'] for c in catalogs] == ['baseline', 'candidate']
    for catalog in catalogs:
        label = catalog['product']
        entries = catalog['managedFiles']
        assert 0 < len(entries) <= 512 and len({p['path'] for p in entries}) == len(entries)
        for pin in entries:
            relative = Path(pin['path']).relative_to(Path(products[label]['directory'])).as_posix()
            file = inventories[label][relative]
            assert pin['name'].lower() != UNAVAILABLE_NAME.lower()
            assert file['sha256'] == pin['sha256'] and file['bytes'] == pin['bytes']
    edges = common['metadataReferences']
    assert len(edges) <= 64 * 256
    counts = Counter()
    for ref in edges:
        owner = files[ref['owner']]
        assert ref['ownerIdentity'] == owner['identity'] and ref['ownerSha256'] == owner['sha256']
        assert ref['requestedIdentity'].split(',')[0] != UNAVAILABLE_NAME and ref['metadataFlags'] in [0, 1]
        target = ref['resolvedIdentity'].split(',')[0]
        assert ref['resolution'] in ['shared-common', 'shared-tpa']
        pin = (files if ref['resolution'] == 'shared-common' else framework)[target]
        assert (ref['resolvedIdentity'], ref['path'], ref['sha256']) == (pin['identity'], pin['path'], pin['sha256'])
        assert ref['packageRelativePath'] == (Path(pin['path']).relative_to(base).as_posix() if ref['resolution'] == 'shared-common' else None)
        counts[ref['owner']] += 1
    counts['System.Reactive'] += 1
    owners = common['metadataOwners']
    assert len(owners) == len(files) and {o['owner'] for o in owners} == set(files)
    for owner in owners:
        pin = files[owner['owner']]
        assert owner['ownerIdentity'] == pin['identity'] and owner['ownerSha256'] == pin['sha256']
        assert owner['assemblyReferenceCount'] == counts[owner['owner']] <= 256
    phases = ['initial-framework-only', 'complete-metadata-inventory-before-common-load', 'common-closure-preloaded-before-product-load']
    if name == NAMES[0]:
        phases += ['before-default-negative-demand']
    elif name == NAMES[1]:
        phases += ['before-private-negative-demand', 'private-core-pinned-before-demand', 'private-context-final-loader-audit-baseline']
    else:
        phases += ['before-reactive-event-negative-demand']
    phases += ['nonproof-negative-control-complete', 'nonproof-final-audit-and-retirement']
    assert [a['phase'] for a in common['defaultAudits']] == phases
    slots, prior = {}, set()
    for index, audit in enumerate(common['defaultAudits']):
        current, names, common_names = set(), set(), set()
        for entry in audit['assemblies']:
            slot, simple = entry['objectSlot'], entry['identity'].split(',')[0]
            assert isinstance(slot, int) and slot > 0 and slot not in current and simple not in names and simple != UNAVAILABLE_NAME
            current.add(slot); names.add(simple)
            fact = (entry['kind'], entry['identity'], entry['path'], entry['sha256'])
            assert slot not in slots or slots[slot] == fact
            slots[slot] = fact
            if entry['kind'] == 'harness':
                assert entry['sha256'] == receipt['harnessAssemblySha256'] and simple == 'B3AlcGate'
            else:
                pin = (framework if entry['kind'] == 'shared-tpa' else files)[simple]
                assert entry['kind'] in ['shared-tpa', 'shared-common']
                assert fact[1:] == (pin['identity'], pin['path'], pin['sha256'])
                if entry['kind'] == 'shared-common': common_names.add(simple)
        assert prior.issubset(current) and common_names == (set() if index < 2 else set(files))
        assert sum(e['kind'] == 'harness' for e in audit['assemblies']) == 1
        context_kinds = [c['kind'] for c in audit['contexts']]
        private_phase = name == NAMES[1] and audit['phase'] in ['private-core-pinned-before-demand', 'private-context-final-loader-audit-baseline']
        assert context_kinds == (['default', 'owned-private'] if private_phase else ['default'])
        for context in audit['contexts']:
            assert context['isCollectible'] == (context['kind'] == 'owned-private')
        prior = current
    loads = common['runtimeAssemblyLoads']
    assert 13 <= len(loads) <= 1024 and [e['sequence'] for e in loads] == list(range(1, len(loads) + 1))
    for event in loads:
        assert event['validated'] and event['failure'] is None and event['identity'].split(',')[0] != UNAVAILABLE_NAME
        simple = event['identity'].split(',')[0]
        if event['kind'] == 'private-package':
            assert name == NAMES[1] and event['context'] == 'unavailable-private-negative-control'
            relative = Path(event['path']).relative_to(base).as_posix()
            assert inventories['baseline'][relative]['sha256'] == event['sha256']
        else:
            assert event['kind'] in ['shared-common', 'shared-tpa'] and event['context'] == 'Default'
            pin = (files if event['kind'] == 'shared-common' else framework)[simple]
            assert (event['identity'], event['path'], event['sha256']) == (pin['identity'], pin['path'], pin['sha256'])
    if name == NAMES[1]:
        core = [e for e in observation['privateLoaderLedger'] if e['kind'] == 'private-loaded' and e['identity'] == CORE_IDENTITY]
        assert len(core) == 1 and core[0]['sha256'] == products['baseline']['coreSha256']
        assert core[0]['informationalVersion'] == products['baseline']['coreInformationalVersion']
    else:
        assert observation['privateLoaderLedger'] == []
