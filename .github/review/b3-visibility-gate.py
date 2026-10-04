"""Focused definition visibility gate; strict public proof prerequisite and actual workers."""
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET

output = Path('out/b3-native-compile')
output.mkdir(parents=True, exist_ok=True)
receipt = {'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'scope': 'bounded source-owned definition visibility; no full backend or default parity claim',
           'verifiedLibraryRun': 37220064287, 'priorFullGatePassed': False,
           'passed': False, 'stages': []}


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1048576), b''):
            value.update(block)
    return value.hexdigest()


def stage(name, command, validator=None, timeout=1800):
    with (output / (name + '.txt')).open('w') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
        log.flush()
    receipt['stages'].append({'stage': name, 'command': command, 'exitCode': result.returncode})
    if result.returncode:
        raise RuntimeError(name + ' failed: exit ' + str(result.returncode))
    if validator:
        validator()


def verified_inputs():
    reuse = output / 'verified-inputs'
    stage('verified-input-download', ['gh', 'run', 'download', '37220064287', '-R', 'erniecohen/dafny',
                                     '-n', 'b3-native-compile', '-D', str(reuse)], timeout=180)
    original = reuse / 'out/b3-native-compile'
    assert digest(original / 'summary.json') == '534bc7387194c50df1a0db2c3eb5c073ea0608a616fa3f90e8ae002beca616f6'
    summary = json.loads((original / 'summary.json').read_text())
    assert summary['head'] == '79ae9789ecb1a90b723426286fa1ebf88f1b0afc' and summary['fullGate']
    assert not summary['passed'], 'This prerequisite has a failed runtime stage; do not claim full acceptance'
    assert any(s['stage'] == 'worker-bootstrap' and s['exitCode'] == 0 for s in summary['stages'])
    source = Path('ThirdParty/B3')
    manifest_path = source / 'source-manifest.json'
    fingerprint = digest(manifest_path)
    assert fingerprint == '8525b69200d1c5d2e3e18e21a97f14aeb68009137b489093b9ff3046357162ac'
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
    assert inventory == set(manifest['files']), 'Source inventory differs'
    for name, value in manifest['files'].items():
        assert digest(source / name) == value, name
    library = original / 'worker/library/B3Library.dll'
    library_sha = '8c9892bde19f589124f3268bca21283af9dab3ca3f11e26df9803fab2d9ef35e'
    assert digest(library) == library_sha
    assert digest(original / 'worker/library/resources.csv') == '7373fed6019e5baf757e86cbcba62d95cfbb34f70799a92dff2e2f01d7f2688a'
    assert digest(original / 'worker-bootstrap.txt') == 'c43127f14789e55118a7b693ea19eb2f147a41f07d12e630031f900f5b14c977'
    rows = list(csv.DictReader((original / 'worker/library/resources.csv').open()))
    assert len(rows) == 560 and all(r['TestResult.Outcome'] == 'Passed' for r in rows)
    assert re.findall(r'Dafny program verifier finished with (\d+) verified, 0 errors',
                      (original / 'worker-bootstrap.txt').read_text()) == ['560']
    proof = summary['libraryProof']
    assert proof['sourceManifestSha256'] == fingerprint and proof['librarySha256'] == library_sha
    assert proof['batchCount'] == 560 and proof['resourceCount'] == sum(int(r['TestResult.ResourceCount']) for r in rows)
    assert proof['maximumBatchResources'] == max(int(r['TestResult.ResourceCount']) for r in rows)
    solver = original / 'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
    solver_sha = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
    assert digest(solver) == solver_sha
    solver.chmod(0o755)
    assert subprocess.check_output([str(solver), '-version'], text=True, timeout=20).strip() == 'Z3 version 5.1.0 - 64 bit'
    inputs = output / 'inputs'
    inputs.mkdir()
    for src, name in [(library, 'B3Library.dll'), (solver, 'z3'), (original / 'summary.json', 'original-summary.json'),
                      (original / 'worker/library/resources.csv', 'library-resources.csv'),
                      (original / 'worker-bootstrap.txt', 'original-bootstrap.txt')]:
        shutil.copyfile(src, inputs / name)
    (inputs / 'z3').chmod(0o755)
    installed = Path('Binaries/z3/bin/z3-5.1.0')
    installed.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(solver, installed)
    installed.chmod(0o755)
    receipt.update({'sourceManifestSha256': fingerprint, 'verifiedLibrarySha256': library_sha,
                    'solverSha256': solver_sha, 'libraryProof': proof})
    shutil.rmtree(reuse)
    return str((inputs / 'B3Library.dll').resolve()), str((inputs / 'z3').resolve())


