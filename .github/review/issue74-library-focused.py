#!/usr/bin/env python3
"""Recheck the six library regression declarations without editing sources or limits."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

p = argparse.ArgumentParser()
p.add_argument('--baseline', required=True); p.add_argument('--candidate', required=True)
p.add_argument('--z3', required=True); p.add_argument('--output', required=True)
a = p.parse_args(); out = Path(a.output).resolve(); out.mkdir(parents=True, exist_ok=True)
lib = Path('Source/DafnyStandardLibraries').resolve()
symbols = ['Std.Arithmetic.DivMod.LemmaHoistOverDenominator', 'Std.Arithmetic.Power2.Lemma2To64',
           'Std.Arithmetic.DivMod.LemmaMultiplyDivideLe', 'Std.BulkActions.ToBatchedProducer',
           'Std.JSON.ConcreteSyntax.SpecProperties.ConcatBytes_Linear',
           'Std.JSON.ZeroCopy.Deserializer.Sequences.Elements']
report = []
for symbol in symbols:
    row = {'symbol': symbol, 'results': {}}
    for mode, binary in [('BE', a.baseline), ('E', a.candidate)]:
        dest = out / symbol / mode; dest.mkdir(parents=True, exist_ok=True)
        cmd = [binary, 'verify', 'src/Std/dfyconfig.toml', '--solver-path', a.z3,
               '--additional-axioms', '--cores', '1', '--verification-time-limit', '0',
               '--filter-symbol:' + symbol, '--bprint', str(dest / 'input.bpl'),
               '--solver-log', str(dest / 'solver.smt2'),
               '--log-format', 'json;LogFileName=' + str(dest / 'results.json')]
        (dest / 'command.json').write_text(json.dumps(cmd, indent=2))
        try:
            run = subprocess.run(cmd, cwd=lib, capture_output=True, text=True, timeout=900)
            rc, text = run.returncode, run.stdout + run.stderr
        except subprocess.TimeoutExpired as ex:
            rc, text = 'TIMEOUT', str(ex)
        (dest / 'output.txt').write_text(text)
        logs = json.loads((dest / 'results.json').read_text()).get('verificationResults', []) if (dest / 'results.json').exists() else []
        row['results'][mode] = {'exit': rc, 'summary': re.findall(r'verifier finished with ([^\n]*)', text),
                              'declarations': [{k:d[k] for k in ['name', 'outcome', 'resourceCount', 'vcResults']} for d in logs]}
        print(symbol, mode, rc, row['results'][mode]['summary'], flush=True)
    before = {d['name']: d for d in row['results']['BE']['declarations']}
    after = {d['name']: d for d in row['results']['E']['declarations']}
    row['regressions'] = [key for key, decl in before.items() if decl['outcome'] == 'Correct' and
                          (key not in after or after[key]['outcome'] != 'Correct')]
    row['nonempty'] = bool(before) and bool(after) and before.keys() == after.keys()
    report.append(row)
    (out / 'report.json').write_text(json.dumps(report, indent=2))
(out / 'summary.json').write_text(json.dumps({'solver': subprocess.check_output([a.z3, '--version'], text=True).strip(),
    'solver_sha256': hashlib.sha256(Path(a.z3).read_bytes()).hexdigest(),
    'all_nonempty': all(row['nonempty'] for row in report),
    'regressions': [row['symbol'] for row in report if row['regressions']]}, indent=2))
