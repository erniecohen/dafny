#!/usr/bin/env python3
"""Recursive groups retain complete library semantics through all persistent cache layers."""
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tomllib
import zipfile

exe, solver, fixture, destination = (Path(p).resolve() for p in sys.argv[1:])
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True)
DOTNET = shutil.which('dotnet')
checks = []


def invoke(command):
    child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                             text=True, start_new_session=True)
    try:
        text, _ = child.communicate(timeout=180)
        return child.returncode, text
    finally:
        if child.poll() is None:
            os.killpg(child.pid, signal.SIGTERM)
            try:
                child.wait(timeout=3)
            except subprocess.TimeoutExpired:
                pass
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass


def run(label, inputs, *, cache=None, audit=False, graphs=False, persistent=False,
        expected=0, threads=0, axioms=False, library=None, command='verify', extra=()):
    output = destination / label
    output.mkdir()
    profile = output / 'profile.jsonl'
    args = [DOTNET, str(exe), command, *map(str, inputs), '--standard-libraries:false',
            '--type-system-refresh', '--member-invariants', '--recursive-modules',
            '--show-snippets:false', '--use-basename-for-filename',
            '--solver-path', str(solver), '--pipeline-profile', str(profile)]
    if command != 'resolve':
        args += ['--additional-axioms:' + str(axioms).lower()]
    if threads:
        args += ['--resolution-threads', str(threads)]
    if library:
        args += ['--library', str(library)]
    if command != 'resolve':
        args += ['--cores', '2', '--verification-time-limit', '0', '--resource-limit', '1000000',
                 '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver',
                 '--log-format', 'json;LogFileName=' + str(output / 'proof.json')]
    if cache:
        args += ['--cache-dir', str(cache)]
    else:
        args += ['--resolution-cache:false']
    if persistent:
        args += ['--persistent-translation', '--translation-global-inputs']
    if audit:
        args += ['--resolution-cache-check']
        if persistent:
            args += ['--persistent-translation-audit']
    if graphs:
        args += ['--bprint', str(output / 'out.bpl')]
    rc, text = invoke(args + list(map(str, extra)))
    (output / 'run.log').write_text(text)
    assert rc == expected, (label, rc, expected, text)
    assert not re.search(r'out of resource|timed out|inconclusive|Warning:|Unhandled exception', text, re.I), label
    events = [json.loads(line) for line in profile.read_text().splitlines()] if profile.exists() else []
    snapshots = [e for e in events if e.get('stage') == 'resolve.snapshot']
    record = {'exit': rc, 'diagnostics': text}
    if command != 'resolve' and expected in (0, 4):
        results = json.loads((output / 'proof.json').read_text())['verificationResults']
        record['costs'] = sorted((r['name'], v['vcNum'], v['outcome'], v['resourceCount'])
                                 for r in results for v in r['vcResults'])
        assert record['costs'], label
        assert all(v[2] in ('Valid', 'Invalid', 'Passed', 'Failed') for v in record['costs']), label
    if graphs:
        record['graphs'] = {p.name: p.read_bytes() for p in output.glob('*.bpl')}
        assert record['graphs'], label
    checks.append({'label': label, 'scopes': len(record.get('costs', []))})
    return record, snapshots, events


def group(snapshots, members):
    selected = [e for e in snapshots if e['kind'] == 'module' and
                e['module'].removeprefix('_module.') in members]
    assert len(selected) == len(members), (members, snapshots)
    assert len({e['key'] for e in selected}) == 1, selected
    return selected


def hit(snapshots, members, audit=False):
    selected = group(snapshots, members)
    assert all(e['state'].startswith('hit (checked ' if audit else 'hit') for e in selected), selected
    assert sum(e['resolverCalls'] for e in selected) == (1 if audit else 0), selected
    return selected[0]['key']


def compare(label, inputs, members, *, threads=0, axioms=False, library=None):
    for graphs in (True, False):
        cache = destination / (label + ('-resolution' if graphs else '-composed'))
        observations = []
        for phase in ('fresh', 'cold', 'warm', 'audit'):
            result, snapshots, events = run(label + '-' + str(graphs) + '-' + phase, inputs,
                cache=None if phase == 'fresh' else cache, audit=phase == 'audit',
                graphs=graphs, persistent=not graphs and phase != 'fresh',
                threads=threads, axioms=axioms, library=library)
            observations.append(result)
            if phase in ('warm', 'audit'):
                hit(snapshots, members, phase == 'audit')
                assert snapshots and all(e['state'].startswith('hit (checked ' if phase == 'audit' else 'hit')
                                         for e in snapshots), snapshots
            if phase == 'warm' and not graphs:
                modules = [e for e in events if e.get('ev') == 'persistent-module']
                assert modules and all(e['hit'] == 'translation-key' for e in modules), modules
                counters = [e for e in events if e.get('ev') == 'result-cache']
                assert len(counters) == 1 and counters[0]['solverStarts'] == 0, counters
            if phase == 'audit' and not graphs:
                counters = [e for e in events if e.get('ev') == 'persistent-translation']
                assert len(counters) == 1 and counters[0]['auditChecks'] > 0 and counters[0]['auditMismatches'] == 0, counters
        assert all(r == observations[0] for r in observations), label


