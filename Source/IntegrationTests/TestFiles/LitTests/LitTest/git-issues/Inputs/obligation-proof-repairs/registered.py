"""Check the maintained obligation proofs and retain their exact resource scope."""
import argparse
import hashlib
import importlib.util
import json
import os
import re
from pathlib import Path
import subprocess
import signal
import sys
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('assembly', type=Path)
parser.add_argument('solver', type=Path)
parser.add_argument('output', type=Path)
parser.add_argument('--diagnostic', action='store_true')
parser.add_argument('--full-files', action='store_true')
parser.add_argument('--shard', help='Zero-based part/count; keep OFF/default cases together')
parser.add_argument('--capture-verdicts', action='store_true')
args = parser.parse_args()
shard_index, shard_count = (map(int, args.shard.split('/')) if args.shard else (0, 1))
assert shard_count > 0 and 0 <= shard_index < shard_count
# The lit harness has a fifteen-minute wait. End each registered shard first,
# including failure and child cleanup, so an unfinished task cannot count as green.
deadline = time.monotonic() + 720 if args.shard else None
repository = next(p for p in Path(__file__).resolve().parents if (p / 'Source/DafnyStandardLibraries').is_dir())
assembly, solver, output = args.assembly.resolve(), args.solver.resolve(), args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
for stale in ['receipt.json', 'results.json', 'plan.json']:
    (output / stale).unlink(missing_ok=True)
