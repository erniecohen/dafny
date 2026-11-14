#!/usr/bin/env python3
"""Build the certified standard-library core under --modular-termination, and load it.

Milestones M5a and M5b of https://github.com/erniecohen/dafny/issues/211.

    modular-termination-std.py DAFNY Z3 OUT [--cores N]

DAFNY is the dafny executable (or Dafny.dll), Z3 the Z3 binary the record names.  The script
builds Source/DafnyStandardLibraries/modular-termination/dfyconfig.toml with
`dafny build --target:lib`, which under the project's `modular-termination = true` writes a
library only when every verification task of the core, and of the program stored in it, succeeds.
It then checks the library's record and loads it into clients:

  - a client that uses Wrappers, Collections.Seq and Arithmetic.DivMod verifies;
  - a client with a false lemma fails verification (exit 4), so the library is not vacuous;
  - a client that imports Std.Strings, which the core leaves out, does not resolve;
  - a client run without one of the core's semantic options is rejected at admission;
  - the bundled standard library is still rejected under the option.

M5b: it then builds the expanded core, Source/DafnyStandardLibraries/modular-termination/trusted,
which adds the modules whose only premises are on the standard-library trust list, under
--modular-termination-trust:standard-library, and checks:

  - its record lists exactly the 17 premises of the trust list in its modules (all but
    Std.Strings.HexConversion.CharsConsistent, since Strings is left out);
  - a client that opted in to the trust list verifies, and its summary names the premises;
  - a client that did not opt in is refused at admission;
  - a client of the core gives the same output with and without the trust list.

OUT gets every command, its output and exit code, the per-implementation resource counts of each
build (build.csv and trusted-build.csv) and summary.md.  Exits 1 when a check fails.
"""
import csv
import os
import shutil
import subprocess
import sys
import tomllib
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PROJECT = os.environ.get('MODULAR_TERMINATION_STD_PROJECT') or os.path.join(
    ROOT, 'Source', 'DafnyStandardLibraries', 'modular-termination', 'dfyconfig.toml')
TRUSTED_PROJECT = os.environ.get('MODULAR_TERMINATION_STD_TRUSTED_PROJECT') or os.path.join(
    ROOT, 'Source', 'DafnyStandardLibraries', 'modular-termination', 'trusted', 'dfyconfig.toml')
TRUST = '--modular-termination-trust:standard-library'
TRUST_LIST_SIZE = 17
# The semantic options of the core's project file, which a client must use too.
CORE_OPTIONS = ['--type-system-refresh', '--general-traits=datatype', '--general-newtypes',
                '--reads-clauses-on-methods', '--enforce-determinism', '--track-print-effects']

CLIENT = """module Client {
  import opened Std.Wrappers
  import Std.Collections.Seq
  import Std.Arithmetic.DivMod

  function Head(s: seq<int>): Option<int> {
    if |s| == 0 then None else Some(s[0])
  }

  method Use() {
    var s := Seq.Map(x => x + 1, [1, 2, 3]);
    assert s[0] == 2;
    assert Head(s) == Some(2);
    DivMod.LemmaFundamentalDivMod(7, 3);
    assert 7 == 3 * (7 / 3) + 7 % 3;
  }
}
"""

FALSE_CLIENT = """module Client {
  import opened Std.Wrappers

  lemma Bad(o: Option<int>)
    ensures o.IsFailure()
  {
  }
}
"""

EXCLUDED_CLIENT = """module Client {
  import Std.Strings

  function F(n: nat): string {
    Strings.OfNat(n)
  }
}
"""


TRUSTED_CLIENT = """module Client {
  import Std.Ordinal
  import Std.RealCompleteness

  lemma Successor(a: ORDINAL)
    ensures a < a + 1
  {
    Ordinal.Succ(a, a + 1);
  }
}
"""


def dafny_command(dafny):
    return ['dotnet', dafny] if dafny.endswith('.dll') else [dafny]


