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
            assert member.name.startswith('dafny/') and (member.isfile() or member.isdir())
            assert (baseline / member.name).resolve().is_relative_to(baseline.resolve())
        reader.extractall(baseline)
    receipt['baselineSource'] = 'b07c038737d6713b6d1a5848d7568bdc972de7dd'
    receipt['baselineArtifactRun'] = 37182760834
    receipt['baselineArchiveSha256'] = hashlib.sha256(archive.read_bytes()).hexdigest()
    stage('packages', ['sh', 'Scripts/fetch-boogie-packages.sh'])
    stage('candidate-build', ['dotnet', 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--nologo'])
    stage('harness-build', ['dotnet', 'build', str(sources / 'B3AlcGate.csproj'), '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--output', str(output / 'harness'), '--nologo'])
    controls_path = output / 'controls.json'
    stage('controls', ['timeout', '--kill-after=10s', '180s', 'dotnet', str(output / 'harness/B3AlcGate.dll'), '--baseline', str(baseline / 'dafny'), '--candidate', 'Binaries/net8.0', '--receipt', str(controls_path)], 200)
    controls = json.loads(controls_path.read_text())
    assert controls['controlsPassed'] and len(controls['runs']) == 4
    assert [(r['product'], r['control'], r['exitCode']) for r in controls['runs']] == [
        ('baseline', 'version', 0), ('baseline', 'malformed-command', 1),
        ('candidate', 'version', 0), ('candidate', 'malformed-command', 1)]
    assert all(r['contextCollected'] and not r['failure'] and not r['remainingDirectChildren'] for r in controls['runs'])
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
