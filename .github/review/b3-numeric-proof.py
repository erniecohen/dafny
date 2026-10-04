"""Focused universal numeric-checker proofs; expected failures remain receipt data."""
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

output = Path('out/b3-native-compile')
output.mkdir(parents=True, exist_ok=True)
receipt = {'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'scope': 'focused B3 numeric checker proofs', 'passed': False,
           'completeLibraryVerified': False, 'workerBinaryProduced': False, 'stages': []}

def stage(name, command, cwd=None, timeout=1800):
    with (output / (name + '.txt')).open('w') as log:
        result = subprocess.run(command, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
    receipt['stages'].append({'stage': name, 'command': command, 'exitCode': result.returncode})
    if result.returncode:
        raise RuntimeError(name + ' failed: exit ' + str(result.returncode))

try:
    source = Path('ThirdParty/B3')
    manifest_path = source / 'source-manifest.json'
    fingerprint = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    expected = re.search(r"source_manifest.read_bytes\(\)\).hexdigest\(\) == '([0-9a-f]{64})'",
                         Path('Scripts/build-b3-worker.sh').read_text())
    assert expected and fingerprint == expected.group(1), 'Source consumer pin mismatch'
    manifest = json.loads(manifest_path.read_text())
    assert manifest['upstreamCommit'] == 'ea6e8a18dfe9e317d313de769291f989957dc5f2'
    inventory = set()
    for name in manifest['vendoredPaths']:
        path = source / name
        if path.is_dir():
            inventory.update(p.relative_to(source).as_posix() for p in path.rglob('*') if p.is_file())
        else:
            inventory.add(name)
    inventory.update(p['file'] for p in manifest['patches'])
    assert inventory == set(manifest['files']), 'Source inventory mismatch'
    for name, digest in manifest['files'].items():
        assert hashlib.sha256((source / name).read_bytes()).hexdigest() == digest, name
    receipt['sourceManifestSha256'] = fingerprint
    stage('pinned-inputs', ['python3', '.github/review/b3-public-inputs.py'], timeout=240)
    archive = output / 'inputs/bootstrap/dafny.tar.gz'
    compiler_dir = output / 'numeric-proof/compiler'
    compiler_dir.mkdir(parents=True, exist_ok=True)
    stage('unpack-compiler', ['tar', '-xzf', str(archive), '-C', str(compiler_dir)], timeout=120)
    compiler = (compiler_dir / 'dafny/Dafny.dll').resolve()
    solver = (output / 'inputs' / os.environ['Z3_LINUX_X64'] / 'bin/z3').resolve()
    assert subprocess.check_output(['dotnet', str(compiler), '--version'], text=True, timeout=20).strip() == '4.11.0+fcb2042d.review.a171069d'
    assert hashlib.sha256(solver.read_bytes()).hexdigest() == 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
    assert subprocess.check_output([str(solver), '-version'], text=True, timeout=10).strip() == 'Z3 version 5.1.0 - 64 bit'
    csv_path = (output / 'numeric-proof/resources.csv').resolve()
    stage('numeric-proof', ['env', 'DOTNET_GCHeapHardLimit=C0000000', 'dotnet', str(compiler),
                          'verify', 'library/dfyconfig.toml', '--filter-symbol', 'TypeChecker',
                          '--solver-path', str(solver), '--cores', '2', '--resource-limit', '100000000',
                          '--verification-time-limit', '120', '--log-format', 'csv;LogFileName=' + str(csv_path)], cwd='ThirdParty/B3')
    rows = list(csv.DictReader(csv_path.open()))
    required = {'TypeChecker.CheckExpr (correctness)', 'TypeChecker.ExpectSameNumericOperands (correctness)'}
    assert required <= {row['TestResult.DisplayName'] for row in rows}, 'Affected proof denominator missing'
    assert rows and all(row['TestResult.Outcome'] == 'Passed' for row in rows), 'Focused proofs incomplete'
    assert all(row['TestResult.DisplayName'].startswith('TypeChecker.') for row in rows), 'Unexpected proof scope'
    summaries = re.findall(r'Dafny program verifier finished with (\d+) verified, 0 errors', (output / 'numeric-proof.txt').read_text())
    assert len(summaries) == 1 and int(summaries[0]) == len(rows), 'Output and CSV proof counts disagree'
    receipt['focusedProof'] = {'batchCount': len(rows), 'resourceCount': sum(int(row['TestResult.ResourceCount']) for row in rows),
                               'maximumBatchResources': max(int(row['TestResult.ResourceCount']) for row in rows),
                               'requiredDeclarations': sorted(required)}
    receipt['passed'] = True
except Exception as error:
    receipt['failure'] = type(error).__name__ + ': ' + str(error)
finally:
    (output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write('Focused B3 numeric proofs: ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
        summary.write('This verifies the selected type-checker contracts only. No whole-library proof, worker build or backend runtime acceptance is claimed. Expected failures are recorded with exit zero; inspect summary.json and the CSV.\n')