class Run:
    def __init__(self, dafny, z3, out):
        self.dafny = dafny_command(dafny)
        self.z3 = z3
        self.out = out
        self.results = []
        self.log = open(os.path.join(out, 'commands.txt'), 'w')

    def run(self, name, arguments, timeout=None):
        command = self.dafny + arguments
        process = subprocess.run(command, capture_output=True, text=True, cwd=self.out, timeout=timeout)
        output = process.stdout + process.stderr
        self.log.write(f'## {name}\n$ {" ".join(command)}\nexit {process.returncode}\n{output}\n')
        self.log.flush()
        return process.returncode, output

    def check(self, name, ok, detail):
        self.results.append((name, ok, detail))
        print(f'{"PASS" if ok else "FAIL"} {name}: {detail}', flush=True)

    def client(self, name, text, extra, expected_exit, expected_text, library='core.doo'):
        path = os.path.join(self.out, f'{name}.dfy')
        with open(path, 'w') as f:
            f.write(text)
        code, output = self.run(name, ['verify', '--modular-termination', '--use-basename-for-filename',
                                       '--solver-path', self.z3, '--library', library] + extra + [path])
        ok = (code != 0 if expected_exit is None else code == expected_exit) and (expected_text is None or expected_text in output)
        lines = [line for line in output.splitlines() if line.strip()]
        self.check(name, ok, f'exit {code} (expected {expected_exit if expected_exit is not None else "nonzero"}); ' + (lines[-1] if lines else 'no output'))
        return code, output


def resource_counts(path):
    implementations, total = 0, 0
    if not os.path.exists(path):
        return implementations, total
    with open(path, newline='') as f:
        for row in csv.DictReader(f):
            implementations += 1
            total += int(row.get('TestResult.ResourceCount') or 0)
    return implementations, total


