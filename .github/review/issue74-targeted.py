#!/usr/bin/env python3
"""End-to-end literal-identity probes; expected failures are recorded, never hidden."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

p = argparse.ArgumentParser()
p.add_argument('--baseline', required=True)
p.add_argument('--candidate', required=True)
p.add_argument('--z3', required=True)
p.add_argument('--output', required=True)
a = p.parse_args()
out = Path(a.output).resolve(); out.mkdir(parents=True, exist_ok=True)
inputs = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs').resolve()
report = {'runs': [], 'off_differences': [], 'checks': {}}
files = ['github-issue-33-original.dfy', 'github-issue-33-positive.dfy',
         'github-issue-33-negative.dfy', 'github-issue-74-positive.dfy', 'github-issue-74-negative.dfy']
for refresh in [False, True]:
    for name in files:
        results = {}
        for mode, binary, flags in [('B', a.baseline, []), ('O', a.candidate, []),
                                    ('BE', a.baseline, ['--additional-axioms']),
                                    ('E', a.candidate, ['--additional-axioms'])]:
            dest = out / str(refresh).lower() / name / mode; dest.mkdir(parents=True, exist_ok=True)
            cmd = [binary, 'verify', str(inputs / name), '--solver-path', a.z3,
                   '--cores', '1', '--resource-limit', '200000', '--verification-time-limit', '0',
                   '--type-system-refresh:' + str(refresh).lower(), '--show-snippets:false',
                   '--error-limit', '0', '--boogie', '/normalizeDeclarationOrder:0',
                   '--solver-log', str(dest / 'solver.smt2'),
                   '--bprint', str(dest / 'input.bpl'), '--log-format', 'json;LogFileName=' + str(dest / 'results.json')] + flags
            (dest / 'command.json').write_text(json.dumps(cmd))
            try:
                r = subprocess.run(cmd, capture_output=True, text=True, timeout=180)
                rc, text = r.returncode, r.stdout + r.stderr
            except subprocess.TimeoutExpired as ex:
                rc, text = 'TIMEOUT', str(ex)
            (dest / 'output.txt').write_text(text)
            logs = json.loads((dest / 'results.json').read_text()).get('verificationResults', []) if (dest / 'results.json').exists() else []
            batches = {f"{d['name']} #{v['vcNum']}": [v['outcome'], v['resourceCount']]
                       for d in logs for v in d['vcResults']}
            bpl = {x.name: hashlib.sha256(x.read_bytes()).hexdigest() for x in dest.glob('*.bpl')}
            smt = {x.name: hashlib.sha256(x.read_bytes()).hexdigest() for x in dest.glob('solver.smt2*')}
            results[mode] = {'exit': rc, 'summary': re.findall(r'verifier finished with ([^\n]*)', text),
                             'batches': batches, 'bpl': bpl, 'smt': smt}
            print(refresh, name, mode, rc, results[mode]['summary'], flush=True)
        identical = all(results['B'][k] == results['O'][k] for k in ['exit', 'summary', 'batches', 'bpl', 'smt'])
        if not identical: report['off_differences'].append([refresh, name])
        report['runs'].append({'refresh': refresh, 'file': name, 'off_identical': identical, 'results': results})
report['checks']['all_off_identical'] = not report['off_differences']
report['checks']['solver_input_saved'] = all(r['results'][m]['smt'] for r in report['runs'] for m in ['B', 'O', 'BE', 'E'])
report['checks']['positive_pass'] = all(r['results']['E']['exit'] == 0 for r in report['runs'] if 'negative' not in r['file'])
report['checks']['negative_rejected'] = all(r['results']['E']['exit'] == 4 and r['results']['E']['summary'] and
    '11 errors' in r['results']['E']['summary'][0] if '33-negative' in r['file'] else
    r['results']['E']['exit'] == 4 and r['results']['E']['summary'] and '3 errors' in r['results']['E']['summary'][0]
    for r in report['runs'] if 'negative' in r['file'])
report['solver_version'] = subprocess.check_output([a.z3, '--version'], text=True).strip()
report['solver_sha256'] = hashlib.sha256(Path(a.z3).read_bytes()).hexdigest()
report['original_sha256'] = hashlib.sha256((inputs / files[0]).read_bytes()).hexdigest()
(out / 'report.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report['checks'], indent=2))
