#!/usr/bin/env python3
"""Explicit B3 developer controls; this file is not part of any default build."""
import argparse
import csv
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
sys.dont_write_bytecode = True

from provenance import validate

ROOT = Path(__file__).resolve().parents[1]
BUILD_SUMMARY = re.compile(r" error |Error\(s\)|Warning\(s\)|Build succeeded")


def run(command, log, env, expected=0, data=None, build=False):
    result = subprocess.run([str(x) for x in command], input=data, capture_output=True, env=env, timeout=600)
    output = result.stdout + result.stderr
    log.write_bytes(output)
    log.with_suffix(log.suffix + '.command.json').write_text(json.dumps([str(x) for x in command]))
    if build:
        for line in output.decode(errors='replace').splitlines():
            if BUILD_SUMMARY.search(line):
                print(line, flush=True)
    if result.returncode != expected:
        raise RuntimeError(f"{log.name}: exit {result.returncode}, expected {expected}")
    return result.returncode, result.stdout, result.stderr


def compile_program(dafny, project, destination, native, env, allow_warnings=False):
    destination.mkdir(parents=True, exist_ok=True)
    command = dafny + ['translate', 'cs', project, '--no-verify', '--include-runtime', '--output', destination / 'Program.cs']
    if allow_warnings:
        command.append('--allow-warnings')
    run(command,
        destination / 'translate.log', env)
    # Compile separately so the audit setting is an explicit MSBuild argument.
    csproj = '''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS8981</NoWarn></PropertyGroup>
<ItemGroup><Compile Include="Program.cs"/><Compile Include="NATIVE/*.cs"/>
<PackageReference Include="xunit" Version="2.4.2"/></ItemGroup></Project>'''
    (destination / 'Program.csproj').write_text(csproj.replace('NATIVE', str(native)))
    run([dafny[0], 'build', destination / 'Program.csproj', '-c', 'Release', '-m:1', '-p:NuGetAudit=false'],
        destination / 'build.log', env, build=True)
    return [dafny[0], destination / 'bin/Release/net10.0/Program.dll']


def controls(program, work, env):
    positive = ['type-check-parameter', 'type-check-local', 'positive-substitution']
    rejected = ['missing-type', 'missing-get', 'missing-variable', 'missing-function',
                'missing-let', 'missing-quantifier', 'closure', 'verifier-parameter',
                'verifier-local', 'verifier-loop', 'verifier-call']
    rows = []
    for mode in positive + rejected:
        expected = 0 if mode in positive else 1
        result = subprocess.run([str(x) for x in program + [mode]], capture_output=True, env=env, timeout=60)
        (work / (mode + '.stdout')).write_bytes(result.stdout)
        (work / (mode + '.stderr')).write_bytes(result.stderr)
        passed = (result.returncode == 0 and b'CONTROL PASSED' in result.stdout) if expected == 0 else (result.returncode != 0 and b'CONTROL PASSED' not in result.stdout)
        if expected:
            file = 'Verifier/Incarnations.dfy' if not mode.startswith('verifier-') else 'Verifier/Verifier.dfy'
            marker = {
                'missing-type': ('static method Type2STypeWithMap', 'expect decl in typeMap'),
                'missing-get': ('method Get(', 'expect v in m'),
                'missing-variable': ('method SubstituteVariable(', 'expect v in m'),
                'missing-function': ('case FunctionCallExpr(func, args)', 'expect func in declMap.functionMap'),
                'missing-let': ('method Substitute(expr', 'expect v in m'),
                'missing-quantifier': ('method Substitute(expr', 'expect forall v <- vv'),
                'closure': ('method Substitute(expr', 'expect false'),
                'verifier-parameter': ('method AssumeAutoInvariant(', 'expect autoInv.WellFormed()'),
                'verifier-local': ('method Process(', 'expect autoInv.WellFormed()'),
                'verifier-loop': ('method ProcessLoop(', 'expect autoInv.WellFormed()'),
                'verifier-call': ('method ProcessCall(', 'expect AstValid.ProcedureHeader(proc)'),
            }[mode]
            source = (ROOT / 'src' / file).read_text()
            offset = source.index(marker[1], source.index(marker[0]))
            line = source[:offset].count('\n') + 1
            expected_location = Path(file).name + '(' + str(line) + ','
            passed = passed and expected_location.encode() in result.stdout and b'[Program halted]' in result.stdout
        rows.append({'mode': mode, 'exit': result.returncode, 'passed': passed})
        print(mode, 'passed' if passed else 'FAILED', flush=True)
    fixture = work / 'response.py'
    for mode, lines in [('response-balanced', '(\n)\n'), ('response-limit', '(\n(\n(\n')]:
        fixture.write_text('import sys\nsys.stdout.write(' + repr(lines) + ')\n')
        args = program + [mode, sys.executable, shlex.quote(str(fixture))]
        result = subprocess.run([str(x) for x in args], capture_output=True, env=env, timeout=60)
        (work / (mode + '.stdout')).write_bytes(result.stdout)
        (work / (mode + '.stderr')).write_bytes(result.stderr)
        passed = result.returncode == 0 and b'CONTROL PASSED' in result.stdout
        rows.append({'mode': mode, 'exit': result.returncode, 'passed': passed})
        print(mode, 'passed' if passed else 'FAILED', flush=True)
    (work / 'controls.json').write_text(json.dumps(rows, indent=2) + '\n')
    if not all(row['passed'] for row in rows):
        raise RuntimeError('runtime control failure')
    return rows


