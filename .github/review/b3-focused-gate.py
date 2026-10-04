"""Scratch checks; reuse only the exact unchanged library with public verification evidence."""
import csv
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET

output = Path('out/b3-native-compile')
output.mkdir(parents=True, exist_ok=True)
mode = os.environ['B3_FOCUS_GATE']
assert mode in {'ide-regressions', 'maps'}
receipt = {'mode': mode, 'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'verifiedLibraryRun': 37201990430, 'stages': [], 'passed': False}

def run(name, command, validate=None):
    with (output / (name + '.txt')).open('w') as log:
        try:
            result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=1800)
            code = result.returncode
            if code == 0 and validate:
                validate()
        except subprocess.TimeoutExpired:
            log.write('Stage exceeded its safety timeout\n')
            code = 124
        except Exception as error:
            log.write(str(error) + '\n')
            code = 1
    receipt['stages'].append({'stage': name, 'exitCode': code, 'command': command})
    print(name, code, flush=True)
    if code:
        print((output / (name + '.txt')).read_text()[-10000:], flush=True)
    return code == 0

def verified_inputs():
    reuse = output / 'verified-inputs'
    subprocess.run(['gh', 'run', 'download', '37201990430', '-R', 'erniecohen/dafny',
                    '-n', 'b3-native-compile', '-D', str(reuse)], check=True, timeout=180)
    original = reuse / 'out/b3-native-compile'
    summary = json.loads((original / 'summary.json').read_text())
    assert summary['head'] == '89a3b25f96e916a758bd581cd1933e217b7b1352'
    assert summary['fullGate'] and any(s['stage'] == 'worker-bootstrap' and s['exitCode'] == 0
                                       for s in summary['stages'])
    source = Path('ThirdParty/B3')
    source_manifest = source / 'source-manifest.json'
    assert hashlib.sha256(source_manifest.read_bytes()).hexdigest() == '22997b3e47b9b05da58d3efe42e8d7cb46d01a46ea741558c886257791153239'
    manifest = json.loads(source_manifest.read_text())
    actual = set()
    for name in manifest['vendoredPaths']:
        path = source / name
        if path.is_dir():
            actual.update(str(p.relative_to(source)) for p in path.rglob('*') if p.is_file())
        else:
            actual.add(name)
    actual.update(p['file'] for p in manifest['patches'])
    assert actual == set(manifest['files']), 'Unlisted or missing B3 source files'
    for name, digest in manifest['files'].items():
        assert hashlib.sha256((source / name).read_bytes()).hexdigest() == digest, 'Changed B3 source: ' + name
    library = original / 'worker/library/B3Library.dll'
    assert hashlib.sha256(library.read_bytes()).hexdigest() == '2a39d9a11ad2417eb572769c98fa9421009e1d88536547a1e431ef3dd6334720'
    rows = list(csv.DictReader((original / 'worker/library/resources.csv').open()))
    assert len(rows) == 557 and all(r['TestResult.Outcome'] == 'Passed' for r in rows)
    assert (original / 'worker-bootstrap.txt').read_text().startswith('\nDafny program verifier finished with 557 verified, 0 errors\n')
    solver = original / 'inputs/z3-5.1.0-x64-glibc-2.39/bin/z3'
    assert hashlib.sha256(solver.read_bytes()).hexdigest() == 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
    solver.chmod(0o755)
    assert subprocess.check_output([str(solver), '-version'], text=True).strip() == 'Z3 version 5.1.0 - 64 bit'
    installed = Path('Binaries/z3/bin/z3-5.1.0')
    installed.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(solver, installed)
    installed.chmod(0o755)
    receipt['verifiedLibrarySha256'] = hashlib.sha256(library.read_bytes()).hexdigest()
    receipt['solverSha256'] = hashlib.sha256(solver.read_bytes()).hexdigest()
    inputs = output / 'inputs'
    inputs.mkdir()
    for src, name in [(library, 'B3Library.dll'), (solver, 'z3'),
                      (original / 'summary.json', 'original-summary.json'),
                      (original / 'worker/library/resources.csv', 'library-resources.csv'),
                      (original / 'worker-bootstrap.txt', 'original-bootstrap.txt')]:
        shutil.copyfile(src, inputs / name)
    (inputs / 'z3').chmod(0o755)
    shutil.rmtree(reuse)
    return str((inputs / 'B3Library.dll').resolve()), str((inputs / 'z3').resolve())

