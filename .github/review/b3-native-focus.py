#!/usr/bin/env python3
"""Scratch-only paired native Fuel and pruning lifecycle observations."""
import argparse
import json
from pathlib import Path
import re
import subprocess


def run(binary, solver, cwd, arguments, output):
    output.mkdir(parents=True, exist_ok=True)
    command = [str(binary), 'verify', *arguments, '--solver-path', str(solver),
               '--verification-time-limit', '0', '--log-format',
               'json;LogFileName=' + str(output / 'results.json')]
    (output / 'command.json').write_text(json.dumps(command) + '\n')
    process = subprocess.run(command, cwd=cwd, capture_output=True, text=True, timeout=1800)
    text = process.stdout + process.stderr
    (output / 'output.txt').write_text(text)
    results = json.loads((output / 'results.json').read_text()).get('verificationResults', []) if (output / 'results.json').exists() else []
    batches = {result['name'] + '\t' + str(batch['vcNum']): [batch['outcome'], batch['resourceCount']]
               for result in results for batch in result['vcResults']}
    verdict = [process.returncode, re.findall(r'verifier finished with ([^\n]*)', text),
               sorted(re.findall(r'^.*?\(\d+,\d+\): (?:Error|Warning): .*$', text, re.M))]
    return {'verdict': verdict, 'batches': batches}


def differences(first, second):
    return {key: {'first': first.get(key), 'second': second.get(key)}
            for key in sorted(first.keys() | second.keys()) if first.get(key) != second.get(key)}


def main():
    parser = argparse.ArgumentParser()
    for key in ['candidate', 'baseline', 'previous', 'solver', 'output']:
        parser.add_argument('--' + key, required=True, type=Path)
    options = parser.parse_args()
    for key in ['candidate', 'baseline', 'previous', 'solver', 'output']:
        setattr(options, key, getattr(options, key).resolve())
    root = Path.cwd()
    observations = {}
    binaries = {'baseline': options.baseline, 'previous': options.previous, 'candidate': options.candidate}
    # The exact existing corpus program and flags that exposed the translation-error abort.
    fuel_arguments = ['dafny0/Fuel.dfy', '--resource-limit', '16000000', '--boogie',
                      '/normalizeDeclarationOrder:0', '--cores', '1', '--allow-warnings',
                      '--allow-deprecation', '--manual-triggers', '--error-limit', '0']
    fuel_cwd = root / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'
    for role in ['baseline', 'candidate']:
        observations['Fuel-' + role] = run(binaries[role], options.solver, fuel_cwd, fuel_arguments,
                                           options.output / ('Fuel-' + role))
    # Materializing all lazy split blocks before selecting a declaration may change AST IDs
    # and pruning state. These unchanged library declarations retain the full module translation.
    library_arguments = ['src/Std/dfyconfig.toml', '--filter-symbol', 'Std.JSON.ZeroCopy.Serializer', '--cores', '4']
    for repeat in range(2):
        for role, binary in binaries.items():
            name = 'Library-' + role + '-' + str(repeat)
            observations[name] = run(binary, options.solver, root / 'Source/DafnyStandardLibraries',
                                     library_arguments, options.output / name)
    fuel = {'verdict_equal': observations['Fuel-baseline']['verdict'] == observations['Fuel-candidate']['verdict'],
            'batch_changes': differences(observations['Fuel-baseline']['batches'], observations['Fuel-candidate']['batches'])}
    library = {}
    for repeat in range(2):
        for role in ['previous', 'candidate']:
            before = observations['Library-baseline-' + str(repeat)]
            after = observations['Library-' + role + '-' + str(repeat)]
            library[role + '-' + str(repeat)] = {
                'verdict_equal': before['verdict'] == after['verdict'],
                'batch_changes': differences(before['batches'], after['batches'])}
    for role in binaries:
        before = observations['Library-' + role + '-0']
        after = observations['Library-' + role + '-1']
        library[role + '-repeat'] = {'verdict_equal': before['verdict'] == after['verdict'],
                                     'batch_changes': differences(before['batches'], after['batches'])}
    report = {'solver': subprocess.check_output([str(options.solver), '--version'], text=True).strip(),
              'fuel': fuel, 'library': library,
              'denominators': {name: len(value['batches']) for name, value in observations.items()}}
    (options.output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    lines = ['# Focused native compatibility observations', '', 'All verification uses Z3 5.1.0.',
             'Mismatches are recorded without failing this scratch probe.', '',
             'Fuel verdict equal: ' + str(fuel['verdict_equal']),
             'Fuel batch differences: ' + str(len(fuel['batch_changes']))]
    lines += [name + ': verdict equal=' + str(value['verdict_equal']) + ', batch differences=' + str(len(value['batch_changes']))
              for name, value in library.items()]
    (options.output / 'summary.md').write_text('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
