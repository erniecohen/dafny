#!/usr/bin/env python3
"""Run serial mutations of the repaired B3 trust and termination sites."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('b3check', ROOT / 'controls/check.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)

# id, source, exact text removed/replaced, replacement, focused proof symbol
MUTATIONS = [
    ('type-map', 'Verifier/Incarnations.dfy', '        expect decl in typeMap, decl.Name;\n', '', 'Incarnations.DeclMappings.Type2STypeWithMap'),
    ('get-map', 'Verifier/Incarnations.dfy', '    method Get(v: Variable) returns (r: SConstant) {\n      expect v in m, v.name;', '    method Get(v: Variable) returns (r: SConstant) {', 'Incarnations.Incarnations.Get'),
    ('variable-map', 'Verifier/Incarnations.dfy', '    method SubstituteVariable(v: Variable) returns (r: SConstant) {\n      expect v in m, v.name;', '    method SubstituteVariable(v: Variable) returns (r: SConstant) {', 'Incarnations.Incarnations.SubstituteVariable'),
    ('function-map', 'Verifier/Incarnations.dfy', '        expect func in declMap.functionMap, func.Name;\n', '', 'Incarnations.Incarnations.Substitute.'),
    ('let-map', 'Verifier/Incarnations.dfy', '      case LetExpr(v, rhs, body) =>\n        expect v in m, v.name;', '      case LetExpr(v, rhs, body) =>', 'Incarnations.Incarnations.Substitute.'),
    ('quantifier-map', 'Verifier/Incarnations.dfy', '        expect forall v <- vv :: v in m, "quantifier-vars";\n', '', 'Incarnations.Incarnations.Substitute.'),
    ('parameter-invariant', 'Ast/TypeChecker.dfy', '        if !p.maybeAutoInv.value.WellFormed() {\n          return Fail("ill-formed automatic invariant");\n        }\n', '', 'TypeChecker.CheckProcedure.'),
    ('local-invariant', 'Ast/TypeChecker.dfy', '          if !variable.maybeAutoInv.value.WellFormed() {\n            return Fail("ill-formed automatic invariant");\n          }\n', '', 'TypeChecker.CheckStmt.'),
    ('assume-auto-invariant', 'Verifier/Verifier.dfy', '        expect autoInv.WellFormed();', '', 'Verifier.AssumeAutoInvariant.'),
    ('local-auto-invariant', 'Verifier/Verifier.dfy', '        expect autoInv.WellFormed();', '', 'Verifier.Process.'),
    ('loop-auto-invariant', 'Verifier/Verifier.dfy', '          expect autoInv.WellFormed();', '', 'Verifier.ProcessLoop.'),
    ('procedure-header', 'Verifier/Verifier.dfy', '    expect AstValid.ProcedureHeader(proc);\n', '', 'Verifier.ProcessCall.'),
    ('response-progress', 'Solver/ExternalSolvers.dfy', '      remaining := remaining - 1;', '      remaining := remaining;', 'ExternalSolvers.ReadBalancedParentheses.'),
    ('marker-termination', 'Solver/DeclarationMarkers.dfy', 'trait DeclarationMarker', 'trait {:termination false} DeclarationMarker', 'Incarnations'),
    ('syntax-termination', 'Driver/CommandLineOptions.dfy', 'trait Syntax', 'trait {:termination false} Syntax', 'CommandLineOptions'),
    ('option-info-closure', 'Driver/CommandLineOptions.dfy', 'datatype {:termination_closed} OptionInfo', 'datatype OptionInfo', 'B3.B3CliSyntax'),
    ('process-allocation-contract', 'Util/OSProcesses.dfy', 'p.Valid() && fresh(p)', 'p.Valid()', 'ExternalSolvers.StartSmtSolverProcess.'),
    ('substitute-termination', 'Verifier/Incarnations.dfy', '      decreases expr, 0', '      decreases 0', 'Incarnations.Incarnations.Substitute.'),
    ('list-termination', 'Verifier/Incarnations.dfy', '      decreases exprs, 1', '      decreases 0', 'Incarnations.Incarnations.SubstituteList.'),
    ('patterns-termination', 'Verifier/Incarnations.dfy', '      decreases patterns, 0', '      decreases 0', 'Incarnations.Incarnations.SubstitutePatterns.'),
    ('process-valid-proof', 'Util/OSProcesses.dfy', 'ghost predicate Valid() { true }', 'ghost predicate Valid() { false }', 'OSProcesses.OSProcess.Create.'),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dafny-dll', type=Path, required=True)
    parser.add_argument('--solver', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--only', nargs='+', help='Run only these mutation ids; policy checks always run')
    args = parser.parse_args()
    work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True)
    (work / 'tmp').mkdir(exist_ok=True)
    env = dict(os.environ, TMPDIR=str(work / 'tmp'))
    dafny = [args.dotnet, str(args.dafny_dll.resolve())]
    flags = ['--cores', '2', '--solver-path', str(args.solver.absolute()), '--resource-limit', '100000000', '--verification-time-limit', '60']
    rows = []
    for name, file, before, after, symbol in MUTATIONS:
        if args.only and name not in args.only:
            continue
        tree = work / 'source'
        if tree.exists():
            shutil.rmtree(tree)
        shutil.copytree(ROOT, tree, ignore=shutil.ignore_patterns('__pycache__'))
        source = tree / 'src' / file
        text = source.read_text()
        if name == 'local-auto-invariant':
            start = text.index('  method Process(')
        else:
            start = 0
        offset = text.index(before, start)
        source.write_text(text[:offset] + after + text[offset + len(before):])
        command = dafny + ['verify', str(tree / 'src/dfyconfig.toml'), '--filter-symbol', symbol] + flags
        result = subprocess.run(command, capture_output=True, env=env, timeout=180)
        output = result.stdout + result.stderr
        (work / (name + '.log')).write_bytes(output)
        # A type-correct proof failure or the intended termination diagnostic is
        # required. An infrastructure failure never kills a mutation.
        killed = result.returncode != 0 and (b'could not be proved' in output or b'might not hold' in output or b'might not be in domain' in output or b'call might violate' in output or b'decreases' in output or b'unchecked' in output or b'termination false' in output)
        rows.append({'name': name, 'exit': result.returncode, 'killed': killed})
        print(name, 'caught' if killed else 'SURVIVED', flush=True)
        (work / 'mutations.json').write_text(json.dumps(rows, indent=2) + '\n')
        if not killed:
            raise RuntimeError('mutation was not caught: ' + name)
    # Dafny permits deliberately nonterminating executable methods and declared
    # axioms. The snapshot policy check must reject reintroducing either escape.
    from provenance import TRUST
    policy_sites = [
        ('main-wildcard', 'Driver/Main.dfy', '  method Main(args: seq<string>)', '    decreases *'),
        ('stdin-driver-wildcard', 'Driver/Main.dfy', '  method ReadAndParseFromStdin()', '    decreases *'),
        ('stdin-wildcard', 'Util/StdinReader.dfy', '  method {:extern} ReadStdin()', '    decreases *'),
        ('stdin-axiom', 'Util/StdinReader.dfy', '  method {:extern} ReadStdin()', '  @Axiom'),
        ('process-create-axiom', 'Util/OSProcesses.dfy', '    static method Create(', '    @Axiom'),
        ('process-send-axiom', 'Util/OSProcesses.dfy', '    method Send(', '    @Axiom'),
        ('process-read-axiom', 'Util/OSProcesses.dfy', '    method ReadLine()', '    @Axiom'),
    ]
    for name, file, marker, insertion in policy_sites:
        source = (ROOT / 'src' / file).read_text()
        offset = source.index(marker)
        if 'wildcard' in name:
            offset = source.index('\n', offset)
        changed = source[:offset] + '\n' + insertion + '\n' + source[offset:]
        assert not TRUST.search(source), 'original fails trust policy'
        killed = bool(TRUST.search(changed))
        (work / (name + '.log')).write_text('unchecked premise rejected\n' if killed else 'SURVIVED\n')
        rows.append({'name': name, 'killed': killed, 'control': 'trust-policy'})
        print(name, 'caught' if killed else 'SURVIVED', flush=True)
        if not killed:
            raise RuntimeError('trust-policy mutation survived: ' + name)
    if args.only and 'closure-halt' not in args.only:
        (work / 'mutations.json').write_text(json.dumps(rows, indent=2) + '\n')
        if tree.exists():
            shutil.rmtree(tree)
        return
    # The closure case used to obtain a value from Expect(false). Removing its
    # executable halt still verifies, so its runtime negative control is needed.
    tree = work / 'source'
    shutil.rmtree(tree)
    shutil.copytree(ROOT, tree, ignore=shutil.ignore_patterns('__pycache__'))
    source = tree / 'src/Verifier/Incarnations.dfy'
    source.write_text(source.read_text().replace('        expect false, "closure-not-elaborated";\n', ''))
    program = check.compile_program(dafny, tree / 'controls/dfyconfig.toml', work / 'closure-build', tree / 'target/cs/src', env)
    result = subprocess.run([str(x) for x in program + ['closure']], capture_output=True, env=env, timeout=60)
    (work / 'closure.log').write_bytes(result.stdout + result.stderr)
    killed = result.returncode == 0 and b'CONTROL PASSED' in result.stdout
    rows.append({'name': 'closure-halt', 'exit': result.returncode, 'killed': killed})
    print('closure-halt', 'caught' if killed else 'SURVIVED', flush=True)
    (work / 'mutations.json').write_text(json.dumps(rows, indent=2) + '\n')
    if not killed:
        raise RuntimeError('closure mutation was not distinguished')
    shutil.rmtree(tree)


if __name__ == '__main__':
    main()