original = fixture.read_text().replace('module B {', '''module B {
  lemma Private() { assert true; }
  function Choice(): bool { true }
''', 1)
client_text = '''module Client {
  import A
  import B
  lemma Facts(t: A.T) { assert t.n >= 0; assert A.Count(0) == 0; }
  lemma Guard() { if !B.Choice() { assert false; } }
}
'''
producer = destination / 'producer.dfy'
producer.write_text(original)
client = destination / 'client.dfy'
client.write_text(client_text)
outside = destination / 'outside.dfy'
outside.write_text('module Outside { lemma Proof() { assert true; } }\n')
inputs = [producer, client, outside]
for axioms in (False, True):
    for threads in (0, 3):
        compare('source-' + str(axioms) + '-' + str(threads), inputs, ['A', 'B'],
                threads=threads, axioms=axioms)

# Abstract groups and higher refinements must observe the accepted internal import parents.
compare('refinement', [fixture.with_name('git-issue-466-stage5-positive.dfy')],
        ['Left', 'Right'], threads=3)

# A group belongs wholly to its archive; loading it still reruns archive admission.
library = destination / 'group.doo'
run('producer', [producer], command='build', extra=['--target', 'lib', '--output', library])
for threads in (0, 3):
    compare('library-' + str(threads), [client], ['A', 'B'], threads=threads, library=library)
with zipfile.ZipFile(library) as archive:
    entries = {name: archive.read(name) for name in archive.namelist()}
manifest = tomllib.loads(entries['manifest.toml'].decode('utf-8-sig'))
record = copy.deepcopy(manifest['recursive_modules'])
record['groups'] = []
canonical = {'Schema': record['schema'], 'ProgramSha256': record['program_sha256'],
             'ManifestSha256': record['manifest_sha256'], 'Groups': []}
record['record_sha256'] = hashlib.sha256(json.dumps(canonical, separators=(',', ':')).encode()).hexdigest()
text = entries['manifest.toml'].decode('utf-8-sig')
text = re.sub(r'(?m)^groups = .*$', 'groups = []', text)
text = re.sub(r'(?m)^record_sha256 = .*$', 'record_sha256 = ' + json.dumps(record['record_sha256']), text)
with zipfile.ZipFile(library, 'w') as archive:
    for name, data in entries.items():
        archive.writestr(name, text if name == 'manifest.toml' else data)
run('warm-rehashed-inaccurate-inventory', [client], cache=destination / 'library-0-composed',
    command='resolve', expected=2, library=library)

# Edits invalidate the complete group but preserve unrelated inputs and proof-task early cutoff.
cache = destination / 'edits-cache'
run('edits-cold', inputs, cache=cache, persistent=True)
_, snapshots, _ = run('edits-warm', inputs, cache=cache, persistent=True)
old_key = hit(snapshots, ['A', 'B'])
producer.write_text(original.replace('assert true;', 'assert 0 == 0;'))
fresh, _, _ = run('private-edit-fresh', inputs)
actual, snapshots, events = run('private-edit', inputs, cache=cache, persistent=True)
assert actual == fresh
selected = group(snapshots, ['A', 'B'])
assert all(not e['state'].startswith('hit') and e['key'] != old_key for e in selected), selected
assert sum(e['resolverCalls'] for e in selected) == 1, selected
translated = {e['module'] for e in events if e.get('ev') == 'span' and e.get('stage') == 'translate'}
assert {'A', 'B'} <= translated, translated
assert any(e.get('ev') == 'persistent-module' and e['hit'] == 'boogie-graph' for e in events), [
    e for e in events if e.get('ev') == 'persistent-module']
new_key = selected[0]['key']
outside.write_text('module Outside { lemma Proof() { assert 0 == 0; } }\n')
fresh, _, _ = run('outside-edit-fresh', inputs)
actual, snapshots, _ = run('outside-edit', inputs, cache=cache, persistent=True)
assert actual == fresh and hit(snapshots, ['A', 'B']) == new_key
# Position-only changes keep the entry and must rebase every sibling's source tokens.
producer.write_text('\n\n' + producer.read_text())
fresh, _, _ = run('moved-fresh', inputs, graphs=True)
actual, snapshots, _ = run('moved-warm', inputs, graphs=True, cache=cache, audit=True)
assert actual == fresh and hit(snapshots, ['A', 'B'], True) == new_key
# A cached proof of a guarded false assertion cannot survive a changed sibling definition.
producer.write_text(producer.read_text().replace('Choice(): bool { true }', 'Choice(): bool { false }'))
fresh, _, _ = run('changed-guard-fresh', inputs, expected=4)
actual, snapshots, _ = run('changed-guard', inputs, cache=cache, persistent=True, expected=4)
assert actual == fresh
assert all(e['key'] != new_key for e in group(snapshots, ['A', 'B']))
# Unconditional reachable false assertions retain the same failed VCs on every cache path.
client.write_text(client_text + '\nmodule Negative { import A lemma Bad(t:A.T) { assert false; } }\n')
for phase in ('fresh', 'cold', 'warm', 'audit'):
    record, snapshots, _ = run('false-' + phase, inputs, cache=None if phase == 'fresh' else cache,
        audit=phase == 'audit', expected=4, graphs=True)
    if phase == 'fresh':
        baseline = record
    else:
        assert record == baseline
    if phase in ('warm', 'audit'):
        hit(snapshots, ['A', 'B'], phase == 'audit')
# Artifact corruption is a miss, never an unchecked hit.
for path in (cache / 'v1/artifacts/resolutions').glob('*/*'):
    if not path.name.startswith('.'):
        path.write_bytes(b'corrupt snapshot')
record, snapshots, _ = run('corrupt-fallback', inputs, cache=cache, expected=4, graphs=True)
assert record == baseline
assert all(not e['state'].startswith('hit') for e in group(snapshots, ['A', 'B']))
(destination / 'summary.json').write_text(json.dumps(checks, indent=2) + '\n')
print('recursive persistent groups:', len(checks), 'controls passed')