def normalize_debug_maps(output):
    # P0 prints sets/maps in their unspecified enumeration order. Preserve
    # every section and value; sort only entries of explicit debug map blocks.
    lines = output.decode().splitlines(keepends=True)
    result = []
    index = 0
    while index < len(lines):
        result.append(lines[index])
        if lines[index].rstrip() in ('  nextSequenceCount:', '  m:'):
            index += 1
            entries = []
            while index < len(lines) and lines[index].startswith('    '):
                entries.append(lines[index])
                index += 1
            result.extend(sorted(entries))
        else:
            index += 1
    return ''.join(result).encode()


def corpus(program, baseline, work, env):
    rows = []
    for index, source in enumerate(sorted((ROOT / 'test').rglob('*.b3'))):
        line = source.read_text().splitlines()[0]
        command = line.removeprefix('// RUN: ')
        stdin = ' | ' in command
        if stdin:
            command = command.split(' | ', 1)[1]
        args = shlex.split(command.split(' > ', 1)[0])
        name = args.pop(0)
        if name == '%verify':
            args.insert(0, 'verify')
        args = [str(source) if x == '%s' else x for x in args]
        data = source.read_bytes() if stdin else None
        result = run(program + args, work / f'corpus-{index}.log', env, data=data)
        old = run(baseline + args, work / f'corpus-{index}-base.log', env, data=data)
        exact = result == old
        same = exact
        if source.name == 'incarnations.b3' and not exact:
            same = result[0] == old[0] and result[2] == old[2] and normalize_debug_maps(result[1]) == normalize_debug_maps(old[1])
        rows.append({'source': str(source.relative_to(ROOT)), 'same': same, 'byte_identical': exact})
        print(source.relative_to(ROOT), 'same bytes' if exact else ('same debug bindings' if same else 'DIFFERENT'), flush=True)
    (work / 'corpus.json').write_text(json.dumps(rows, indent=2) + '\n')
    if not all(row['same'] for row in rows):
        raise RuntimeError('B3 runtime difference')
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dafny-dll', required=True, type=Path)
    parser.add_argument('--solver', required=True, type=Path)
    parser.add_argument('--work', required=True, type=Path)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--upstream', type=Path, help='Optional exact P0 checkout for runtime comparison')
    parser.add_argument('--skip-proof', action='store_true', help='Compilation/runtime checks only; never proof acceptance')
    args = parser.parse_args()
    validate(args.upstream.resolve() if args.upstream else None)
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'tmp').mkdir(exist_ok=True)
    solver = args.solver.absolute()
    env = dict(os.environ, TMPDIR=str(work / 'tmp'), PATH=str(solver.parent) + os.pathsep + os.environ['PATH'])
    dafny = [args.dotnet, str(args.dafny_dll.resolve())]
    flags = ['--cores', '2', '--solver-path', solver, '--resource-limit', '100000000', '--verification-time-limit', '120']
    if not args.skip_proof:
        run(dafny + ['verify', ROOT / 'src/dfyconfig.toml'] + flags + ['--log-format', 'csv;LogFileName=' + str(work / 'bootstrap.csv')], work / 'bootstrap.log', env)
        rows = list(csv.DictReader((work / 'bootstrap.csv').open()))
        if not rows or any(row['TestResult.Outcome'] != 'Passed' for row in rows):
            raise RuntimeError('incomplete bootstrap')
        run(dafny + ['verify', ROOT / 'controls/dfyconfig.toml', '--filter-symbol', 'CheckedB3Controls'] + flags, work / 'control-proof.log', env)
        if b'2 verified, 0 errors' not in (work / 'control-proof.log').read_bytes():
            raise RuntimeError('unexpected positive control denominator')
        run(dafny + ['verify', ROOT / 'controls/dfyconfig.toml', ROOT / 'controls/Vacuity.dfy', '--filter-symbol', 'CheckedLookupVacuity'] + flags,
            work / 'vacuity.log', env, expected=4)
        if b'0 verified, 3 errors' not in (work / 'vacuity.log').read_bytes():
            raise RuntimeError('unexpected vacuity denominator')
    product = compile_program(dafny, ROOT / 'src/dfyconfig.toml', work / 'product', ROOT / 'target/cs/src', env)
    control = compile_program(dafny, ROOT / 'controls/dfyconfig.toml', work / 'control', ROOT / 'target/cs/src', env)
    controls(control, work, env)
    if args.upstream:
        baseline = compile_program(dafny, args.upstream.resolve() / 'src/dfyconfig.toml', work / 'baseline', args.upstream.resolve() / 'target/cs/src', env, allow_warnings=True)
        corpus(product, baseline, work, env)


if __name__ == '__main__':
    main()