def main():
    arguments = sys.argv[1:]
    cores = '4'
    if '--cores' in arguments:
        i = arguments.index('--cores')
        cores = arguments[i + 1]
        del arguments[i:i + 2]
    if len(arguments) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    dafny, z3, out = (os.path.abspath(a) for a in arguments)
    if os.path.exists(out):
        shutil.rmtree(out)
    os.makedirs(out)
    run = Run(dafny, z3, out)

    code, output = run.run('build', ['build', '--target:lib', '--use-basename-for-filename', '--solver-path', z3,
                                     '--cores', cores, '--log-format', f'csv;LogFileName={os.path.join(out, "build.csv")}',
                                     PROJECT, '--output', os.path.join(out, 'core.doo')])
    built = code == 0 and os.path.exists(os.path.join(out, 'core.doo'))
    summary_lines = [line for line in output.splitlines() if 'verified' in line or 'Error' in line]
    run.check('build', built, f'exit {code}; ' + ('; '.join(summary_lines[-3:]) or 'no summary'))
    implementations, total = resource_counts(os.path.join(out, 'build.csv'))

    exports = []
    if built:
        with zipfile.ZipFile(os.path.join(out, 'core.doo')) as archive:
            manifest = tomllib.loads(archive.read('manifest.toml').decode())
            has_contracts = 'termination-contracts.json' in archive.namelist()
        record = manifest.get('modular_termination', {})
        exports = [e.get('module') for e in record.get('exports', [])]
        run.check('record', manifest.get('doo_file_version') == '2.0' and record.get('status') == 'complete'
                  and record.get('solver_version', '').startswith('5.1.0') and has_contracts and exports,
                  f"doo_file_version {manifest.get('doo_file_version')}, status {record.get('status')}, "
                  f"solver {record.get('solver_version')}, {len(exports)} exported modules")
        excluded = [m for m in exports if m.split('.')[:2] in (['Std', 'Ordinal'], ['Std', 'Strings'], ['Std', 'JSON'],
                                                               ['Std', 'Actions'], ['Std', 'Termination'])]
        run.check('exports', not excluded, f'excluded modules exported: {excluded or "none"}')

        run.client('client', CLIENT, CORE_OPTIONS, 0, '0 errors')
        run.client('false-client', FALSE_CLIENT, CORE_OPTIONS, 4, None)
        run.client('excluded-client', EXCLUDED_CLIENT, CORE_OPTIONS, 2, 'Strings')
        run.client('option-mismatch', CLIENT, [o for o in CORE_OPTIONS if o != '--enforce-determinism'], None,
                   'enforce-determinism')
    code, output = run.run('bundled', ['verify', '--modular-termination', '--use-basename-for-filename',
                                       '--standard-libraries:true', '--solver-path', z3] + CORE_OPTIONS +
                           [os.path.join(out, 'client.dfy') if built else PROJECT])
    run.check('bundled', code != 0 and 'certified standard-library core' in output,
              f'exit {code}; bundled standard library rejected: {"certified standard-library core" in output}')

    # M5b: the expanded core, with the premises of the standard-library trust list.
    code, output = run.run('trusted build', ['build', '--target:lib', '--use-basename-for-filename', '--solver-path', z3,
                                             '--cores', cores, '--log-format',
                                             f'csv;LogFileName={os.path.join(out, "trusted-build.csv")}',
                                             TRUSTED_PROJECT, '--output', os.path.join(out, 'trusted.doo')])
    trusted_built = code == 0 and os.path.exists(os.path.join(out, 'trusted.doo'))
    summary_lines = [line for line in output.splitlines() if 'verified' in line or 'Error' in line or 'trusted premise' in line]
    run.check('trusted build', trusted_built, f'exit {code}; ' + ('; '.join(summary_lines[-3:]) or 'no summary'))
    trusted_implementations, trusted_total = resource_counts(os.path.join(out, 'trusted-build.csv'))
    premises, trusted_exports = [], []
    if trusted_built:
        with zipfile.ZipFile(os.path.join(out, 'trusted.doo')) as archive:
            trusted_record = tomllib.loads(archive.read('manifest.toml').decode()).get('modular_termination', {})
        premises = [(p.get('id'), p.get('kind')) for p in trusted_record.get('premises', [])]
        trusted_exports = [e.get('module') for e in trusted_record.get('exports', [])]
        run.check('trusted record', trusted_record.get('schema') == 2 and trusted_record.get('trust_list') == 'standard-library'
                  and len(premises) == TRUST_LIST_SIZE,
                  f"schema {trusted_record.get('schema')}, trust list {trusted_record.get('trust_list')}, "
                  f"{len(premises)} premises, {len(trusted_exports)} exported modules")
        run.client('trusted-client', TRUSTED_CLIENT, CORE_OPTIONS + [TRUST], 0,
                   f'these results assume {TRUST_LIST_SIZE} trusted premises of the standard-library trust list, from 1 library',
                   library='trusted.doo')
        run.client('trusted-client-not-opted-in', TRUSTED_CLIENT, CORE_OPTIONS, None,
                   f'use {TRUST} to admit it', library='trusted.doo')
    if built:
        _, plain = run.client('core-client-trust', CLIENT, CORE_OPTIONS + [TRUST], 0, '0 errors')
        _, without = run.run('core-client-again', ['verify', '--modular-termination', '--use-basename-for-filename',
                                                   '--solver-path', z3, '--library', 'core.doo'] + CORE_OPTIONS +
                             [os.path.join(out, 'core-client-trust.dfy')])
        run.check('core-client-unchanged', plain == without and 'trusted premise' not in plain,
                  'a core client gives the same output with and without the trust list')

    failed = [name for name, ok, _ in run.results if not ok]
    with open(os.path.join(out, 'summary.md'), 'w') as f:
        f.write('## Certified standard-library core under --modular-termination (issue 211, M5a and M5b)\n\n')
        f.write('Source/DafnyStandardLibraries/modular-termination (the core) and its trusted/ project (the expanded '
                'core, with the standard-library trust list), each built with `dafny build --target:lib` and loaded '
                'into clients.\n\n')
        f.write('| check | result | detail |\n|---|---|---|\n')
        for name, ok, detail in run.results:
            f.write(f'| {name} | {"pass" if ok else "**FAIL**"} | {detail.replace("|", "/")} |\n')
        f.write(f'\nThe core build verified {implementations} implementations, {total:,} Z3 resource units in all, '
                'in the first of its two verifications (build.csv; the second, of the stored program, is not logged).\n')
        f.write(f'\nThe expanded core build verified {trusted_implementations} implementations, {trusted_total:,} Z3 '
                'resource units in all, in the first of its two verifications (trusted-build.csv).\n')
        if premises:
            f.write(f'\nPremises of the expanded core ({len(premises)}): ' +
                    ', '.join(f'`{i}` ({k})' for i, k in premises) + '\n')
        if exports:
            f.write(f'\nExported modules ({len(exports)}): ' + ', '.join(f'`{m}`' for m in exports) + '\n')
        f.write(f'\n{len(run.results) - len(failed)} of {len(run.results)} checks passed.\n')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
