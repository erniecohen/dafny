#!/usr/bin/env python3
"""Retain the original issue's proof results and actual Boogie/SMT inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

p = argparse.ArgumentParser()
p.add_argument('--baseline', required=True)
p.add_argument('--candidate')
p.add_argument('--z3', required=True)
p.add_argument('--output', required=True)
a = p.parse_args()
out = Path(a.output).resolve()
out.mkdir(parents=True, exist_ok=True)
source = Path('Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-33-original.dfy').resolve()
metadata = {'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'z3_sha256': hashlib.sha256(Path(a.z3).read_bytes()).hexdigest(), 'z3_version': subprocess.check_output([a.z3, '--version'], text=True).strip(), 'builds': {}}
for label, binary, flag in [('B', a.baseline, []), *([('O', a.candidate, []), ('F', a.candidate, ['--additional-axioms=false']), ('E', a.candidate, ['--additional-axioms'])] if a.candidate else [])]:
    dest = out / label
    dest.mkdir(exist_ok=True)
    metadata['builds'][label] = {'version': subprocess.check_output([binary, '--version'], text=True).strip(), 'core_sha256': hashlib.sha256(Path(binary).with_name('DafnyCore.dll').read_bytes()).hexdigest()}
    cmd = [binary, 'verify', str(source), '--solver-path', a.z3, '--cores', '1', '--resource-limit', '20000000', '--verification-time-limit', '0', '--boogie', '/normalizeDeclarationOrder:0', '--log-format', 'json;LogFileName=' + str(dest / 'results.json'), '--log-format', 'csv;LogFileName=' + str(dest / 'results.csv'), '--bprint', str(dest / 'program.bpl'), '--solver-log', str(dest / 'solver.smt2')] + flag
    (dest / 'command.json').write_text(json.dumps(cmd, indent=2))
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=1200)
    (dest / 'output.txt').write_text(result.stdout + result.stderr)
    (dest / 'exit-code.txt').write_text(str(result.returncode))
    print(label, result.returncode, result.stdout[-250:], flush=True)
(out / 'metadata.json').write_text(json.dumps(metadata, indent=2))
