#!/usr/bin/env python3
"""Inspect embedded checked bundles in a release zip, then verify with the extracted executable."""
import argparse
import io
from pathlib import Path
import subprocess
import tempfile
import tomllib
import zipfile

TARGETS = ['notarget', 'cs', 'java', 'js', 'go', 'py']
RESOURCES = {'DafnyStandardLibraries-checked' + suffix + '.doo'
             for suffix in [''] + ['-' + target for target in TARGETS]}


def check_resources(directory):
    actual = {p.name for p in directory.glob('*.doo')}
    if actual != RESOURCES:
        raise ValueError('Checked catalog resources differ: missing ' + ', '.join(sorted(RESOURCES - actual)) +
                         '; unexpected ' + ', '.join(sorted(actual - RESOURCES)))
    builds = set()
    for name in sorted(RESOURCES):
        with zipfile.ZipFile(io.BytesIO((directory / name).read_bytes())) as bundle:
            if bundle.testzip() is not None:
                raise ValueError('Corrupt checked bundle: ' + name)
            required = {'manifest.toml', 'program', 'termination-contracts.json'}
            if not required.issubset(bundle.namelist()):
                raise ValueError('Missing certificate content: ' + name)
            record = tomllib.loads(bundle.read('manifest.toml').decode()).get('modular_termination', {})
        if record.get('enabled') is not True or record.get('status') != 'complete' or not record.get('fork_build_id'):
            raise ValueError('Checked bundle lacks a complete record: ' + name)
        builds.add(record['fork_build_id'])
    if len(builds) != 1:
        raise ValueError('Checked bundles were produced by different verifier builds')


def check_archive(archive, reader, solver, inspect_only=False):
    with tempfile.TemporaryDirectory() as scratch:
        root = Path(scratch)
        with zipfile.ZipFile(archive) as package:
            package.extractall(root)
        distribution = root / 'dafny'
        resources = root / 'resources'
        inspected = subprocess.run(['dotnet', str(reader), str(distribution / 'DafnyPipeline.dll'), str(resources)],
                                   capture_output=True, text=True)
        if inspected.returncode:
            raise ValueError('Cannot inspect embedded release resources:\n' + inspected.stdout + inspected.stderr)
        check_resources(resources)
        if inspect_only:
            print('Release zip embeds all seven complete checked bundles; execution qualified downstream')
            return
        executable = distribution / 'dafny'
        executable.chmod(executable.stat().st_mode | 0o111)
        source = root / 'client.dfy'
        program = '''module Client {
  import Std.ActionsExterns
  method Use() {
    var producer, proof := ActionsExterns.MakeSetReader({7});
    assert proof.Valid();
    assert proof.Producer() == producer;
    assert producer.allowance <= proof.allowance;
    proof.ProducesSet([]);
    // CONTROL
  }
}
'''
        command = [str(executable), 'verify', str(source), '--solver-path', str(solver), '--cores', '3',
                   '--resource-limit', '16000000', '--standard-libraries', '--type-system-refresh',
                   '--general-traits=datatype', '--general-newtypes', '--reads-clauses-on-methods',
                   '--enforce-determinism', '--track-print-effects',
                   '--modular-termination-trust:standard-library',
                   '--modular-termination-runtime-trust:standard-library']
        for false in [False, True]:
            source.write_text(program.replace('// CONTROL', 'assert false;' if false else ''))
            result = subprocess.run(command, cwd=root, capture_output=True, text=True)
            text = result.stdout + result.stderr
            expected = 4 if false else 0
            if result.returncode != expected or any(s in text.lower() for s in
                    ['out of resource', 'timed out', 'unhandled exception']):
                raise ValueError('Release catalog client failed:\n' + text)
            if 'Std.ActionsExterns.MakeSetReader' not in text or 'including termination' not in text:
                raise ValueError('Release catalog did not report its runtime assumptions:\n' + text)
            if false and (text.count('Error: assertion might not hold') != 1 or '1 error' not in text):
                raise ValueError('Release false control failed for a different reason:\n' + text)
    print('Release zip embeds all seven complete checked bundles; client and sole-false control pass')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    parser.add_argument('--reader', type=Path, required=True)
    parser.add_argument('--solver', type=Path, required=True)
    parser.add_argument('--inspect-only', action='store_true',
                        help='Inspect cross-published resources; execution must be qualified downstream')
    args = parser.parse_args()
    check_archive(args.archive.resolve(), args.reader.resolve(), args.solver.resolve(), args.inspect_only)
