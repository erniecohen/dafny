#!/usr/bin/env python3
"""Exact option-off diagnostics/Boogie bytes and per-VC resource identity.

Run both products on the same sources, observation paths and solver profile.
No semantic output is renamed, reordered or normalized before comparison.
The suite population comes from the standing verdict gate; Std includes all
six target-specific parts. Existing negative verdicts are compared, not blessed.
The fixed observation profile replaces RUN-line print/log destinations and
disables elapsed-time performance reports before either product is invoked.
"""
import argparse
import concurrent.futures
import glob
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shlex
import shutil
import subprocess


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def digest(data):
    return hashlib.sha256(data).hexdigest()


def resources(log):
    if not log.exists():
        return None
    return [{key: declaration.get(key) for key in ['name', 'outcome', 'resourceCount']} | {
        'vcs': [{key: vc.get(key) for key in ['vcNum', 'outcome', 'resourceCount', 'assertions', 'randomSeed']}
                for vc in declaration.get('vcResults', [])]}
            for declaration in json.loads(log.read_text())['verificationResults']]


def semantic_options(options):
    """Select observations centrally; preserve every proof-affecting option."""
    result = []
    index = 0
    while index < len(options):
        option = options[index]
        name = option.split(':')[0].split('=')[0]
        if name in ['--print', '--rprint', '--bprint', '--log-format', '--performance-stats']:
            if option == name and index + 1 < len(options) and not options[index + 1].startswith('--'):
                index += 1
        else:
            result.append(option)
        index += 1
    return result


def pair(args, name, cwd, options):
    directory = args.output / name.replace('/', '_')
    observation = directory / 'observation'
    observation.mkdir(parents=True)
    observed = []
    for variant, binary in [('base', args.base), ('candidate', args.candidate)]:
        for path in observation.iterdir():
            path.unlink()
        names = {option.split(':')[0].split('=')[0] for option in options if option.startswith('--')}
        controlled = []
        if '--cores' not in names:
            controlled += ['--cores', '1']
        if '--verification-time-limit' not in names:
            controlled += ['--verification-time-limit', '0']
        command = [str(binary), 'verify'] + controlled + [
            '--solver-path', str(args.solver),
            '--bprint', str(observation / 'program.bpl'),
            '--log-format', 'json;LogFileName=' + str(observation / 'results.json')] + options
        result = subprocess.run(command, cwd=cwd, capture_output=True, timeout=5400 if args.kind == 'std' else 900)
        (observation / 'stdout.txt').write_bytes(result.stdout)
        (observation / 'stderr.txt').write_bytes(result.stderr)
        vector = resources(observation / 'results.json')
        snapshot = {'exit': result.returncode, 'stdout': digest(result.stdout), 'stderr': digest(result.stderr),
                    'boogie': {path.name: digest(path.read_bytes()) for path in sorted(observation.glob('*.bpl'))},
                    'resources': vector}
        if result.returncode not in [0, 1, 2, 3, 4] or b'Unhandled exception' in result.stdout + result.stderr:
            raise ValueError(name + ': abnormal verifier exit')
        if b'verified' in result.stdout and vector is None:
            raise ValueError(name + ': missing verification log')
        observed.append(snapshot)
        shutil.copytree(observation, directory / variant)
        (directory / (variant + '-command.json')).write_text(json.dumps(command, indent=2) + '\n')
    equal = observed[0] == observed[1]
    report = {'name': name, 'equal': equal, 'base': observed[0], 'candidate': observed[1]}
    (directory / 'comparison.json').write_text(json.dumps(report, indent=2) + '\n')
    if equal:
        # Retain exact fingerprints and resource vectors. Raw differing bytes stay available.
        for variant in ['base', 'candidate']:
            shutil.rmtree(directory / variant)
    shutil.rmtree(observation)
    print(name, 'identical' if equal else 'DIFFERS', flush=True)
    return report


def suite(args):
    runner = load('lit-verdicts')
    plan = args.output / 'plan.tsv'
    runner.plan(str(args.sources), str(plan))
    rows = [line.split('\t', 1) for line in plan.read_text().splitlines()]
    shard, count = map(int, args.shard.split('/'))
    work = []
    for index, (source, own) in enumerate(rows):
        if index % count != shard:
            continue
        options = semantic_options(shlex.split(own))
        names = {option.split(':')[0].split('=')[0] for option in options if option.startswith('--')}
        fixed = ['--allow-warnings']
        if '--resource-limit' not in names:
            fixed += ['--resource-limit', '16000000']
        uses_library = '--standard-libraries' in names and not any(
            re.match(r'--standard-libraries[:=]false$', option) for option in options)
        if '--boogie' not in names and not uses_library:
            fixed += ['--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']
        work.append((source, [source] + fixed + options))
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        return list(executor.map(lambda item: pair(args, item[0], args.sources, item[1]), work))


def std(args):
    common = ['src/Std/dfyconfig.toml', '--boogie', '/deterministicLiteralHashes:1', '--boogie', '/restartProver']
    reports = [pair(args, 'Std', args.sources, common)]
    target_root = args.sources / 'src/Std/TargetSpecific'
    for target in ['notarget', 'cs', 'java', 'js', 'go', 'py']:
        inputs = sorted('./' + str(Path(path).relative_to(target_root))
                        for path in glob.glob(str(target_root / '**' / ('*-' + target + '*.dfy')), recursive=True))
        reports.append(pair(args, 'TargetSpecific-' + target, target_root, ['dfyconfig.toml'] + inputs))
    return reports


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('kind', choices=['suite', 'std'])
    for name in ['base', 'candidate', 'solver', 'sources', 'output']:
        parser.add_argument(name, type=lambda value: Path(value).resolve())
    parser.add_argument('--shard', default='0/1')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    reports = suite(args) if args.kind == 'suite' else std(args)
    summary = {'parts': len(reports), 'different': [report['name'] for report in reports if not report['equal']]}
    (args.output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print(summary)
    return bool(summary['different'])


if __name__ == '__main__':
    raise SystemExit(main())