def passing_tests(directory, count):
    tests = ET.parse(output / directory / 'result.trx').findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    assert tests and (count is None or len(tests) == count) and all(t.get('outcome') == 'Passed' for t in tests), f'Expected {count or "nonzero"} passing {directory} checks'

try:
    library, solver = verified_inputs()
    receipt['stages'].append({'stage': 'verified-inputs', 'exitCode': 0})
    prerequisite = run('packages', ['sh', 'Scripts/fetch-boogie-packages.sh'])
    prerequisite = prerequisite and run('compiler', ['dotnet', 'build', 'Source/Dafny/Dafny.csproj', '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--nologo'])
    def test(directory, project, selector=None, count=None, environment=None):
        command = ['dotnet', 'test', project, '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--results-directory', str(output / directory), '--logger', 'trx;LogFileName=result.trx', '--nologo']
        if selector:
            command += ['--filter', selector]
        if environment:
            command = ['env', *environment, *command]
        return run(directory, command, lambda: passing_tests(directory, count))
    if prerequisite and mode == 'ide-regressions':
        selector = 'FullyQualifiedName~B3CacheVerificationTest|FullyQualifiedName~B3ProjectMigrationTest|FullyQualifiedName~IdeStateObserverRetirementTest|FullyQualifiedName~CounterExampleCapabilityTest|FullyQualifiedName~ProjectManagerDatabaseTest|FullyQualifiedName~ProjectFilesTest|FullyQualifiedName~MultipleFilesProjectTest|FullyQualifiedName~CompetingProjectFilesTest|FullyQualifiedName~AdditionalAxiomsTest|FullyQualifiedName~CounterexamplesStillWorksIfNothingHasBeenVerified'
        test('language-server', 'Source/DafnyLanguageServer.Test/DafnyLanguageServer.Test.csproj', selector, 40, ['DAFNY_TEST_SOLVER_PATH=' + solver])
        test('regressions', 'Source/IntegrationTests/IntegrationTests.csproj', 'DisplayName~git-issue-118.dfy|DisplayName~git-issue-120.dfy|DisplayName~git-issue-126.dfy|DisplayName~git-issue-126-capabilities.dfy', 4)
    elif prerequisite:
        test('normalizer', 'Source/DafnyB3Normalizer.Test/DafnyB3Normalizer.Test.csproj')
        if run('host', ['bash', 'Source/DafnyB3Host.Test/run-tests.sh', library, solver]):
            run('map-theory', ['dotnet', 'run', '--project', 'Source/DafnyB3MapTheory.TestRunner', '-c', 'Release', '--', '--worker', str(Path('build/b3-host-tests/package/DafnyB3Host.dll').resolve()), '--solver', solver, '--solver-sha256', receipt['solverSha256'], '--emit-directory', str(output / 'map-requests')])
    receipt['passed'] = prerequisite and all(s['exitCode'] == 0 for s in receipt['stages'])
except Exception as error:
    receipt['stages'].append({'stage': 'prerequisite', 'exitCode': 1, 'error': str(error)})
    print(str(error), flush=True)
(output / 'summary.json').write_text(json.dumps(receipt, indent=2) + '\n')
with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
    summary.write('Focused B3 ' + mode + ': ' + ('PASS' if receipt['passed'] else 'NOT GREEN') + '\n\n')
    for stage in receipt['stages']:
        summary.write('- ' + stage['stage'] + ': exit ' + str(stage['exitCode']) + '\n')
    summary.write('\nThe unchanged B3 library is hash-pinned to public run 37201990430 (557 passed proof batches). These checks do not repeat that bootstrap or establish complete backend acceptance. Expected failures are recorded with exit zero; inspect summary.json.\n')