assert '5.1.0' in subprocess.check_output([str(solver), '--version'], text=True)
launch = ['dotnet', str(assembly)]
os.environ['DOTNET_PROCESSOR_COUNT'] = '1'
environment = dict(os.environ)
L = 'Source/IntegrationTests/TestFiles/LitTests/LitTest/'
S = 'Source/DafnyStandardLibraries/src/Std/'
# The project and source attributes own the library ceilings. Suite ceilings
# match the standing verifier gate. No isolation or limit is added by this runner.
targets = [
    ('siftdown', L + 'dafny1/PriorityQueue.dfy', 'PriorityQueue.SiftDown', []),
    ('schorr', L + 'dafny1/SchorrWaite-stages.dfy', 'M2.SchorrWaite', ['--allow-deprecation']),
    ('remove', L + 'dafny4/Primes.dfy', 'RemoveFactor', []),
    ('join', L + 'dafny4/UnionFind.dfy', 'M3.UnionFind.JoinMaintainsReaches1', ['--relax-definite-assignment']),
    ('append', L + 'dafny1/ExtensibleArray.dfy', 'ExtensibleArray.Append', []),
    ('iterator', L + 'dafny2/SnapshotableTrees.dfy', 'SnapTree.Iterator.MoveNext', ['--solver-option', 'O:smt.qi.eager_threshold=80']),
    ('divstrong', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaDivByMultipleIsStronglyOrdered', []),
    ('power', S + 'Arithmetic/Power.dfy', 'Std.Arithmetic.Power.LemmaPowStrictlyIncreases', []),
    ('decode', S + 'Base64.dfy', 'Std.Base64.DecodeValidEncode1Padding', []),
    ('mod', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaModNegNeg', []),
    ('encode', S + 'Base64.dfy', 'Std.Base64.EncodeBVIsBase64', []),
]
helpers = [
    ('initial-path', L + 'dafny1/SchorrWaite-stages.dfy', 'M2.ExtendInitialPath', ['--allow-deprecation']),
    ('mod-multiple', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaModMultiplesBasic', []),
    ('subtract-mod', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaSubModNoopRight', []),
    ('distribute', S + 'Arithmetic/Mul.dfy', 'Std.Arithmetic.Mul.LemmaMulIsDistributiveSub', []),
    ('multiply-one', S + 'Arithmetic/Mul.dfy', 'Std.Arithmetic.Mul.LemmaMulBasics', []),
    ('quotient-remainder-converse', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaFundamentalDivModConverse', []),
    ('shifted-quotient', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaDivMultiplesVanishFancy', []),
    ('fundamental-division', S + 'Arithmetic/DivMod.dfy', 'Std.Arithmetic.DivMod.LemmaFundamentalDivMod', []),
    ('commutation', S + 'Arithmetic/Mul.dfy', 'Std.Arithmetic.Mul.LemmaMulIsCommutative', []),
    ('encode-padding-identity', S + 'Base64.dfy', 'Std.Base64.EncodeBVIs1Padded', []),
    ('unpadded-prefix', S + 'Base64.dfy', 'Std.Base64.UnpaddedBase64Prefix', []),
    ('recursive-decoder-support', S + 'Base64.dfy', 'Std.Base64.DecodeEncodeRecursively', []),
    ('two-padding-support', S + 'Base64.dfy', 'Std.Base64.DecodeValidEncode2Padding', []),
    ('encode-two-padding-identity', S + 'Base64.dfy', 'Std.Base64.EncodeBVIs2Padded', []),
    ('decode-two-padding-inverse', S + 'Base64.dfy', 'Std.Base64.DecodeEncode2Padding', []),
    ('decode-valid-decomposition', S + 'Base64.dfy', 'Std.Base64.AboutDecodeValid', []),
    ('encode-length-multiple', S + 'Base64.dfy', 'Std.Base64.EncodeBVLengthCongruentToZeroMod4', []),
    ('decode-one-padding-prefix', S + 'Base64.dfy', 'Std.Base64.DecodeValidUnpaddedPartialFrom1PaddedSeq', []),
]
base64_source = (repository / (S + 'Base64.dfy')).read_text()
for helper in ['UnpaddedPrefixWithPaddingIsBase64', 'UnpaddedStringIsBase64', 'DecodeValid1PaddingLengthMod3', 'DecodeValid2PaddingLengthMod3']:
    if 'lemma ' + helper + '(' in base64_source:
        helpers.append((helper, S + 'Base64.dfy', 'Std.Base64.' + helper, []))
rows = []
active_process = None
SAFETY_SECONDS = 900

def stop_child(signum, frame):
    if active_process is not None:
        try:
            os.killpg(active_process.pid, signum)
        except ProcessLookupError:
            pass
    raise SystemExit(128 + signum)

for stop_signal in [signal.SIGTERM, signal.SIGINT, signal.SIGHUP]:
    signal.signal(stop_signal, stop_child)

def run_verifier(command):
    global active_process
    active_process = subprocess.Popen(command, cwd=repository, env=environment,
        text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
    try:
        try:
            remaining = SAFETY_SECONDS if deadline is None else max(0.001, deadline - time.monotonic())
            stdout, stderr = active_process.communicate(timeout=min(SAFETY_SECONDS, remaining))
            return stdout + stderr, active_process.returncode
        except subprocess.TimeoutExpired:
            try:
                os.killpg(active_process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            try:
                stdout, stderr = active_process.communicate(timeout=5)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(active_process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                stdout, stderr = active_process.communicate()
            return stdout + stderr, 124
    finally:
        active_process = None

def public_coverage_receipt(identity):
    # Public coverage contains only canonical digests, counts, and booleans.
    # Keep arbitrary compiler output and execution metadata out of this summary.
    result = {}
    for key, width in [('source', 40), ('compiler_core_sha256', 64), ('solver_sha256', 64)]:
        value = identity[key]
        if value is None and key == 'source':
            result[key] = None
        elif isinstance(value, str) and re.fullmatch('[0-9a-f]{' + str(width) + '}', value):
            result[key] = value
        else:
            raise ValueError('Public coverage requires canonical digests')
    for key in ['invocations', 'expected_invocations', 'shard_index', 'shard_count']:
        result[key] = identity[key]
    result['complete'] = identity['complete']
    result['all_selected_correct'] = identity['all_selected_correct']
    result['default_off_equal'] = identity['default_off_equal']
    if any(type(result[key]) is not int or result[key] < 0 for key in ['invocations', 'expected_invocations', 'shard_index', 'shard_count']) or any(
        type(result[key]) is not bool for key in ['complete', 'all_selected_correct', 'default_off_equal']):
        raise ValueError('Public coverage requires counts and booleans')
    if result['shard_count'] < 1 or result['shard_index'] >= result['shard_count'] or result['complete'] != (result['invocations'] == result['expected_invocations']):
        raise ValueError('Public coverage requires a consistent completed partition')
    return result

def verify(label, source, symbol, extra, refresh, axioms, mode, full=False):
    folder = output / label
    folder.mkdir(exist_ok=True)
    for old in [folder / 'results.json', folder / 'resources.csv']:
        old.unlink(missing_ok=True)
    library = source.startswith(S)
    command = launch + ['verify', S + 'dfyconfig.toml' if library else source,
        '--solver-path', str(solver), '--cores', '1', '--verification-time-limit', '0', '--progress', 'Symbol',
        '--log-format', 'json;LogFileName=' + str(folder / 'results.json'),
        '--log-format', 'csv;LogFileName=' + str(folder / 'resources.csv'),
        '--additional-axioms:' + str(axioms).lower()]
    if mode != 'default':
        command += ['--consistent-obligation-checks:' + str(mode == 'on').lower()]
    if not library:
        command += ['--resource-limit', '16000000', '--boogie', '/normalizeDeclarationOrder:0',
            '--allow-warnings', '--type-system-refresh:' + str(refresh).lower(),
            '--general-newtypes:' + str(refresh).lower()] + extra
    if full and library:
        # Resolve with the ordinary project and check every declaration in this file.
        command += ['--filter-position', source]
    elif not full:
        command += ['--filter-symbol', symbol + '.']
    (folder / 'command.json').write_text(json.dumps(command, indent=2) + '\n')
    text, code = run_verifier(command)
    (folder / 'output.txt').write_text(text)
    (folder / 'exit.txt').write_text(str(code) + '\n')
    log = folder / 'results.json'
    log_error = None
    try:
        declarations = json.loads(log.read_text()).get('verificationResults', []) if log.exists() and log.stat().st_size else []
    except json.JSONDecodeError as error:
        declarations, log_error = [], str(error)
    batches = [vc for d in declarations for vc in d['vcResults']]
    costs = [vc['resourceCount'] for vc in batches]
    wanted = [symbol]
    if full:
        wanted = [item[2] for item in targets + helpers if item[1] == source]
    selected = declarations if not full else [d for d in declarations if any(d['name'].startswith(name + ' (') for name in wanted)]
    good = all(any(d['name'].startswith(name + ' (') for d in selected) for name in wanted) and bool(selected) and all(d['outcome'] == 'Correct' and d['vcResults'] and
        all(vc['outcome'] == 'Valid' for vc in d['vcResults']) for d in selected)
    good = good and all(d['outcome'] == 'Correct' for d in declarations if d['name'].endswith('(well-formedness)'))
    # Complete scopes may retain the source files' documented negative checks.
    # A timeout, signal, or driver failure cannot count as a completed scope.
    good = good and (code in [0, 4] if full else code == 0) and not any(
        word in text.lower() for word in ['unhandled exception', 'internal error', 'parse errors'])
    row = {'name': label, 'source': source, 'symbol': symbol, 'refresh': refresh,
        'additional_axioms': axioms, 'mode': mode, 'complete_scope': full,
        'exit': code, 'selected_correct': good, 'log_error': log_error, 'declarations': len(declarations),
        'batches': len(batches), 'total_ru': sum(costs), 'max_ru': max(costs, default=0),
        'outcomes': {d['name']: d['outcome'] for d in declarations},
        'batch_results': [(d['name'], vc['vcNum'], vc['outcome'], vc['resourceCount'])
            for d in declarations for vc in d['vcResults']]}
    rows.append(row)
    (output / 'results.json').write_text(json.dumps(rows, indent=2) + '\n')
    print(label, 'actual exit', code, 'selected proofs correct', good,
        'batches', row['batches'], 'total RU', row['total_ru'], 'max RU', row['max_ru'], file=sys.stderr, flush=True)
    return row

def build_plan():
    groups = []
    for name, source, symbol, extra in targets + helpers:
        for refresh in ([True] if source.startswith(S) else [False, True]):
            for axioms in [False, True]:
                groups.append([(f'{name}-r{int(refresh)}-a{int(axioms)}-{mode}', source, symbol, extra,
                    refresh, axioms, mode, False) for mode in ['on', 'off', 'default']])
    if args.full_files:
        seen = set()
        for name, source, symbol, extra in targets:
            for refresh in ([True] if source.startswith(S) else [False, True]):
                for axioms in [False, True]:
                    key = (source, refresh, axioms)
                    if key in seen:
                        continue
                    seen.add(key)
                    groups.append([(f'complete-{name}-r{int(refresh)}-a{int(axioms)}-{mode}', source, symbol, extra,
                        refresh, axioms, mode, True) for mode in ['on', 'off']])
    return groups

plan = [case for index, group in enumerate(build_plan()) if index % shard_count == shard_index for case in group]
assert plan
(output / 'plan.json').write_text(json.dumps([case[0] for case in plan], indent=2) + '\n')
for case in plan:
    if deadline is not None and time.monotonic() >= deadline:
        raise TimeoutError('Registered proof shard exceeded its safety cap')
    verify(*case)
assert len(rows) == len(plan)

if args.capture_verdicts:
    # This optional diagnostic records the actual standing-gate verdict rows.
    # Expected tables are updated separately, after each changed row is reviewed.
    assert args.diagnostic
    import contextlib
    import re
    import urllib.request
    import zipfile

    def load_runner(name):
        spec = importlib.util.spec_from_file_location(name, repository / ('.github/review/' + name + '-verdicts.py'))
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    capture = output / 'standing-verdicts'
    capture.mkdir(exist_ok=True)
    launcher = capture / 'dafny'
    launcher.write_text('#!/usr/bin/env python3\nimport os,sys\nos.execvp("dotnet", ["dotnet", ' + repr(str(assembly)) + '] + sys.argv[1:])\n')
    launcher.chmod(0o755)
    lit, std = load_runner('lit'), load_runner('std')
    complete_plan = capture / 'complete-plan.tsv'
    with (capture / 'plan-output.txt').open('w') as stream, contextlib.redirect_stdout(stream):
        lit.plan(str(repository / L), str(complete_plan))
    selected_paths = {source.removeprefix(L) for _, source, _, _ in targets if source.startswith(L)}
    plan = capture / 'plan.tsv'
    plan.write_text(''.join(line + '\n' for line in complete_plan.read_text().splitlines() if line.split('\t')[0] in selected_paths))
    assert len(plan.read_text().splitlines()) == len(selected_paths)
    reference = re.search(r'DefaultZ3Version = "([^"]+)"', (repository / 'Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
    hashes = {'4.12.1': 'e07d4193d8e1851114fe4d507332a43576639f4aeaa33c7df15c36426d3cbf3b',
        '4.16.0': '87de9c9e86c877abfccb00cf24b946c4a1bda7b9cba6f363ea58311d2573c320'}
    assert reference in hashes
    archive = capture / 'reference-solver.zip'
    urllib.request.urlretrieve('https://github.com/dafny-lang/solver-builds/releases/download/snapshot-2026-04-03/z3-' + reference + '-x64-ubuntu-24.04-bin.zip', archive)
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == hashes[reference]
    with zipfile.ZipFile(archive) as zipped:
        zipped.extractall(capture / 'reference-solver')
    reference_solver = capture / 'reference-solver' / ('z3-' + reference)
    reference_solver.chmod(0o755)
    for axioms in [False, True]:
        label = 'axioms-' + str(axioms).lower()
        folder = capture / ('suite-' + label)
        folder.mkdir(exist_ok=True)
        actual = folder / 'verdicts.tsv'
        with (folder / 'output.txt').open('w') as stream, contextlib.redirect_stdout(stream):
            lit.run(str(plan), str(repository / L), str(launcher), str(solver), str(actual), jobs=1,
                additional_axioms=axioms, measurements=str(folder / 'measurements'))
        with (folder / 'expected-verdicts.tsv').open('w') as stream, contextlib.redirect_stdout(stream):
            lit.expected([str(actual)])
        for version, selected_solver in [('5.1.0', solver), (reference, reference_solver)]:
            folder = capture / ('library-' + label + '-z3-' + version)
            folder.mkdir(exist_ok=True)
            actual = folder / 'verdicts.tsv'
            with (folder / 'output.txt').open('w') as stream, contextlib.redirect_stdout(stream):
                std.run(str(repository / 'Source/DafnyStandardLibraries'), str(launcher), str(selected_solver), str(actual), cores=4, additional_axioms=axioms)
            with (folder / 'expected-verdicts.tsv').open('w') as stream, contextlib.redirect_stdout(stream):
                std.expected(str(actual))

try:
    source_revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repository, text=True, stderr=subprocess.DEVNULL).strip()
except subprocess.CalledProcessError:
    source_revision = None
identity = {'source': source_revision,
    'compiler_version': subprocess.check_output(launch + ['--version'], text=True).strip(),
    'compiler_core_sha256': hashlib.sha256(assembly.with_name('DafnyCore.dll').read_bytes()).hexdigest(),
    'solver_version': subprocess.check_output([str(solver), '--version'], text=True).strip(),
    'solver_sha256': hashlib.sha256(solver.read_bytes()).hexdigest(), 'diagnostic': args.diagnostic,
    'all_selected_correct': all(row['selected_correct'] for row in rows), 'invocations': len(rows),
    'shard_index': shard_index, 'shard_count': shard_count, 'expected_invocations': len(plan), 'complete': len(rows) == len(plan)}
by_name = {row['name']: row for row in rows}
identity['default_off_differences'] = [row['name'] for row in rows if row['mode'] == 'default' and
    row['batch_results'] != by_name[row['name'].removesuffix('default') + 'off']['batch_results']]
identity['default_off_equal'] = not identity['default_off_differences']
(output / 'receipt.json').write_text(json.dumps(identity, indent=2) + '\n')
print('Proof coverage', json.dumps(public_coverage_receipt(identity), sort_keys=True), file=sys.stderr, flush=True)
summary = os.environ.get('GITHUB_STEP_SUMMARY')
if os.environ.get('GITHUB_ACTIONS') == 'true' and summary:
    # Publish only validated public identity and numeric proof-scope statistics.
    coverage = public_coverage_receipt(identity)
    lines = ['## Maintained obligation proofs', '',
        'Coverage: `' + json.dumps(coverage, sort_keys=True) + '`', '',
        '| case | exit | selected proofs correct | batches | total RU | maximum batch RU |',
        '|---|---:|---|---:|---:|---:|']
    for row in rows:
        assert re.fullmatch('[A-Za-z0-9-]+', row['name'])
        assert type(row['exit']) is int and type(row['selected_correct']) is bool
        assert all(type(row[key]) is int and row[key] >= 0 for key in ['batches', 'total_ru', 'max_ru'])
        lines.append('| ' + row['name'] + ' | ' + str(row['exit']) + ' | ' +
            str(row['selected_correct']) + ' | ' + str(row['batches']) + ' | ' +
            str(row['total_ru']) + ' | ' + str(row['max_ru']) + ' |')
    with Path(summary).open('a') as stream:
        stream.write('\n'.join(lines) + '\n')
if not args.diagnostic:
    assert identity['default_off_equal'], identity['default_off_differences']
    assert identity['complete'] and identity['all_selected_correct'], json.dumps([{'name': r['name'], 'exit': r['exit']} for r in rows if not r['selected_correct']], indent=2)
    print('PASS maintained obligation proofs in the selected partition')
    print('PASS selected target and helper obligations in completed scopes')
    print('PASS default and explicit OFF compatibility')
