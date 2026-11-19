#!/usr/bin/env python3
"""Reproduce a typed pre-VC feature census; no solver or verifier task is invoked."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()


def run(command, log):
    with log.open('w') as out:
        subprocess.run(command, cwd=ROOT, stdout=out, stderr=subprocess.STDOUT, check=True)


REQUIRED_FEATURES = {
    'node.Axiom', 'node.Implementation', 'node.OldExpr', 'node.ForallExpr', 'node.LambdaExpr',
    'node.MapType', 'operator.MapSelect.MapSelect', 'operator.MapStore.MapStore',
    'attribute.bvbuiltin', 'polymorphic.function',
    'contract.free-requires', 'contract.checked-requires', 'contract.free-ensures',
    'contract.checked-ensures', 'where.Formal', 'where.LocalVariable', 'where.GlobalVariable',
    'control.IfCmd', 'control.WhileCmd', 'control.BreakCmd', 'control.structured-preserved',
    'visibility.hideable-axiom', 'visibility.function-definition-edge',
}


def validate_report(report, config):
    cases = report['cases']
    if [c['id'] for c in cases] != [c['id'] for c in config['cases']]:
        raise ValueError('incomplete or reordered corpus')
    for row, planned in zip(cases, config['cases']):
        if row['stratum'] != planned['stratum'] or row['roots'] != planned['roots']:
            raise ValueError('corpus identity mismatch')
        if not row['modules'] or len(row['modules']) != len(set(row['modules'])):
            raise ValueError('empty or duplicate modules')
        if not row['feature_counts'] or any(type(n) is not int or n <= 0
                                             for n in row['feature_counts'].values()):
            raise ValueError('invalid feature counts')
        if not set(row['roots']) <= row['input_sha256'].keys():
            raise ValueError('missing root identity')
    observed = {key for row in cases for key in row['feature_counts']}
    if not REQUIRED_FEATURES <= observed:
        raise ValueError('missing features: ' + ', '.join(sorted(REQUIRED_FEATURES - observed)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True, help='scratch directory inside work/')
    parser.add_argument('--binaries', type=Path, default=ROOT / 'Binaries/net10.0')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    work = args.work.resolve()
    if not work.is_relative_to(ROOT / 'work'):
        parser.error('--work must be inside this checkout\'s work directory')
    work.mkdir(parents=True, exist_ok=True)
    config_path = ROOT / '.github/review/b3-corpus.json'
    config = json.loads(config_path.read_text())
    version = subprocess.check_output(['dotnet', str(args.binaries.resolve() / 'Dafny.dll'), '--version'],
                                      cwd=ROOT, text=True).strip()
    lock = json.loads((ROOT / 'docs/dev/b3/dependencies.json').read_text())
    if version != lock['dafny']['version']:
        raise ValueError('compiler version does not match the P0 lock')
    project = ROOT / '.github/review/b3-census/Census.csproj'
    run(['dotnet', 'build', str(project), '--artifacts-path', str(work / 'build'),
         '-p:DafnyBinaries=' + str(args.binaries.resolve())], work / 'build.log')
    tool = work / 'build/bin/Census/debug/Census.dll'
    run(['dotnet', str(tool), '--self-test'], work / 'controls.log')
    std = ROOT / config['standard_library_dependencies']
    deps = sorted(p.relative_to(ROOT).as_posix() for p in std.rglob('*.dfy')
                  if 'TargetSpecific' not in p.parts)
    report = {'schema': 1, 'source_commit': subprocess.check_output(
        ['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'source_hash_encoding': 'UTF-8 with CRLF normalized to LF',
        'generator_sha256': {p.relative_to(ROOT).as_posix(): sha(p) for p in
            [Path(__file__).resolve(), project, project.with_name('Program.cs'), project.with_name('control.bpl')]},
        'compiler_version': version,
        'corpus_sha256': sha(config_path), 'language_options': config['language_options'],
        'standard_library_options': config['standard_library_options'],
        'boundary': 'typed translator AST before GetVerificationTasks; no VC generation', 'cases': []}
    for case in config['cases']:
        case_work = work / case['id']
        case_work.mkdir(exist_ok=True)
        output = case_work / 'census.json'
        inputs = list(case['roots'])
        if case['stratum'] == 'prelude':
            command = ['dotnet', str(tool), '--prelude', inputs[0], str(output)]
        else:
            dependencies = deps if case['stratum'] == 'standard-library' else []
            inputs += [d for d in dependencies if d not in inputs]
            for name, values in [('roots', case['roots']), ('dependencies', dependencies)]:
                (case_work / (name + '.json')).write_text(json.dumps(values))
            command = ['dotnet', str(tool), str(output), str(case_work / 'roots.json'),
                       str(case_work / 'dependencies.json'), str(config_path)]
        run(command, case_work / 'census.log')
        modules = json.loads(output.read_text())
        if not modules or not all(m.get('node.Axiom', 0) for m in modules.values()):
            raise ValueError('empty census: ' + case['id'])
        features = Counter()
        for module in modules.values():
            features.update(module)
        report['cases'].append({**case, 'input_sha256': {p: sha(ROOT / p) for p in inputs},
                                'modules': sorted(modules), 'feature_counts': dict(sorted(features.items()))})
        print(case['id'], len(modules), 'modules', flush=True)
    validate_report(report, config)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')


if __name__ == '__main__':
    main()
