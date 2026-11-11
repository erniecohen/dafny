#!/usr/bin/env python3
"""CI-only evidence for https://github.com/erniecohen/dafny/issues/23.

Compare only the glue guard using one binary and identical source inputs.
The separately built candidate checks the C# refactor and regression fixtures.
Probe exit codes are evidence, never a substitute for the normal review gate.
"""
import importlib.util
import hashlib
import os
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
(OUT / 'manifest.json').write_text(json.dumps({
    'candidate_source': os.environ.get('GITHUB_SHA'),
    'baseline_source': '428e348b0b125cb67505f03a0026b3f497f98e48',
    'variant': variant, 'scope': scope,
    'prelude_sha256': hashlib.sha256(prelude_path.read_bytes()).hexdigest(),
    'z3_sha256': hashlib.sha256(Path(z3).read_bytes()).hexdigest(),
    'binaries': {v: hashlib.sha256((ROOT / 'out' / v / 'DafnyCore.dll').read_bytes()).hexdigest()
                 for v in ['before', 'after']}}, indent=2))
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
                   '--type-system-refresh:' + refresh, '--general-traits:' + ('datatype' if refresh == 'true' else 'legacy'),
                   '--general-newtypes:' + refresh,
                   '--use-basename-for-filename', '--show-snippets:false', '--standard-libraries:false',
                   '--cores:2', '--verification-time-limit:300', '--resource-limit:50e6']
            r = call(cmd, fixture.parent)
            results.append(r)
            expected = fixture.with_suffix('.dfy.expect').read_text()
            expected_exit = 4 if '%exits-with 4' in fixture.read_text().splitlines()[0] else 0
            if r['exit'] != expected_exit or r['stdout'] + r['stderr'] != expected:
                raise RuntimeError('Fixture differs: ' + json.dumps(r))
            (OUT / (fixture.name + '.' + refresh + '.expect')).write_text(r['stdout'] + r['stderr'])
    # Exact bprint comparison, with identical paths/options/prelude in both builds.
    # The only generating-code difference is the datatype if/else refactor.
    for sample in ['dafny0/Maps.dfy', 'dafny0/Datatypes.dfy', 'dafny0/Tuples.dfy',
                   'dafny4/UnionFind.dfy', 'git-issues/git-issue-6531.dfy',
                   'git-issues/github-issue-23-lambda-cost.dfy']:
        if not (LIT / sample).exists():
            results.append({'missing_sample': sample})
            continue
        for refresh in ['false', 'true']:
            dest = OUT / 'sample.bpl'
            r = call([binary, sample, '/compile:0', '/noVerify',
                      '/typeSystemRefresh:' + ('1' if refresh == 'true' else '0'),
                      '/dprelude:' + str(ROOT / 'Source/DafnyCore/DafnyPrelude.bpl'),
                      '/env:0', '/print:' + str(dest)], LIT)
            results.append(r)
            printed = sorted(OUT.glob('sample*.bpl'))
            if r['exit'] != 0 or not printed:
                raise RuntimeError('Boogie translation failed: ' + json.dumps(r))
            for generated in printed:
                suffix = generated.name[len('sample'):]
                generated.rename(OUT / (sample.replace('/', '_') + '.' + refresh + suffix))
    # Exact suffix filters: a trailing dot requests EndsWith, avoiding JoinMaintains*.
    # Keep the same baseline binary and switch only the two prelude guards.
    for sample, symbol in [('dafny0/Maps.dfy', 'GeneralMaps4.'),
                           ('dafny4/UnionFind.dfy', 'M3.UnionFind.Join.')]:
        for refresh in ['false', 'true']:
            log = OUT / (symbol + refresh + '.json')
            r = call([ROOT / 'out/before/Dafny', 'verify', sample, '--solver-path', z3,
                      '--prelude', prelude_path, '--filter-symbol', symbol,
                      '--type-system-refresh:' + refresh, '--allow-warnings',
                      '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver',
                      '--resource-limit', '100000000', '--verification-time-limit', '0', '--cores', '1',
                      '--log-format', 'json;LogFileName=' + str(log)], LIT)
            results.append(r)
    for sample in ['dafny0/Maps.dfy', 'dafny0/Datatypes.dfy',
                   'git-issues/github-issue-23-box-bad.dfy',
                   'git-issues/github-issue-23-lambda-cost.dfy']:
        for encoding in ['a', 'm']:
            log = OUT / (sample.replace('/', '_') + '.' + encoding + '.json')
            r = call([binary, 'verify', sample, '--solver-path', z3,
                      '--boogie', '/typeEncoding:' + encoding, '--boogie', '/deterministicLiteralHashes:1',
                      '--boogie', '/restartProver', '--resource-limit', '16000000',
                      '--verification-time-limit', '60', '--cores', '1',
                      '--log-format', 'json;LogFileName=' + str(log)], LIT)
            results.append(r)
    (OUT / 'results.json').write_text(json.dumps(results, indent=2))
