"""Checked-Std callback, fresh-producer and repaired-JSON controls, both resolvers."""
from pathlib import Path
import json
import shutil
import subprocess
import sys

binary, solver, output, repository = sys.argv[1:]
binary, solver = str(Path(binary).resolve()), str(Path(solver).resolve())
o = Path(output).resolve()
if o.exists():
    shutil.rmtree(o)
o.mkdir(parents=True)
here = Path(__file__).resolve().parent
recipe = Path(repository).resolve() / 'Source/DafnyStandardLibraries/modular-termination/overlay/build-source.py'
subprocess.run([sys.executable, str(recipe), str(o / 'Std')], check=True)
command = (['dotnet', binary] if binary.endswith('.dll') else [binary])
rows = []
for refresh in [True, False]:
    for name, source, scope in [('families', 'five-families', 'Client'),
                                ('factory', 'fresh-factory', 'FactoryControl'),
                                ('json', 'json-controls', 'JSONControl'),
                                ('proofs', 'proofs', 'ProofWitnesses')]:
        original = (here / ('github-issue-211-checked-std-' + source + '.dfy')).read_text()
        assert original.count('// CONTROL') == 1
        family_controls = original.count('// FAMILY CONTROL')
        assert family_controls == (4 if name == 'families' else 0)
        for negative in [False, True]:
            case = name + ('-refresh' if refresh else '-legacy') + ('-false' if negative else '-positive')
            client = o / (case + '.dfy')
            marker = '// FAMILY CONTROL' if family_controls else '// CONTROL'
            client.write_text(original.replace(marker, 'hide *; assert false;' if negative else marker))
            cmd = command + ['verify', str(o / 'Std/dfyconfig.toml'), str(client),
                '--solver-path', solver, '--use-basename-for-filename', '--show-snippets:false', '--cores', '2',
                '--type-system-refresh:' + str(refresh).lower(), '--general-newtypes:' + str(refresh).lower(),
                '--boogie', '/deterministicLiteralHashes:1', '--boogie', '/restartProver',
                '--resource-limit', '16000000', '--verification-time-limit', '300', '--filter-symbol', scope,
                '--log-format', 'csv;LogFileName=' + str(o / (case + '.csv'))]
            result = subprocess.run(cmd, capture_output=True, text=True)
            text = result.stdout + result.stderr
            (o / (case + '.log')).write_text(text)
            expected_errors = family_controls or 1
            accepted = (result.returncode == 4 and text.count('Error: assertion might not hold') == expected_errors and
                        f'{expected_errors} error' in text and 'out of resource' not in text and 'timed out' not in text
                        if negative else result.returncode == 0)
            rows.append({'case': case, 'exit': result.returncode, 'accepted': accepted})
            print(case + ': ' + ('PASS' if accepted else 'FAIL'))
            if not accepted:
                print(text)
(o / 'exits.json').write_text(json.dumps(rows, indent=2) + '\n')
raise SystemExit(0 if all(row['accepted'] for row in rows) else 1)
