#!/usr/bin/env python3
"""Characterize enabled-suite regressions without changing source or production options."""
import argparse
import json
from pathlib import Path
import re
import subprocess

p = argparse.ArgumentParser()
p.add_argument('--baseline', required=True); p.add_argument('--candidate', required=True)
p.add_argument('--z3', required=True); p.add_argument('--output', required=True)
a = p.parse_args(); out = Path(a.output).resolve(); out.mkdir(parents=True, exist_ok=True)
# Reuse the streaming parser and complete fresh-process profile collector.
helpers = {}
exec(Path('.github/review/issue33-profile.py').read_text().split('p=argparse.ArgumentParser();')[0], helpers)
litdir = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest').resolve()
cases = [('dafny4/Ackermann.dfy', 'Am', []), ('dafny4/Lucas-up.dfy', 'INDUCTION_EVEN_ODD', []),
         ('dafny2/SnapshotableTrees.dfy', 'Iterator.Push', ['--solver-option=O:smt.qi.eager_threshold=80'])]
report = []
for name, symbol, extra in cases:
    for mode, binary in [('BE', a.baseline), ('E', a.candidate)]:
        dest = out / symbol / mode; dest.mkdir(parents=True, exist_ok=True)
        cmd = [binary, 'verify', name, '--additional-axioms', '--solver-path', a.z3,
               '--cores', '1', '--resource-limit', '16000000', '--verification-time-limit', '0',
               '--allow-warnings', '--filter-symbol:' + symbol,
               '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/emitDebugInformation:1',
               '--bprint', str(dest / 'input.bpl'), '--solver-log', str(dest / 'solver.smt2'),
               '--log-format', 'json;LogFileName=' + str(dest / 'results.json')] + extra
        (dest / 'command.json').write_text(json.dumps(cmd, indent=2))
        run = subprocess.run(cmd, cwd=litdir, capture_output=True, text=True, timeout=900)
        (dest / 'stdout.txt').write_text(run.stdout); (dest / 'stderr.txt').write_text(run.stderr)
        (dest / 'exit-code.txt').write_text(str(run.returncode))
        bpl = '\n'.join(x.read_text() for x in dest.glob('*.bpl'))
        identities = re.findall(r'// concrete integer literal identity\s*axiom (.*?);', bpl, re.S)
        summaries = re.findall(r'verifier finished with ([^\n]*)', run.stdout)
        row = {'file': name, 'symbol': symbol, 'mode': mode, 'exit': run.returncode,
               'summaries': summaries, 'identities': identities, 'profiles': []}
        logs = sorted(dest.glob('solver.smt2*'))
        for log in logs:
            for index, (vc, body) in enumerate(helpers['queries'](log)):
                if 'Impl$' not in vc: continue
                profiles = [("unchanged", body)]
                if mode == 'E':
                    lit_axiom = next(c for c in body if ':qid |DafnyPreludebpl.112:29|' in c)
                    lit = re.search(r'\(= \((\S+) (\S+)\) \2\)', lit_axiom)[1]
                    closed = []
                    for c in body:
                        match = re.fullmatch(r'\(assert \(= \(' + re.escape(lit) + r' (.+)\) (.+)\)\)', c)
                        if match and match[1] == match[2]: closed.append(c)
                    row.setdefault('closed_smt', {})[vc] = closed
                    profiles.append(('without-collected-identities', [c for c in body if c not in closed]))
                for variant, replay_body in profiles:
                    result = helpers['replay'](a.z3, replay_body, dest / 'profiles' / log.name / f'{index:04}' / variant,
                                               vc + ' / ' + variant, 16000000)
                    row['profiles'].append(result)
        report.append(row)
        (out / 'report.json').write_text(json.dumps(report, indent=2))
        print(json.dumps({k:v for k,v in row.items() if k != 'profiles'}), flush=True)
