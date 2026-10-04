"""Non-verifying scratch controls. Job success never substitutes for four strict receipts."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile

output = Path('out/b3-native-compile')
output.mkdir(parents=True, exist_ok=True)
receipt = {'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'scope': 'non-verifying assembly isolation controls', 'passed': False, 'stages': []}

def stage(name, command, timeout=1200):
    with (output / (name + '.txt')).open('w') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
    receipt['stages'].append({'stage': name, 'exitCode': result.returncode, 'command': command})
    print(name, result.returncode, flush=True)
    if result.returncode:
        print((output / (name + '.txt')).read_text()[-10000:], flush=True)
        raise RuntimeError(name + ' failed')

try:
    sources = Path('.github/review/alc-controls')
    manifest = json.loads((sources / 'source-manifest.json').read_text())
    assert {f['path'] for f in manifest['files']} == {p.name for p in sources.iterdir() if p.is_file() and p.name != 'source-manifest.json'}
    for f in manifest['files']:
        data = (sources / f['path']).read_bytes()
        assert len(data) == f['bytes'] and hashlib.sha256(data).hexdigest() == f['sha256'], 'Changed isolation fixture bytes: ' + f['path']
    baseline = output / 'baseline'
    stage('baseline-input', ['gh', 'run', 'download', '37182760834', '-R', 'erniecohen/dafny', '-n', 'baseline-dafny', '-D', str(baseline)], 120)
    archive = baseline / 'baseline.tar.gz'
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == '679b3569a3e188cdea5d9c061a463ef4adab849584c7d6990434bd87f80f1bae'
    with tarfile.open(archive) as reader:
        for member in reader.getmembers():
            assert (member.name.startswith('dafny/') or (member.name == 'dafny' and member.isdir())) and (member.isfile() or member.isdir()), 'Unexpected baseline archive entry: ' + member.name
            assert (baseline / member.name).resolve().is_relative_to(baseline.resolve()), 'Baseline archive path escapes output: ' + member.name
        reader.extractall(baseline)
    receipt['baselineSource'] = 'b07c038737d6713b6d1a5848d7568bdc972de7dd'
    receipt['baselineArtifactRun'] = 37182760834
    receipt['baselineArchiveSha256'] = hashlib.sha256(archive.read_bytes()).hexdigest()
    baseline_core_sha256 = hashlib.sha256((baseline / 'dafny/DafnyCore.dll').read_bytes()).hexdigest()
    assert baseline_core_sha256 == '9e4eaf6a52cc7ed29d8df5e2185ce00032382a9e9be54688bbcfda2262c865b9', 'Baseline DafnyCore pin changed'
    stage('packages', ['sh', 'Scripts/fetch-boogie-packages.sh'])
    stage('candidate-build', ['dotnet', 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--nologo'])
    candidate_core_sha256 = hashlib.sha256(Path('Binaries/net8.0/DafnyCore.dll').read_bytes()).hexdigest()
    product_pins = {
        'baseline': {'identity': 'DafnyCore, Version=4.11.0.0, Culture=neutral, PublicKeyToken=null',
                     'informationalVersion': '4.11.0+fcb2042d.review.a171069d', 'sha256': baseline_core_sha256},
        'candidate': {'identity': 'DafnyCore, Version=4.11.0.0, Culture=neutral, PublicKeyToken=null',
                      'informationalVersion': '4.11.0+' + receipt['head'], 'sha256': candidate_core_sha256}}
    receipt['productAssemblyPins'] = product_pins
    stage('harness-build', ['dotnet', 'build', str(sources / 'B3AlcGate.csproj'), '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--output', str(output / 'harness'), '--nologo'])
    controls_path = output / 'controls.json'
    stage('controls', ['timeout', '--kill-after=10s', '180s', 'dotnet', str(output / 'harness/B3AlcGate.dll'), '--baseline', str(baseline / 'dafny'), '--candidate', 'Binaries/net8.0', '--receipt', str(controls_path)], 200)
    controls = json.loads(controls_path.read_text())
    assert controls['schemaVersion'] == 1 and controls['scope'] == 'prototype/non-verifying-controls'
    assert controls['sourceManifestSha256'] == hashlib.sha256((sources / 'source-manifest.json').read_bytes()).hexdigest(), 'Wrong executed source manifest'
    assert controls['harnessAssemblySha256'] == hashlib.sha256((output / 'harness/B3AlcGate.dll').read_bytes()).hexdigest(), 'Wrong executed harness assembly'
    assert controls['controlsPassed'] and len(controls['runs']) == 4
    assert [(r['product'], r['control'], r['exitCode']) for r in controls['runs']] == [
        ('baseline', 'help', 0), ('baseline', 'malformed-command', 1),
        ('candidate', 'help', 0), ('candidate', 'malformed-command', 1)]
    assert all(r['contextCollected'] and not r['failure'] and not r['remainingDirectChildren'] for r in controls['runs'])
    fixtures = {control['name']: control for control in json.loads((sources / 'control-fixtures.json').read_text())['controls']}
    for run in controls['runs']:
        fixture = fixtures[run['control']]
        assert run['arguments'] == fixture['arguments'] and run['exitCode'] == fixture['expectedExitCode'], 'Wrong argument control receipt'
        assert fixture['requiredOutput'] in run['output'] + run['errorOutput'], 'Missing product argument-path diagnostic'
        expected = product_pins[run['product']]
        loaded_core = [entry for entry in run['loaderLedger']
                       if entry['kind'] == 'private-loaded' and entry['identity'].startswith('DafnyCore,')]
        assert len(loaded_core) == 1, 'Missing or duplicate loaded product identity: ' + run['product']
        actual = loaded_core[0]
        assert all(actual[key] == expected[key] for key in expected), 'Loaded product identity/hash mismatch: ' + run['product']
        expected_path = baseline / 'dafny/DafnyCore.dll' if run['product'] == 'baseline' else Path('Binaries/net8.0/DafnyCore.dll')
        assert Path(actual['path']) == expected_path.resolve(), 'Loaded product assembly escaped its package: ' + run['product']
        assert run['proofCleanup'] is None, 'Non-verifying control acquired a proof scope'
    receipt['passed'] = True
except Exception as error:
    receipt['failure'] = str(error)
    print(str(error), flush=True)
finally:
    # Replace only public scratch runner directories; hashes/identities remain exact.
    controls_path = output / 'controls.json'
    if controls_path.exists():
        data = controls_path.read_text().replace(str(Path.cwd()), 'checkout')
        data = data.replace('/usr/share/dotnet', 'shared-dotnet-runtime')
        controls_path.write_text(data)
    (output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write('Non-verifying assembly isolation controls: ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
        for item in receipt['stages']:
            summary.write('- ' + item['stage'] + ': exit ' + str(item['exitCode']) + '\n')
        summary.write('\nThese are four argument/loader/cleanup controls, with no verification workload. They do not establish native query equality or resource parity. Expected failures are recorded with exit zero; inspect summary.json and controls.json.\n')
