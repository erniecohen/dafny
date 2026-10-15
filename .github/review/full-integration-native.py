"""Scratch issue 47 probe: record outcomes without accepting them as a gate."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

root = Path('out/native-evidence')
root.mkdir(parents=True, exist_ok=True)
assembly = Path('out/toolchain/dafny/Dafny.dll').resolve()
solver = Path(os.environ['Z3']).resolve()
source = Path('.github/review/issue47/equality-runtime.dfy')
rows = []
for refresh in [False, True]:
    mode = ['--type-system-refresh:' + str(refresh).lower(),
            '--general-newtypes:' + str(refresh).lower(),
            '--standard-libraries:false', '--show-snippets:false',
            '--use-basename-for-filename', '--allow-warnings']
    for phase in ['resolve', 'verify', 'run']:
        folder = root / (phase + ('-refresh' if refresh else '-legacy'))
        folder.mkdir(exist_ok=True)
        command = ['dotnet', str(assembly), phase, str(source)] + mode
        if phase == 'verify':
            command += ['--solver-path', str(solver), '--cores:1',
                        '--resource-limit:16000000', '--verification-time-limit:30',
                        '--boogie:/normalizeDeclarationOrder:0',
                        '--log-format', 'csv;LogFileName=' + str((folder/'resources.csv').resolve())]
        if phase == 'run':
            command += ['--no-verify', '--target:cs']
        (folder/'command.json').write_text(json.dumps(command, indent=2) + '\n')
        try:
            result = subprocess.run(command, capture_output=True, text=True, timeout=240)
            stdout, stderr, code = result.stdout, result.stderr, result.returncode
        except subprocess.TimeoutExpired as error:
            stdout, stderr, code = str(error.stdout), str(error.stderr), 'timeout'
        (folder/'stdout.txt').write_text(stdout)
        (folder/'stderr.txt').write_text(stderr)
        rows.append({'phase': phase, 'refresh': refresh, 'exit': code,
                     'stdout': stdout, 'stderr': stderr})
        print(phase, refresh, 'actual exit', code, flush=True)
receipt = {'diagnostic': True, 'acceptance': False,
           'scope': 'Original, explicit crossed instantiations, and common static views; recorded resolver, verification and C# runtime outcomes.',
           'compiler_source': Path('out/toolchain/source.txt').read_text().strip(),
           'probe_source': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
           'compiler_version': subprocess.check_output(['dotnet', str(assembly), '--version'], text=True).strip(),
           'compiler_core_sha256': hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),
           'solver_version': subprocess.check_output([str(solver), '--version'], text=True).strip(),
           'fixture_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'rows': rows}
(root/'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
if os.getenv('GITHUB_STEP_SUMMARY'):
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as stream:
        stream.write('## Issue 47 runtime probe\n\nDiagnostic outcomes only; this is not an acceptance gate.\n\n')
        for row in rows:
            stream.write(f"- {row['phase']}, refresh={row['refresh']}: actual exit {row['exit']}\n")
