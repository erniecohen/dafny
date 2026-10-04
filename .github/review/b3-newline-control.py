"""Check that the full-pipe regression distinguishes the old unbounded newline write."""
import hashlib
import json
import pathlib
import subprocess
import xml.etree.ElementTree as ET

output = pathlib.Path('out/b3-native-compile/terminator-control')
output.mkdir(parents=True, exist_ok=True)
source = pathlib.Path('Source/DafnyB3Protocol/WorkerProcessClient.cs')
original = source.read_bytes()
bounded = b"await process.StandardInput.BaseStream.WriteAsync(new byte[] { (byte)'\\n' }, token);"
assert original.count(bounded) == 1
broken = original.replace(bounded, b'await process.StandardInput.WriteLineAsync();')
report = {'sourceSha256': hashlib.sha256(original).hexdigest(), 'runs': []}

def run(label):
    folder = output / label
    folder.mkdir(exist_ok=True)
    command = ['dotnet', 'test', 'Source/DafnyB3Protocol.Test/DafnyB3Protocol.Test.csproj',
               '-c', 'Release', '-m:1', '-p:UseSharedCompilation=false', '--nologo',
               '--filter', 'FullyQualifiedName~BackpressureOnRecordTerminatorHonorsCancellation',
               '--results-directory', str(folder), '--logger', 'trx;LogFileName=result.trx']
    with (folder / 'log.txt').open('w') as log:
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=180)
    tree = ET.parse(folder / 'result.trx')
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests = [{'name': test.attrib['testName'], 'outcome': test.attrib['outcome'],
              'error': '\n'.join(test.itertext())}
             for test in tree.findall('.//t:UnitTestResult', ns)]
    row = {'label': label, 'exitCode': result.returncode, 'tests': tests}
    report['runs'].append(row)
    return row

try:
    source.write_bytes(broken)
    prior = run('old-write')
finally:
    source.write_bytes(original)
current = run('bounded-write')
assert source.read_bytes() == original
report['passed'] = (prior['exitCode'] == 1 and len(prior['tests']) == 2 and
                    all(test['outcome'] == 'Failed' and 'TimeoutException' in test['error']
                        for test in prior['tests']) and
                    current['exitCode'] == 0 and len(current['tests']) == 2 and
                    all(test['outcome'] == 'Passed' for test in current['tests']))
(output / 'summary.json').write_text(json.dumps(report, indent=2) + '\n')
print('Terminator regression counterfactual:', 'PASS' if report['passed'] else 'NOT GREEN')
raise SystemExit(0 if report['passed'] else 1)
