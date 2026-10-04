import json
import os
from pathlib import Path
import subprocess

repo = Path.cwd()
dafny = str(repo / 'out/dafny/Dafny')
out = repo / 'out/diagnostics'
out.mkdir(parents=True, exist_ok=True)
metadata = {
    'source': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
    'dafny': subprocess.check_output([dafny, '--version'], text=True).strip(),
    'solvers': {key: subprocess.check_output([os.environ[key], '--version'], text=True).strip()
                for key in ['PINNED_Z3', 'TREE_Z3']}
}
(out / 'metadata.json').write_text(json.dumps(metadata, indent=2))
cases = sorted((repo / 'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues').glob('git-issue-141*.dfy'))
cases = [p for p in cases if p.name == 'git-issue-141.dfy' or p.name.startswith('git-issue-141-')]
summary = []
for solver in ['PINNED_Z3', 'TREE_Z3']:
    for case in cases:
        target = out / solver / case.stem
        target.mkdir(parents=True, exist_ok=True)
        args = [dafny, 'verify', str(case), '--type-system-refresh', '--general-traits=datatype',
                '--general-newtypes', '--use-basename-for-filename', '--show-snippets=false',
                '--standard-libraries=false', '--cores=1', '--resource-limit=16000000',
                '--verification-time-limit=60', '--solver-path=' + os.environ[solver]]
        result = subprocess.run(args, capture_output=True, text=True, timeout=600)
        (target / 'stdout.txt').write_text(result.stdout)
        (target / 'stderr.txt').write_text(result.stderr)
        diagnostic_args = args + ['--bprint=' + str(target / 'program.bpl'),
                                  '--log-format=csv;LogFileName=' + str(target / 'resources.csv')]
        diagnostic = subprocess.run(diagnostic_args, capture_output=True, text=True, timeout=600)
        (target / 'diagnostic-stdout.txt').write_text(diagnostic.stdout)
        (target / 'diagnostic-stderr.txt').write_text(diagnostic.stderr)
        row = {'case': case.name, 'solver': solver, 'exit': result.returncode,
               'diagnostic_exit': diagnostic.returncode, 'command': args}
        (target / 'result.json').write_text(json.dumps(row, indent=2))
        summary.append(row)
(out / 'summary.json').write_text(json.dumps(summary, indent=2))
with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as f:
    f.write('Focused measurements; expected failures are recorded rather than failing the job.\n\n')
    f.write('| Case | Solver | Exit | Diagnostic exit |\n|---|---|---|---|\n')
    for row in summary:
        f.write('| {case} | {solver} | {exit} | {diagnostic_exit} |\n'.format(**row))
