"""Fail-closed validation of the full OFF suite against original run inventory.

Only executable, solver and observation-file locations are abstracted in command
comparisons. Raw commands, process output and verification logs remain untouched.
"""
import hashlib
import json
from pathlib import Path
import re
import shlex

import importlib.util
spec = importlib.util.spec_from_file_location('lit_verdicts', Path(__file__).with_name('lit-verdicts.py'))
lit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lit)


def require(condition, message):
    if not condition:
        raise ValueError('Incomplete suite measurement: ' + message)


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def read_rows(path, columns):
    result = {}
    for line in path.read_text().splitlines():
        row = line.split('\t')
        require(len(row) == columns, 'malformed row in ' + path.name)
        require(row[0] not in result, 'duplicate program in ' + path.name + ': ' + row[0])
        result[row[0]] = row[1:]
    return result


def diagnostics(output):
    # CLI help changes when options are added. Keep the actual error/warning lines
    # and required-argument failures, rather than treating an arbitrary exit 1 as
    # equivalent to the original intentional diagnostic case.
    return [line for line in output.splitlines() if re.search(
        r'\(\d+,\d+\): (?:Error|Warning)|^CLI: (?:Error|Warning):|^Required argument missing|^Unrecognized', line)]


def diagnostic_profile(output):
    lines = diagnostics(output)
    return {'count': len(lines), 'sha256': digest(lines)}


def expected_command(program, options, dafny, solver, log):
    # Mirror lit-verdicts.verify without invoking a verifier. Program-owned proof
    # options override FIXED exactly as they do in the unchanged ordinary runner.
    own = shlex.split(options)
    names = {token.split('=')[0].split(':')[0] for token in own if token.startswith('--')}
    fixed, index = [], 0
    while index < len(lit.FIXED):
        name = lit.FIXED[index]
        takes = index + 1 < len(lit.FIXED) and not lit.FIXED[index + 1].startswith('--')
        if name not in names:
            fixed += lit.FIXED[index:index + 2] if takes else [name]
        index += 2 if takes else 1
    return [str(dafny), 'verify', program, '--solver-path', str(solver)] + fixed + [
        '--log-format', 'json;LogFileName=' + str(log)] + own


def proof_identity(raw):
    return sorted([[declaration['name'], proof['vcNum'], proof.get('randomSeed', 0), proof['assertions']]
                   for declaration in raw for proof in declaration['vcResults']], key=lambda row: row[:3])


def profile(raw):
    scopes = {declaration['name']: len(declaration['vcResults']) for declaration in raw}
    return {'declarations': len(scopes), 'vcs': sum(scopes.values()),
            'scope_vc_counts_sha256': digest(sorted(scopes.items())),
            'vc_identity_sha256': digest(proof_identity(raw))}


