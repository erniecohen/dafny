#!/usr/bin/env python3
"""Equality and sensitivity controls for ready-module verification (issue 241)."""
from collections import Counter
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile


def observations(path):
    rows = json.loads(path.read_text())['verificationResults']
    result = {}
    for row in rows:
        for vc in row['vcResults']:
            key = (row['name'], vc['vcNum'])
            assert key not in result, ('duplicate VC', key)
            result[key] = (vc['outcome'], vc['resourceCount'],
                           json.dumps(vc['assertions'], sort_keys=True))
    assert result, 'no VCs exercised'
    assert any('Good' in key[0] and value[0] == 'Valid' and value[1] > 0
               for key, value in result.items()), 'positive proof absent'
    assert any('Bad' in key[0] and value[0] == 'Invalid' and value[1] > 0
               for key, value in result.items()), 'false assertion did not fail'
    return result


def require_equal(expected, actual, label):
    assert actual == expected, ('output, Boogie, VCs or SMT changed', label)


def run(binary, solver, source, directory, extra):
    directory.mkdir()
    (directory / 'smt').mkdir()
    command = ['dotnet', binary, 'verify', source, '--solver-path', solver,
               '--cores', '2', '--type-system-refresh', '--verification-time-limit', '60',
               '--show-snippets:false', '--use-basename-for-filename',
               '--boogie', '/restartProver', '--boogie', '/deterministicLiteralHashes:1',
               '--bprint', str(directory / 'program.bpl'),
               '--log-format', 'json;LogFileName=' + str(directory / 'log.json'),
               '--solver-log', str(directory / 'smt' / '@PROC@.smt2')]
    # The lit runner sanitizes its environment. Keep subprocess scratch beside its outputs.
    env = dict(os.environ, TMPDIR=str(directory), MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false')
    result = subprocess.run(command + extra, text=True, capture_output=True, env=env, timeout=110)
    assert result.returncode == 4, (result.returncode, result.stdout, result.stderr)
    assert not result.stderr, result.stderr
    (directory / 'stdout').write_text(result.stdout)
    bpl = {p.name: p.read_bytes() for p in directory.glob('program_*.bpl')}
    assert len(bpl) >= 4, 'module capture did not run'
    helper = Path(__file__).with_name('issue241-module-window.py')
    spec = importlib.util.spec_from_file_location('module_window', helper)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    smt = Counter(module.stream_hash(p)[0] for p in (directory / 'smt').glob('*.smt2'))
    assert smt and sum(smt.values()) >= 4, 'solver input not exercised'
    return result.stdout, bpl, observations(directory / 'log.json'), smt


def main():
    binary, solver, source = map(str, map(Path.resolve, map(Path, sys.argv[1:4])))
    directory = Path(sys.argv[4]).resolve()
    directory.mkdir(parents=True, exist_ok=True)
    directory = Path(tempfile.mkdtemp(prefix='run-', dir=directory))
    baseline = run(binary, solver, source, directory / 'serial', [])
    parallel = ['--verification-module-window', '4', '--translation-workers', '4']
    for name, extra in [('ordered', parallel),
                        ('ready', parallel + ['--ready-module-verification', '--translation-workers-stress', '-1']),
                        ('seeded', parallel + ['--ready-module-verification', '--translation-workers-stress', '7'])]:
        actual = run(binary, solver, source, directory / name, extra)
        require_equal(baseline, actual, name)
    print('ready modules: identical Boogie, SMT, assertions, verdicts and resources; positive and vacuity controls exercised')
    fresh = ['--boogie', '/restartProver']
    for extra in [fresh, fresh + ['--translation-workers', '4'],
                  fresh + ['--verification-module-window', '4'], parallel]:
        # The last case supplies both concurrency options but omits fresh solver history.
        result = subprocess.run(['dotnet', binary, 'verify', source, '--type-system-refresh', '--ready-module-verification'] + extra,
                                text=True, capture_output=True, timeout=110)
        assert result.returncode == 1, (extra, result.returncode, result.stdout, result.stderr)
        assert '--ready-module-verification requires' in result.stdout + result.stderr
    help_result = subprocess.run(['dotnet', binary, 'verify', '--help'], text=True, capture_output=True, timeout=110)
    assert help_result.returncode == 0
    assert '--ready-module-verification' not in help_result.stdout
    print('invalid combinations rejected; hidden option absent from help')
    shutil.rmtree(directory)


if __name__ == '__main__':
    main()
