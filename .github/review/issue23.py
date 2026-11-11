#!/usr/bin/env python3
"""CI-only evidence for https://github.com/erniecohen/dafny/issues/23.

Compare only the glue guard using one binary and identical source inputs.
The separately built candidate checks the C# refactor and regression fixtures.
Probe exit codes are evidence, never a substitute for the normal review gate.
"""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path.cwd()
LIT = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'
OUT = ROOT / 'evidence'
variant, scope, z3 = sys.argv[1:]

def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / f'.github/review/{name}-verdicts.py')
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod

def call(cmd, cwd=ROOT):
    p = subprocess.run(list(map(str, cmd)), cwd=cwd, capture_output=True, text=True, timeout=600)
    return {'command': list(map(str, cmd)), 'exit': p.returncode, 'stdout': p.stdout, 'stderr': p.stderr}

# Restore only the two old guards for BEFORE. The compiled baseline is identical
# across the A/B runs; --prelude changes only those two antecedents.
prelude = (ROOT / 'Source/DafnyCore/DafnyPrelude.bpl').read_text()
if variant == 'before':
    prelude = prelude.replace('Set#IsMember(Map#Domain(Map#Glue(a, b, t)), bx) ==>', 'Set#IsMember(a, bx) ==>')
    prelude = prelude.replace('IMap#Domain(IMap#Glue(a, b, t))[bx] ==>', 'a[bx] ==>')
prelude_path = OUT / 'prelude.bpl'
prelude_path.write_text(prelude)
wrapper = OUT / 'dafny'
wrapper.write_text('#!/bin/sh\nexec "' + str(ROOT / 'out/before/Dafny') + '" "$@" --prelude "' + str(prelude_path) + '"\n')
wrapper.chmod(0o755)

if scope.startswith('suite'):
    mod = load('lit')
    mod.plan(str(LIT), str(OUT / 'plan.tsv'))
    mod.run(str(OUT / 'plan.tsv'), str(LIT), str(wrapper), z3, str(OUT / 'verdicts.tsv'),
            shard=scope[-1] + '/4', jobs=4, measurements=str(OUT / 'measurements'))
elif scope == 'std':
    mod = load('std')
    mod.run(str(ROOT / 'Source/DafnyStandardLibraries'), str(wrapper), z3,
            str(OUT / 'verdicts.tsv'), cores=4,
            boogie='/deterministicLiteralHashes:1 /restartProver', measurements=str(OUT / 'measurements'))
elif scope == 'focused':
    binary = ROOT / 'out' / variant / 'Dafny'
    results = []
    fixtures = sorted((LIT / 'git-issues').glob('github-issue-23-*.dfy'))
    for fixture in fixtures:
        for refresh in ['false', 'true']:
            cmd = [binary, 'verify', fixture.name, '--solver-path', z3,
                   '--type-system-refresh:' + refresh, '--general-traits=datatype', '--general-newtypes',
                   '--use-basename-for-filename', '--show-snippets:false', '--standard-libraries:false',
                   '--cores:2', '--verification-time-limit:300', '--resource-limit:50e6']
            r = call(cmd, fixture.parent)
            results.append(r)
            (OUT / (fixture.name + '.' + refresh + '.expect')).write_text(r['stdout'] + r['stderr'])
    # Exact bprint comparison, with identical paths/options/prelude in both builds.
    # The only generating-code difference is the datatype if/else refactor.
    for sample in ['dafny0/Maps.dfy', 'dafny0/Datatypes.dfy', 'dafny0/Tuples.dfy',
                   'dafny2/UnionFind.dfy', 'git-issues/git-issue-6531.dfy',
                   'git-issues/github-issue-23-lambda-cost.dfy']:
        if not (LIT / sample).exists():
            results.append({'missing_sample': sample})
            continue
        for refresh in ['false', 'true']:
            dest = OUT / 'sample.bpl'
            r = call([binary, 'verify', sample, '--solver-path', z3, '--no-verify',
                      '--type-system-refresh:' + refresh, '--prelude', ROOT / 'Source/DafnyCore/DafnyPrelude.bpl',
                      '--bprint', dest], LIT)
            results.append(r)
            if dest.exists():
                dest.rename(OUT / (sample.replace('/', '_') + '.' + refresh + '.bpl'))
    (OUT / 'results.json').write_text(json.dumps(results, indent=2))
