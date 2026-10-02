#!/usr/bin/env python3
"""Retain the original issue's proof results and actual Boogie/SMT inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

def run_probe(cmd, **kwargs):
    try:
        return subprocess.run(cmd, **kwargs)
    except subprocess.TimeoutExpired as error:
        decode = lambda value: value.decode(errors='replace') if isinstance(value, bytes) else (value or '')
        return subprocess.CompletedProcess(cmd, -1, decode(error.stdout), decode(error.stderr) + '\nPROBE SAFETY TIMEOUT\n')

p = argparse.ArgumentParser()
p.add_argument('--baseline', required=True)
p.add_argument('--candidate')
p.add_argument('--z3', required=True)
p.add_argument('--output', required=True)
p.add_argument('--stress', action='store_true')
a = p.parse_args()
out = Path(a.output).resolve()
out.mkdir(parents=True, exist_ok=True)
source = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-33-original.dfy').resolve()
metadata = {'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'z3_sha256': hashlib.sha256(Path(a.z3).read_bytes()).hexdigest(), 'z3_version': subprocess.check_output([a.z3, '--version'], text=True).strip(), 'builds': {}}
for label, binary, flag in [('B', a.baseline, []), *([('O', a.candidate, []), ('F', a.candidate, ['--additional-axioms=false']), ('E', a.candidate, ['--additional-axioms'])] if a.candidate else [])]:
    dest = out / label
    dest.mkdir(exist_ok=True)
    metadata['builds'][label] = {'version': subprocess.check_output([binary, '--version'], text=True).strip(), 'core_sha256': hashlib.sha256(Path(binary).with_name('DafnyCore.dll').read_bytes()).hexdigest()}
    cmd = [binary, 'verify', str(source), '--solver-path', a.z3, '--cores', '1', '--resource-limit', '20000000', '--verification-time-limit', '0', '--boogie', '/normalizeDeclarationOrder:0', '--log-format', 'json;LogFileName=' + str(dest / 'results.json'), '--log-format', 'csv;LogFileName=' + str(dest / 'results.csv'), '--bprint', str(dest / 'program.bpl'), '--solver-log', str(dest / 'solver.smt2')] + flag
    (dest / 'command.json').write_text(json.dumps(cmd, indent=2))
    result = run_probe(cmd, capture_output=True, text=True, timeout=1200)
    (dest / 'output.txt').write_text(result.stdout + result.stderr)
    (dest / 'exit-code.txt').write_text(str(result.returncode))
    print(label, result.returncode, result.stdout[-250:], flush=True)
(out / 'metadata.json').write_text(json.dumps(metadata, indent=2))

# These are probes: every outcome is saved for review, including intentional failures.
if a.candidate:
    for resolver in ['false', 'true']:
        for name in ['original', 'positive', 'negative']:
            dest = out / ('test-' + resolver + '-' + name)
            dest.mkdir(exist_ok=True)
            input_file = source.with_name('github-issue-33-' + name + '.dfy')
            cmd = [a.candidate, 'verify', str(input_file), '--solver-path', a.z3, '--cores', '1', '--resource-limit', '200000', '--verification-time-limit', '0', '--additional-axioms', '--type-system-refresh=' + resolver, '--show-snippets=false', '--use-basename-for-filename', '--error-limit=0', '--log-format', 'json;LogFileName=' + str(dest / 'results.json'), '--log-format', 'csv;LogFileName=' + str(dest / 'results.csv'), '--bprint', str(dest / 'program.bpl'), '--solver-log', str(dest / 'solver.smt2'), '--solver-option', 'O:smt.qi.profile=true', '--boogie', '/emitDebugInformation:1']
            (dest / 'command.json').write_text(json.dumps(cmd, indent=2))
            result = run_probe(cmd, capture_output=True, text=True, timeout=1200)
            (dest / 'stdout.txt').write_text(result.stdout)
            (dest / 'stderr.txt').write_text(result.stderr)
            (dest / 'exit-code.txt').write_text(str(result.returncode))
            print(resolver, name, result.returncode, result.stdout[-500:], flush=True)
    # Ordinary project parsing, inheritance, and explicit CLI false override.
    config_dir = out / 'config'
    config_dir.mkdir(exist_ok=True)
    (config_dir / 'source.dfy').write_text('lemma RoundTrip(a: int) requires 0 <= a < 4294967296 ensures (a as bv32) as int == a {}')
    (config_dir / 'base.toml').write_text('includes = ["source.dfy"]\n[options]\nadditional-axioms = true\n')
    (config_dir / 'dfyconfig.toml').write_text('base = "base.toml"\n')
    for name, extra in [('inherited-true', []), ('override-false', ['--additional-axioms=false'])]:
        cmd = [a.candidate, 'verify', str(config_dir / 'dfyconfig.toml'), '--solver-path', a.z3, '--cores', '1', '--resource-limit', '200000', '--verification-time-limit', '0'] + extra
        result = run_probe(cmd, capture_output=True, text=True, timeout=1200)
        (config_dir / (name + '.txt')).write_text(result.stdout + result.stderr)
        (config_dir / (name + '.exit')).write_text(str(result.returncode))
        print(name, result.returncode, result.stdout[-150:], flush=True)
    # Randomization uses this branch's actual command spelling, --mutations.
    for isolate in [False, True]:
        dest = out / ('random-isolated' if isolate else 'random-original')
        dest.mkdir(exist_ok=True)
        cmd = [a.candidate, 'measure-complexity', str(source), '--additional-axioms', '--solver-path', a.z3, '--cores', '1', '--resource-limit', '200000', '--verification-time-limit', '0', '--mutations', '10', '--log-format', 'csv;LogFileName=' + str(dest / 'results.csv')]
        if isolate: cmd.append('--isolate-assertions')
        result = run_probe(cmd, capture_output=True, text=True, timeout=1200)
        (dest / 'output.txt').write_text(result.stdout + result.stderr)
        (dest / 'exit-code.txt').write_text(str(result.returncode))
        print(dest.name, result.returncode, result.stdout[-200:], flush=True)
    for extra in [['--disable-nonlinear-arithmetic'], ['--manual-triggers']]:
        dest = out / extra[0][2:]
        dest.mkdir(exist_ok=True)
        cmd = [a.candidate, 'verify', str(source), '--additional-axioms', '--solver-path', a.z3, '--cores', '1', '--resource-limit', '200000', '--verification-time-limit', '0', '--log-format', 'csv;LogFileName=' + str(dest / 'results.csv')] + extra
        result = run_probe(cmd, capture_output=True, text=True, timeout=1200)
        (dest / 'output.txt').write_text(result.stdout + result.stderr)
        (dest / 'exit-code.txt').write_text(str(result.returncode))
        print(dest.name, result.returncode, result.stdout[-150:], flush=True)
    if a.stress:
        stress = out / 'stress'
        stress.mkdir(exist_ok=True)
        cases = {}
        for count in [1, 10, 100, 1000]:
            parameters = ', '.join('a' + str(i) + ': int' for i in range(count))
            conditions = '\n'.join('  requires 0 <= a{0} < 4294967296\n  ensures (a{0} as bv32) as int == a{0}'.format(i) for i in range(count))
            cases['distinct-' + str(count)] = 'lemma Stress(' + parameters + ')\n' + conditions + '\n{}\n'
        for depth in [1, 4, 16, 64]:
            term = 'a'
            for i in range(depth): term = '((' + term + ' as bv32) as int)'
            cases['nested-' + str(depth)] = 'lemma Stress(a: int) requires 0 <= a < 4294967296 ensures ' + term + ' == a {}'
        for count in [1, 4, 16, 64]:
            cases['widths-' + str(count)] = '\n'.join('lemma Width{0}(a: int) requires 0 <= a < {1} ensures (a as bv{0}) as int == a {{}}'.format(w, 1 << w) for w in range(1, count + 1))
        cases['wide-1024'] = 'lemma Stress(a: int) requires 0 <= a < ' + str(1 << 1024) + ' ensures (a as bv1024) as int == a {}'
        cases['integer-only'] = 'lemma Stress(a: int) ensures a + 0 == a {}'
        cases['pure-bv'] = 'lemma Stress(a: bv32) ensures a ^ 0 == a {}'
        cases['to-only'] = 'lemma Stress(a: int) requires 0 <= a < 4294967296 ensures a as bv32 == a as bv32 {}'
        cases['from-only'] = 'lemma Stress(a: bv32) ensures 0 <= a as int < 4294967296 {}'
        for name, text in cases.items():
            input_file = stress / (name + '.dfy')
            input_file.write_text(text)
            cmd = [a.candidate, 'verify', str(input_file), '--additional-axioms', '--solver-path', a.z3, '--cores', '1', '--resource-limit', '20000000', '--verification-time-limit', '0', '--log-format', 'csv;LogFileName=' + str(stress / (name + '.csv')), '--solver-option', 'O:smt.qi.profile=true', '--boogie', '/emitDebugInformation:1']
            result = run_probe(cmd, capture_output=True, text=True, timeout=1200)
            (stress / (name + '.output')).write_text(result.stdout + result.stderr)
            (stress / (name + '.exit')).write_text(str(result.returncode))
            print(name, result.returncode, result.stdout[-150:], flush=True)
