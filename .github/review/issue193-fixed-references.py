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

ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / 'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy'
COMPANION = MODEL.with_name('IntegerBitwiseCompiler.dfy')
INPUTS = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/issue193'
FUNCTIONS = dict(Power='P', Normalize='norm', And='andNat', Not='notNat', Or='orNat', Xor='xorNat', AndNot='andNotNat', LeftShift='shlNat', RightShift='shrNat', LeftRotate='rolNat', RightRotate='rorNat', Population='popNat')
FOUNDATIONS = dict(Power='n', Normalize='n,-1', And='n,5,-3', Not='n,-1',
                   Or='n,5,-3', Xor='n,5,-3', AndNot='n,-1,3', LeftShift='n,-1,-1',
                   RightShift='n,-1,n', LeftRotate='n,-1,-1', RightRotate='n,-1,n', Population='n,-1')


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

    def verify(path, name, errors=(), extra=(), limit=200000):
        log = out / (name + '.csv')
        command = ['verify', str(path), *common, '--resource-limit', str(limit),
                   '--log-format', 'csv;LogFileName=' + str(log), *extra]
        text = invoke(command, name, 4 if errors else 0)
        actual = [(int(line), message) for line, message in re.findall(r'\((\d+),\d+\): Error: ([^\n]+)', text)]
        assert sorted(actual) == sorted(errors), (name, actual, errors, text)
        with log.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        assert rows and all(r['TestResult.Outcome'] in ('Passed', 'Failed') for r in rows), (name, rows)
        assert sum(r['TestResult.Outcome'] == 'Failed' for r in rows) == len(errors), (name, rows)
        return rows

    def source(name, lines):
        path = out / (name + '.dfy')
        path.write_text('\n'.join(lines) + '\n')
        return path

    header = ['include "' + os.path.relpath(COMPANION, out) + '"',
              'import B = Std.Arithmetic.IntegerBitwiseCompiler']
    checked_source = re.sub(r'//[^\n]*', '', COMPANION.read_text())
    assert not re.search(r'\b(assume|expect)\b|\{:\s*(axiom|verify|extern)', checked_source)
    lemmas = set(re.findall(r'\blemma\s+(?:\{:[^}]*\}\s*)*(\w+)\s*\(', checked_source))
    assert set(FOUNDATIONS) <= lemmas
    # Positive proofs check every body, including M1, without assuming consumer ranges.
    verify(COMPANION, 'bootstrap', extra=['--verify-included-files'], limit=2000000)
    emission = source('emission', ['lemma Emit() ensures IntegerBitwiseModel.P(0) == 1',
                                  '{ IntegerBitwiseModel.unfoldP(0); }'])
    bpl = out / 'emission.bpl'
    verify(emission, 'emission', extra=['--bprint', str(bpl)])
    load('slices', INPUTS / 'unfold-slices.py').check(bpl, MODEL, args.z3.resolve(), out)

    # Every fixed equation is active via its foundation call, followed by false
    # and by a wrong result. The latter is a direct disagreement with the model.
    for axioms in (False, True):
        lines, errors = list(header), []
        for n in ((0, 1, 3, 8, 16, 32, 64) if args.full else (0, 3, 64)):
            for name, parameters in FOUNDATIONS.items():
                for wrong in (False, True):
                    lines += [f'lemma {{:induction false}} {name}_{n}_{wrong}() {{', f'  var n := {n};',
                              f'  B.{name}({parameters});']
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
    cumulative, errors = list(header), []
    for wrong in (False, True):
        cumulative += [f'lemma LambdaCumulative_{wrong}() {{', '  var n := 3;',
                       '  var f := (x:int) requires x > 0 => x;', '  assert f.requires(1);']
        cumulative += [f'  B.{name}({parameters});' for name, parameters in FOUNDATIONS.items()]
        cumulative += ['  assert IntegerBitwiseModel.P(3) != B.P(3);' if wrong else '  assert false;']
        errors.append((len(cumulative), 'assertion might not hold'))
        cumulative += ['}']
    verify(source('lambda-cumulative', cumulative), 'lambda-cumulative', errors, ['--additional-axioms:true'])

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

    # An off-built user library may spell the same names, but gets no privilege.
    spoof_library = out / 'spoof.doo'
    invoke(['build', str(spoof), '--target:lib', *common, '--integer-bitwise:false', '--output', str(spoof_library)], 'spoof-library-build')
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