def vector(directory, inventory, dafny, solver, merge_outcomes):
    plan = read_rows(directory / 'plan.tsv', 2)
    require(list(plan.items()) == list(inventory['plan'].items()), 'plan differs from authoritative ordered baseline')
    rows = read_rows(directory / 'verdicts.tsv', 5)
    require(set(rows) == set(plan) == set(inventory['programs']), 'program population differs from authoritative baseline')
    measurements = directory / 'measurements'
    actual = {path.parent.relative_to(measurements).as_posix() for path in measurements.rglob('command.json')}
    require(actual == set(plan), 'missing or unexpected per-program command')
    for filename in ['output.txt', 'results.json']:
        require({path.parent.relative_to(measurements).as_posix() for path in measurements.rglob(filename)} <= set(plan),
                'unexpected program observations')
    verdicts, commands, declarations, vcs, nonproof = {}, {}, {}, {}, {}
    for program, options in plan.items():
        expected = inventory['programs'][program]
        folder = measurements / program
        log = folder / 'results.json'
        command = json.loads((folder / 'command.json').read_text())
        require(command == expected_command(program, options[0], dafny, solver, log.resolve()),
                program + ': actual command differs from original proof options')
        canonical = command.copy()
        canonical[0], canonical[4] = '<dafny>', '<solver>'
        canonical[canonical.index('--log-format') + 1] = 'json;LogFileName=<observation>'
        commands[program] = canonical
        output = (folder / 'output.txt').read_text()
        row = rows[program]
        require(row[0] != 'TIMEOUT', program + ': wall-clock safety cap')
        require(float(row[3]) >= 0, program + ': invalid process duration')
        summaries = re.findall(r'verifier finished with ([^\n]*)', output)
        errors = sorted(set(re.findall(r'\((\d+),\d+\): Error', output)), key=int)
        oor = len(re.findall(r'out of resource', output))
        observed = [row[0], ''.join(summaries) + ('; %d out of resource' % oor if oor else ''), ','.join(errors)]
        require(row[:3] == observed, program + ': TSV disagrees with actual output')
        verdicts[program] = observed
        mode = 'missing' if not log.exists() else 'blank' if not log.read_text().strip() else 'json'
        raw = json.loads(log.read_text())['verificationResults'] if mode == 'json' else []
        mode = ('proof' if raw else 'empty') if mode == 'json' else mode
        require(mode == expected['mode'], program + ': original proof/diagnostic population changed')
        if mode != 'proof':
            require(observed == expected['verdict'] and diagnostic_profile(output) == expected['diagnostics'],
                    program + ': intentional diagnostic/zero-proof outcome differs from original baseline')
            nonproof[program] = {'mode': mode, 'verdict': observed, 'diagnostics': diagnostics(output)}
            continue
        outcomes = []
        for declaration in raw:
            scope = program + '\t' + declaration['name']
            require(scope not in declarations, 'duplicate scope: ' + scope)
            proofs = declaration['vcResults']
            require(proofs, 'empty scope: ' + scope)
            folded, resources = 'Correct', 0
            for proof in proofs:
                key = scope + '\t' + str(proof['vcNum'])
                require(key not in vcs, 'duplicate VC: ' + key)
                require(type(proof['vcNum']) is int and type(proof.get('randomSeed', 0)) is int,
                        scope + ': invalid VC identity')
                require(type(proof['resourceCount']) is int and proof['resourceCount'] >= 0, scope + ': invalid VC cost')
                require(isinstance(proof['assertions'], list), scope + ': missing assertion identities')
                folded = merge_outcomes(folded, proof['outcome'])
                resources += proof['resourceCount']
                outcomes.append(proof['outcome'])
                vcs[key] = [proof['outcome'], proof['resourceCount'], proof['assertions'], proof.get('randomSeed', 0)]
            require(declaration['outcome'] == folded and declaration['resourceCount'] == resources,
                    scope + ': declaration disagrees with raw VCs')
            declarations[scope] = [folded, resources, len(proofs)]
        require(profile(raw) == expected['profile'], program + ': scope/VC/assertion inventory differs from authoritative baseline')
        require(row[0] == ('4' if any(outcome != 'Valid' for outcome in outcomes) else '0'),
                program + ': process exit disagrees with verification outcomes')
        summary = re.fullmatch(r'(\d+) verified, (\d+) errors?(.*)', summaries[0]) if len(summaries) == 1 else None
        require(summary is not None and int(summary[1]) == outcomes.count('Valid'), program + ': inconsistent proof summary')
        invalid = outcomes.count('Invalid')
        require(int(summary[2]) >= invalid if invalid else int(summary[2]) == 0, program + ': inconsistent error summary')
        tail = ''
        for outcome, label, plural in [('Undetermined', 'inconclusive', True), ('TimeOut', 'time out', True),
                                       ('OutOfMemory', 'out of memory', False), ('OutOfResource', 'out of resource', False)]:
            count = outcomes.count(outcome)
            if count:
                tail += ', ' + str(count) + ' ' + label + ('s' if plural and count != 1 else '')
        require(summary[3] == tail, program + ': inconsistent failed-proof summary')
    return {'plan': plan, 'verdicts': verdicts, 'commands': commands,
            'nonproof': nonproof, 'declarations': declarations, 'vcs': vcs}
