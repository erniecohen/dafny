"""Check pass-through, redundant join batches, and nonvacuity in each encoding."""
import json
from pathlib import Path
import subprocess
import sys

dafny, solver, source, output = sys.argv[1:]
output = Path(output).resolve()
output.mkdir(parents=True, exist_ok=True)
logs = {}
for encoding in ('a', 'm', 'p'):
    for policy in (None, 0, 1):
        name = encoding + '-' + str(policy)
        log = output / (name + '.json')
        flags = '/typeEncoding:' + encoding + ' /restartProver'
        if policy is not None:
            flags += ' /monomorphicSharedLiterals:' + str(policy)
        command = ['dotnet', dafny, 'verify', source, '--solver-path', solver,
                   '--cores', '2', '--isolate-assertions', '--show-snippets:false',
                   '--use-basename-for-filename', '--standard-libraries:false',
                   '--type-system-refresh', '--general-traits=datatype', '--general-newtypes',
                   '--boogie', flags, '--log-format', 'json;LogFileName=' + str(log)]
        result = subprocess.run(command, capture_output=True, text=True, timeout=120)
        assert result.returncode == 4, (name, result.returncode, result.stdout, result.stderr)
        assert "out-parameter 'x'" in result.stdout, (name, result.stdout)
        assert 'assertion might not hold' in result.stdout, (name, result.stdout)
        assert result.stdout.count(': Error:') == 2, (name, result.stdout)
        rows = json.loads(log.read_text())['verificationResults']
        positive = [r for r in rows if r['name'].endswith('Join (correctness)')
                    and not r['name'].endswith('FalseAtJoin (correctness)')]
        assert len(positive) == 1 and positive[0]['outcome'] == 'Correct', (name, rows)
        assert all(vc['outcome'] == 'Valid' for vc in positive[0]['vcResults']), (name, positive)
        logs[encoding, policy] = len(positive[0]['vcResults'])
    assert logs[encoding, None] == logs[encoding, 0], logs
assert logs['m', 1] < logs['m', 0], logs
for encoding in ('a', 'p'):
    assert logs[encoding, 0] == logs[encoding, 1], logs
print('Shared-literal pass-through, join batches, and controls passed')
