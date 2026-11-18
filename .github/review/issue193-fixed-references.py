#!/usr/bin/env python3
"""M2 emission, bootstrap, identity, and live-control gates (fail closed)."""
import argparse
import csv
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy'
COMPANION = MODEL.with_name('IntegerBitwiseCompiler.dfy')
FOUNDATION = MODEL.with_name('IntegerBitwiseCompilerFoundation.dfy')
INPUTS = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/issue193'
FUNCTIONS = dict(Power='P', Normalize='norm', And='andNat', Not='notNat', Or='orNat', Xor='xorNat', AndNot='andNotNat', LeftShift='shlNat', RightShift='shrNat', LeftRotate='rolNat', RightRotate='rorNat', Population='popNat')
FOUNDATIONS = dict(Power='n', Normalize='n,-1', And='n,5,-3', Not='n,-1',
                   Or='n,5,-3', Xor='n,5,-3', AndNot='n,-1,3', LeftShift='n,-1,0',
                   RightShift='n,-1,0', LeftRotate='n,-1,-1', RightRotate='n,-1,n', Population='n,-1')


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dafny', required=True, type=Path)
    parser.add_argument('--z3', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--resolver', choices=['legacy', 'refreshed'], default='refreshed')
    parser.add_argument('--full', action='store_true')
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    dafny = args.dafny.resolve()
    executable = ['dotnet', str(dafny)] if dafny.suffix == '.dll' else [str(dafny)]
    common = ['--solver-path', str(args.z3.resolve()), '--cores', '1', '--standard-libraries:false',
              '--integer-bitwise', '--allow-warnings:false', '--show-snippets:false',
              '--type-system-refresh:' + str(args.resolver == 'refreshed').lower(),
              '--resource-limit', '200000', '--verification-time-limit', '60',
              '--error-limit', '0', '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver']
    checks = []

    def invoke(command, name, expected=0, contains=()):
        result = subprocess.run(executable + command, capture_output=True, text=True, timeout=1200)
        text = result.stdout + result.stderr
        (out / (name + '.log')).write_text(text)
        assert result.returncode == expected, (name, result.returncode, text)
        assert not re.search(r'out of resource|timed out|inconclusive|unknown|Warning:', text, re.I), (name, text)
        for message in contains:
            assert message in text, (name, message, text)
        checks.append(name)
        return text

    def verify(path, name, errors=(), extra=(), limit=200000, failed_scopes=None):
        log = out / (name + '.csv')
        command = ['verify', str(path), *common, '--resource-limit', str(limit),
                   '--log-format', 'csv;LogFileName=' + str(log), *extra]
        text = invoke(command, name, 4 if errors else 0)
        actual = [(int(line), message) for line, message in re.findall(r'\((\d+),\d+\): Error: ([^\n]+)', text)]
        assert sorted(actual) == sorted(errors), (name, actual, errors, text)
        with log.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        assert sum(r['TestResult.Outcome'] == 'Failed' for r in rows) == (len(errors) if failed_scopes is None else failed_scopes), (name, rows)
        return rows

    def source(name, lines):
        path = out / (name + '.dfy')
        path.write_text('\n'.join(lines) + '\n')
        return path

    def activate(name, parameters):
        lines = ['  B.Power(n);', f'  B.{name}({parameters});']
        if name == 'RightShift':
            lines += ['  B.Power(0);']
        elif name == 'LeftRotate':
            lines += ['  if n > 0 { B.Power(n-(-1)%n); }']
        elif name == 'RightRotate':
            lines += ['  if n > 0 { B.Power(n%n); }']
        function = FUNCTIONS[name]
        unfold = 'unfold' + function[0].upper() + function[1:].replace('Nat', '')
        lines += [f'  IntegerBitwiseModel.{unfold}({parameters});']
        return lines

    header = ['include "' + os.path.relpath(COMPANION, out) + '"',
              'import B = Std.Arithmetic.IntegerBitwiseCompiler']
    checked_source = re.sub(r'//[^\n]*', '', FOUNDATION.read_text() + COMPANION.read_text())
    assert not re.search(r'\b(assume|expect)\b|\{:\s*(axiom|verify|extern)', checked_source)
    lemmas = set(re.findall(r'\blemma\s+(?:\{:[^}]*\}\s*)*(\w+)\s*\(', checked_source))
    assert set(FOUNDATIONS) <= lemmas
    # This input contains only M1 and raw-reference agreement proofs, with no
    # consumer contracts in scope. Verify every body before checking consumers.
    verify(FOUNDATION, 'foundation', extra=['--verify-included-files'], limit=2000000)
    verify(COMPANION, 'bootstrap', limit=2000000)
    emission = source('emission', ['lemma Emit() ensures IntegerBitwiseModel.P(0) == 1',
                                  '{ IntegerBitwiseModel.unfoldP(0); }'])
    bpl = out / 'emission.bpl'
    verify(emission, 'emission', extra=['--bprint', str(bpl)])
    load('slices', INPUTS / 'unfold-slices.py').check(bpl, MODEL, args.z3.resolve(), out)

    # Each assertion site directly calls its fixed unfold lemma, after obtaining
    # the required raw power positivity from the checked foundation.
    for axioms in (False, True):
        lines, errors = list(header), []
        for n in ((0, 1, 3, 8, 16, 32, 64) if args.full else (0, 3, 64)):
            for name, parameters in FOUNDATIONS.items():
                for wrong in (False, True):
                    lines += [f'lemma {{:induction false}} {name}_{n}_{wrong}() {{', f'  var n := {n};']
                    lines += activate(name, parameters)
                    # Disagree with this particular checked reference function.
                    function = FUNCTIONS[name]
                    lines += [f'  assert B.{function}({parameters}) != IntegerBitwiseModel.{function}({parameters});' if wrong else '  assert false;']
                    errors.append((len(lines), 'assertion might not hold'))
                    lines += ['}']
        path = source('unfold-live-' + str(axioms), lines)
        verify(path, path.stem, errors, ['--additional-axioms:' + str(axioms).lower()])

    # Guarded branches must not expose n-1 or k%n in the width-zero case.
    guarded = source('guards', [
        'lemma Endpoints() {',
        '  IntegerBitwiseModel.unfoldAnd(0,-1,-3);',
        '  IntegerBitwiseModel.unfoldPop(0,-1);',
        '  IntegerBitwiseModel.unfoldShl(0,-1,-1);',
        '  IntegerBitwiseModel.unfoldShr(0,-1,0);',
        '  IntegerBitwiseModel.unfoldRol(0,-1,-1);',
        '  IntegerBitwiseModel.unfoldRor(0,-1,0);',
        '  assert IntegerBitwiseModel.andNat(0,-1,-3) == 0;',
        '  assert IntegerBitwiseModel.popNat(0,-1) == 0;',
        '  assert IntegerBitwiseModel.shlNat(0,-1,-1) == 0;',
        '  assert IntegerBitwiseModel.shrNat(0,-1,0) == 0;',
        '  assert IntegerBitwiseModel.rolNat(0,-1,-1) == 0;',
        '  assert IntegerBitwiseModel.rorNat(0,-1,0) == 0;', '}'])
    verify(guarded, 'guards')
    # Positivity must be obtained from the foundation, not a raw signature.
    raw = source('raw', ['lemma Raw(n:nat) {', '  assert 0 < IntegerBitwiseModel.P(n);', '}',
                         'lemma RawRange(n:nat,a:int,b:int) {', '  assert 0 <= IntegerBitwiseModel.andNat(n,a,b);', '}'])
    verify(raw, 'raw', [(2, 'assertion might not hold'), (5, 'assertion might not hold')])
    # Without an unfold call, a fixed UF has no available recurrence.
    finite = source('finite', ['lemma Finite() {', '  assert IntegerBitwiseModel.P(0) == 1;', '}'])
    verify(finite, 'finite', [(2, 'assertion might not hold')])
    verify(INPUTS / 'definedness.dfy', 'definedness',
           [(line, 'a precondition for this call could not be proved') for line in range(1, 6)] +
           [(6, "value does not satisfy the subset constraints of 'nat'")])
    cumulative, errors, reverse_errors = [], [], []
    for wrong in (False, True):
        cumulative += [f'lemma LambdaCumulative_{wrong}(x:int) {{', '  var n := 3;',
                       '  var f := (y:int) requires y > 0 => y;', '  assert f.requires(1);',
                       '  assert f.requires(x) ==> x > 0;']
        reverse_errors.append((len(cumulative), 'assertion might not hold'))
        # Establish concrete power denominators by the actual raw equations.
        # This scalar control has no dependency on the sequence proof companion.
        cumulative += [f'  IntegerBitwiseModel.unfoldP({width});' for width in range(4)]
        for name, parameters in FOUNDATIONS.items():
            function = FUNCTIONS[name]
            unfold = 'unfold' + function[0].upper() + function[1:].replace('Nat', '')
            cumulative += [f'  IntegerBitwiseModel.{unfold}({parameters});']
        cumulative += ['  assert IntegerBitwiseModel.P(3) != 8;' if wrong else '  assert false;']
        errors.append((len(cumulative), 'assertion might not hold'))
        cumulative += ['}']
    lambda_path = source('lambda-cumulative', cumulative)
    verify(lambda_path, 'lambda-cumulative', errors, ['--additional-axioms:true'])
    # This reverse domain observation is the actual missing fact from issue 97.
    # OFF must lose it while still rejecting the final false/wrong assertions.
    verify(lambda_path, 'lambda-cumulative-off', errors + reverse_errors, ['--additional-axioms:false'], failed_scopes=2)

    # Both options off / bridge option alone must not install a public signature.
    for axioms in (False, True):
        invoke(['verify', str(emission), *common, '--integer-bitwise:false', '--additional-axioms:' + str(axioms).lower()], 'off-' + str(axioms), 2, ['unresolved identifier: IntegerBitwiseModel'])
    # User functions of the same spelling remain ordinary source declarations.
    spoof = source('spoof', ['module Spoof {', '  class IntegerBitwiseModel {',
                            '    static ghost function {:integerBitwiseIntrinsic "P"} P(n:nat):int { 7 }',
                            '    static lemma unfoldP(n:nat) ensures P(n)==7 { }', '  }',
                            '}', 'import S = Spoof', 'lemma User() { S.IntegerBitwiseModel.unfoldP(0); assert S.IntegerBitwiseModel.P(0)==7; }'])
    verify(spoof, 'source-spoof')

    prelude = dafny.parent / 'DafnyPrelude.bpl'
    collision = out / 'collision.bpl'
    collision.write_text(prelude.read_text() + '\nfunction _System.IntegerBitwiseModel.P(n:int):int;\n')
    invoke(['verify', str(emission), *common, '--prelude', str(collision)],
           'prelude-collision', 2, ["custom prelude declares reserved integer-bitwise symbol '_System.IntegerBitwiseModel.P'"])

    # Enabled .doo requires enabled clients; reconstruction uses installed builtins.
    library_source = source('library', ['module FixedLibrary {',
        ' lemma Zero() ensures IntegerBitwiseModel.P(0)==1 { IntegerBitwiseModel.unfoldP(0); }', '}'])
    library = out / 'library.doo'
    invoke(['build', str(library_source), '--target:lib', *common, '--output', str(library)], 'library-build')
    client = source('client', ['import L = FixedLibrary', 'lemma Client() ensures IntegerBitwiseModel.P(0)==1 { L.Zero(); }'])
    verify(client, 'library-reload', extra=['--library', str(library)])
    invoke(['resolve', str(client), '--standard-libraries:false', '--library', str(library)],
           'library-disabled', 1, ['--integer-bitwise is set locally to False, but the library was built with True'])

    # FileVersion alone does not identify fork reference equations. Missing or
    # different product-version bindings must not import their proof contracts.
    with zipfile.ZipFile(library) as archive:
        manifest = archive.read('manifest.toml').decode()
        binding = re.search(r'^integer_bitwise_reference_version = "([^"]+)"$', manifest, re.M)
        assert binding, manifest
        for name, replacement in [('missing', ''), ('mismatch', 'integer_bitwise_reference_version = "different-product-version"')]:
            incompatible = out / (name + '.doo')
            with zipfile.ZipFile(incompatible, 'w') as target:
                for entry in archive.infolist():
                    contents = archive.read(entry)
                    if entry.filename == 'manifest.toml':
                        contents = (manifest[:binding.start()] + replacement + manifest[binding.end():]).encode()
                    target.writestr(entry, contents)
            invoke(['resolve', str(client), '--integer-bitwise', '--standard-libraries:false',
                    '--library', str(incompatible)],
                   'library-abi-' + name, 1, ['integer-bitwise reference ABI does not match this Dafny product version; rebuild the library with this version'])

    # An off-built user library may spell the same names, but gets no privilege.
    spoof_library = out / 'spoof.doo'
    invoke(['build', str(spoof), '--target:lib', *common, '--integer-bitwise:false', '--output', str(spoof_library)], 'spoof-library-build')
    with zipfile.ZipFile(spoof_library) as archive:
        manifest = archive.read('manifest.toml').decode()
        assert 'integer_bitwise_reference_version' not in manifest and 'integer-bitwise' not in manifest, manifest
    spoof_client = source('spoof-client', ['import T = Spoof',
        'lemma SpoofClient() { T.IntegerBitwiseModel.unfoldP(0); assert T.IntegerBitwiseModel.P(0)==7; }',
        'lemma FixedClient() { IntegerBitwiseModel.unfoldP(0); assert IntegerBitwiseModel.P(0)==1; }'])
    verify(spoof_client, 'spoof-library-reload', extra=['--library', str(spoof_library)])

    if args.full:
        m1 = load('m1', ROOT / '.github/review/issue193-reference.py')
        m1.inventory()
        m1.MODEL = COMPANION
        for width in (8, 16, 32, 64):
            for negative in (False, True):
                path = out / f'mapped-{width}-{negative}.dfy'
                bad = m1.materialize(path, m1.CALLS, width, negative)
                path.write_text(path.read_text().replace('Std.Arithmetic.IntegerBitwise\n', 'Std.Arithmetic.IntegerBitwiseCompiler\n'))
                verify(path, path.stem, [(line, 'assertion might not hold') for line in bad], limit=200000 if negative else 2000000)
            path = out / f'clients-{width}.dfy'
            m1.symbolic_clients(path, MODEL, width)
            path.write_text(path.read_text().replace('Std.Arithmetic.IntegerBitwise\n', 'Std.Arithmetic.IntegerBitwiseCompiler\n'))
            verify(path, path.stem, limit=2000000)
    (out / 'summary.json').write_text(json.dumps({'checks': checks, 'passed': True}, indent=2) + '\n')
    print(f'{len(checks)} fixed-reference checks passed ({args.resolver})')


if __name__ == '__main__':
    main()
