"""Source-only proposed coordinator inspection for the separate shared scope.

No entrypoint, subprocess, solver or write operation. The fixed coordinator may
call inspect_shared_common(controls, sha) before accepting its six proof receipts.
Its existing source/package/lifecycle/solver/owned-parent checks stay required.
"""
from pathlib import Path


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


def inspect_shared_common(controls, sha):
    assert controls['schemaVersion'] == 1
    assert controls['scope'] == 'prototype/six-fixed-shared-common-boogie-native-proof-smoke-controls'
    assert controls['persistentBoogieState'] and not controls['freshBoogieStateEstablished']
    assert not controls['ordinaryProofCliEnabled'] and not controls['nativeQueryOrResourceParityEstablished']
    assert controls['requiredLaterControls'] == ['repeat', 'interleaved', 'reversed-order']
    common = controls['commonClosure']
    assert common['scope'] == 'exact-common-managed-closure-in-default-with-private-dafny'
    assert common['sameAssemblyObjectsRequired'] and common['ordinaryPackageResolverSelectionsRequired']
    assert common['persistentBoogieState'] and not common['freshBoogieStateEstablished']
    assert not common['nativeQueryOrCostParityEstablished']
    assert common['requiredLaterControls'] == ['repeat', 'interleaved', 'reversed-order']
    assert common['roots'] == sorted(BOOGIE_ROOTS)
    assert [p['label'] for p in controls['products']] == ['baseline', 'candidate']
    baseline, candidate = controls['products']
    base_dir = Path(baseline['directory'])
    candidate_dir = Path(candidate['directory'])
    base_pins = {f['path']: f for f in baseline['files']}
    candidate_pins = {f['path']: f for f in candidate['files']}
    files = {f['name']: f for f in common['commonFiles']}
    framework = {f['name']: f for f in common['frameworkFiles']}
    assert len(files) == len(common['commonFiles']) and 13 <= len(files) <= 64
    assert len(framework) == len(common['frameworkFiles']) and 0 < len(framework) <= 512
    assert set(files).isdisjoint(framework) and set(BOOGIE_ROOTS).issubset(files)
    for name, pin in files.items():
        assert not name.lower().startswith('dafny') and name != 'B3AlcGate'
        path = Path(pin['path'])
        assert path == path.resolve() and path.is_relative_to(base_dir)
        relative = path.relative_to(base_dir).as_posix()
        assert base_pins[relative] == candidate_pins[relative]
        assert base_pins[relative]['sha256'] == pin['sha256']
        assert base_pins[relative]['bytes'] == pin['bytes'] == path.stat().st_size
        assert 0 < pin['bytes'] <= 67108864 and sha(path) == pin['sha256']
        other = candidate_dir / relative
        assert other.stat().st_size == pin['bytes'] and sha(other) == pin['sha256']
        assert pin['identity'].split(',')[0] == name
        if name in BOOGIE_ROOTS:
            assert relative == name + '.dll' and pin['sha256'] == BOOGIE_ROOTS[name]
    runtime_dir = Path(controls['commonFramework']['coreLibPath']).parent
    for name, pin in framework.items():
        path = Path(pin['path'])
        assert path.parent == runtime_dir and path == path.resolve()
        assert path.stat().st_size == pin['bytes'] and sha(path) == pin['sha256']
        assert not name.lower().startswith(('dafny', 'boogie')) and name != 'B3AlcGate'
        assert pin['identity'].split(',')[0] == name
    assert framework['System.Private.CoreLib']['sha256'] == controls['commonFramework']['coreLibSha256']
    numerics_name = controls['commonFramework']['numericsIdentity'].split(',')[0]
    assert framework[numerics_name]['sha256'] == controls['commonFramework']['numericsSha256']
    assert len(common['metadataReferences']) <= 64 * 256
    for ref in common['metadataReferences']:
        assert ref['owner'] in files and not ref['requestedIdentity'].split(',')[0].lower().startswith('dafny')
        target = ref['resolvedIdentity'].split(',')[0]
        pins = files if ref['resolution'] == 'shared-common' else framework
        assert ref['resolution'] in ('shared-common', 'shared-tpa')
        pin = pins[target]
        assert (ref['resolvedIdentity'], ref['path'], ref['sha256']) == (pin['identity'], pin['path'], pin['sha256'])
        if ref['resolution'] == 'shared-common':
            assert ref['packageRelativePath'] == Path(pin['path']).relative_to(base_dir).as_posix()
        else:
            assert ref['packageRelativePath'] is None
    audits = common['defaultAudits']
    expected_phases = ['initial-framework-only', 'complete-metadata-preflight-before-common-load',
                       'common-closure-preloaded-before-product-load']
    for index, case in enumerate(controls['runs'], 1):
        product = case['product']
        expected_phases += ['before-native-run-' + product + '-' + str(index),
                            'private-context-final-loader-audit-' + product,
                            'after-weak-collection-' + product + '-' + str(index),
                            'after-native-evidence-' + case['name']]
    expected_phases += ['fixed-sequence-complete']
    assert [a['phase'] for a in audits] == expected_phases and len(audits) == 28
    slots, identities, prior_slots = {}, {}, set()
    for index, audit in enumerate(audits):
        current_slots, names, current_common = set(), set(), set()
        for entry in audit['assemblies']:
            slot, kind, identity = entry['objectSlot'], entry['kind'], entry['identity']
            name = identity.split(',')[0]
            assert isinstance(slot, int) and slot > 0 and slot not in current_slots and name not in names
            current_slots.add(slot)
            names.add(name)
            fact = (kind, identity, entry['path'], entry['sha256'])
            assert slot not in slots or slots[slot] == fact
            assert identity not in identities or identities[identity] == slot
            slots[slot], identities[identity] = fact, slot
            if kind == 'harness':
                assert name == 'B3AlcGate' and entry['sha256'] == controls['harnessAssemblySha256']
            else:
                assert kind in ('shared-tpa', 'shared-common') and not name.lower().startswith('dafny')
                pin = (framework if kind == 'shared-tpa' else files)[name]
                assert (identity, entry['path'], entry['sha256']) == (pin['identity'], pin['path'], pin['sha256'])
                if kind == 'shared-common':
                    current_common.add(name)
        assert prior_slots.issubset(current_slots)
        assert current_common == (set() if index < 2 else set(files))
        assert sum(e['kind'] == 'harness' for e in audit['assemblies']) == 1
        prior_slots = current_slots
    for case in controls['runs']:
        ledger = case['run']['loaderLedger']
        preloaded = [e for e in ledger if e['kind'] == 'shared-common-loaded']
        assert len(preloaded) == len(files) and {e['identity'].split(',')[0] for e in preloaded} == set(files)
        assert any(e['kind'] == 'shared-common' and e['identity'].startswith('Boogie.') for e in ledger)
        for entry in ledger:
            name = entry['identity'].split(',')[0]
            if entry['kind'].startswith('private-'):
                assert name not in files and name not in framework
            if entry['kind'] in ('shared-common', 'shared-common-loaded', 'shared-tpa'):
                pin = (framework if entry['kind'] == 'shared-tpa' else files)[name]
                assert entry['context'] == 'Default'
                assert (entry['identity'], entry['path'], entry['sha256']) == (pin['identity'], pin['path'], pin['sha256'])