def tests(name, count):
    rows = ET.parse(output / name / 'result.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    assert len(rows) == count and all(row.get('outcome') == 'Passed' for row in rows), 'Incomplete ' + name + ' denominator'
    receipt.setdefault('testCounts', {})[name] = count


def host():
    totals = re.findall(r'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)',
                        (output / 'host.txt').read_text())
    assert totals == [('0', '79', '0', '79'), ('0', '35', '0', '35')], 'Incomplete host/protocol controls'


def visibility():
    rows = [json.loads(line) for line in (output / 'visibility.txt').read_text().splitlines() if line.startswith('{')]
    expected = json.loads(Path('Source/DafnyB3Normalizer.Test/VisibilityInputs/cases.json').read_text())
    cases = [row for row in rows if row.get('kind') == 'visibility-case']
    assert len(expected['cases']) == len(cases) == 27 and all(row['matched'] and row['allStrict'] and row['attemptsMatched'] for row in cases)
    assert {row['fixture'] for row in cases} == {case['file'] for case in expected['cases']}
    assert all(row['compilerVersion'] == '4.11.0+' + receipt['head'] and
               row['workerSourceFingerprint'] == receipt['sourceManifestSha256'] and
               row['solverVersion'] == '5.1.0' and row['solverSha256'] == receipt['solverSha256'] for row in cases)
    isolation = [row for row in rows if row.get('kind') == 'visibility-session-isolation']
    assert len(isolation) == 1 and isolation[0]['matched'] and len(isolation[0]['completions']) == 5
    assert len(rows) == 29 and rows[-1] == {'kind': 'visibility-summary', 'matched': 28, 'total': 28, 'isolationMatched': True, 'passed': True}
    receipt['visibility'] = {'cases': 27, 'sessionIsolationControls': 1, 'sessionIsolationLaunches': 5}


try:
    library, solver = verified_inputs()
    stage('packages', ['sh', 'Scripts/fetch-boogie-packages.sh'])
    stage('compiler', ['dotnet', 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--nologo'])
    for name, project, count, selector in [
            ('contracts', 'Source/DafnyCore.Test/DafnyCore.Test.csproj', 26,
             'FullyQualifiedName~VerificationContractsTest|FullyQualifiedName~B3BackendSelectionTest|FullyQualifiedName~B3WorkItemTest'),
            ('normalizer', 'Source/DafnyB3Normalizer.Test/DafnyB3Normalizer.Test.csproj', 252, None)]:
        command = ['dotnet', 'test', project, '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false',
                   '--results-directory', str(output / name), '--logger', 'trx;LogFileName=result.trx', '--nologo']
        if selector:
            command += ['--filter', selector]
        stage(name, command, lambda name=name, count=count: tests(name, count))
    stage('host', ['bash', 'Source/DafnyB3Host.Test/run-tests.sh', library, solver], host)
    stage('visibility', ['dotnet', 'run', '--project', 'Source/DafnyB3Visibility.TestRunner', '-c', 'Release', '--',
                         '--worker', str(Path('build/b3-host-tests/package/DafnyB3Host.dll').resolve()), '--solver', solver,
                         '--solver-sha256', receipt['solverSha256'], '--compiler-version', '4.11.0+' + receipt['head']], visibility)
    receipt['passed'] = True
except Exception as error:
    receipt['failure'] = type(error).__name__ + ': ' + str(error)
finally:
    (output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write('Focused B3 visibility: ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
        summary.write('The exact unchanged library uses the inspected 560 passed proof batches from public run 37220064287. That prior full gate failed four runtime fixtures; this gate does not waive them or claim complete backend acceptance. Definition fixtures require actual strict worker verdicts, reachable false controls, source-origin evidence and five session-isolation launches. Expected failures exit zero; inspect summary.json and visibility.txt.\n')
