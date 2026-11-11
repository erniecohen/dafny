"""M1 acceptance driver: keep resolution rejection distinct from verifier negatives."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


def main():
    executable = Path(sys.argv[1]).resolve()
    output = Path(sys.argv[2]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    sources = Path(__file__).resolve().parent
    launcher = ['dotnet', str(executable)] if executable.suffix == '.dll' else [str(executable)]
    solver = os.environ.get('DAFNY_OPERATORS_Z3')
    records, failures = [], []
    common = ['--use-basename-for-filename', '--show-snippets:false', '--cores', '1',
              '--resource-limit', '16000000', '--verification-time-limit', '0']
    if solver:
        common += ['--solver-path', solver]
    enabled = ['--user-defined-operators', '--type-system-refresh']

    def check(name, filename='positive', args=(), kind='success', diagnostic=None, command='verify',
              feature=True, runtime=None, source_text=None):
        folder = output / name
        folder.mkdir(exist_ok=True)
        source = sources / ('github-issue-194-' + filename + '.dfy')
        local = folder / 'test.dfy'
        if source_text is None:
            shutil.copyfile(source, local)
        else:
            local.write_text(source_text)
        options = common if command == 'verify' else ['--use-basename-for-filename'] + ([] if command == 'format' else ['--show-snippets:false'])
        cmd = launcher + command.split() + [str(local)] + options + (enabled if feature else []) + list(args)
        try:
            process = subprocess.run(cmd, cwd=folder, capture_output=True, text=True, timeout=180)
            text = process.stdout + process.stderr
            (folder / 'stdout.txt').write_text(process.stdout)
            (folder / 'stderr.txt').write_text(process.stderr)
            (folder / 'command.json').write_text(json.dumps(cmd, indent=2))
            record = {'name': name, 'exit': process.returncode, 'kind': kind}
            records.append(record)
            errors = []
            if kind == 'success' and process.returncode != 0:
                errors.append('expected success')
            elif kind == 'reject' and (process.returncode == 0 or 'Error' not in text):
                errors.append('expected front-end rejection')
            elif kind == 'verify-failure':
                if process.returncode != 4 or not re.search(r'verifier finished with \d+ verified, 1 error', text):
                    errors.append('expected exactly one verification error, exit 4')
                if 'FALSE_CONTROL' in local.read_text():
                    line = next(i for i, value in enumerate(local.read_text().splitlines(), 1) if 'FALSE_CONTROL' in value)
                    if not re.search(r'test.dfy\(' + str(line) + r',\d+\): Error: assertion', text):
                        errors.append('failure did not reach the false assertion')
            if re.search(r'out of resource|timed out|internal error|Unhandled exception', text, re.I):
                errors.append('infrastructure or resource failure')
            if diagnostic and not any(value in text for value in (diagnostic if isinstance(diagnostic, tuple) else (diagnostic,))):
                errors.append('missing diagnostic: ' + str(diagnostic))
            if runtime is not None and not text.rstrip().endswith(runtime):
                errors.append('wrong runtime result')
            if feature and list(folder.rglob('*.dtr')):
                errors.append('enabled compilation emitted a translation record')
            if errors:
                failures.append({'name': name, 'errors': errors, 'output': text})
            return text
        except subprocess.TimeoutExpired:
            failures.append({'name': name, 'errors': ['wall-clock safety cap']})
            return ''

    for filename in ['positive', 'named', 'revealed', 'intrinsic-refinement', 'module-attributes', 'module-attribute-named', 'opaque', 'by-method']:
        check(filename, filename)
    for filename in ['static-unary', 'trait-named-body', 'calc']:
        check(filename, filename)
    for filename in ['traversal', 'ghost-formal', 'chain', 'inference']:
        check(filename, filename)
        check(filename + '-named', filename + '-named')
    for filename, kind in [('reads', 'verify-failure'), ('null', 'verify-failure'), ('ghost', 'reject')]:
        check(filename, filename, kind=kind)
        check(filename + '-named', filename + '-named', kind=kind)
    check('unresolved', 'unresolved', kind='reject', diagnostic='operator receiver type is not determined')
    check('calc-negative', 'calc-negative', kind='reject')
    check('chain-false', 'chain-false', kind='verify-failure')
    check('format', command='format')
    check('format-roundtrip', source_text=(output / 'format' / 'test.dfy').read_text())
    check('false', 'false', kind='verify-failure', diagnostic='assertion might not hold')
    check('precondition', 'contracts', kind='verify-failure', diagnostic='precondition')
    check('termination', 'termination', kind='verify-failure', diagnostic='decreases')
    for filename, diagnostic in [
        ('refinement', 'operators require directly declared'),
        ('opaque-rank', ('operator selection cannot be transported', 'operators require directly declared')),
        ('hidden-member', 'operator selection cannot be transported'),
        ('abstract-import', 'feature-bearing abstract-signature'),
        ('new-refinement', 'feature-bearing refinement'),
        ('transitive-refinement', 'feature-bearing refinement'),
        ('module-attribute-user', None),
    ]:
        check(filename, filename, kind='reject', diagnostic=diagnostic,
              args=['--general-traits=datatype'] if filename == 'refinement' else [])
    exclusions = ['equality', 'inequality', 'override', 'malformed', 'duplicate-attribute', 'duplicate-slot',
                  'arity', 'result', 'method', 'trait', 'newtype', 'codatatype', 'module', 'member-generic',
                  'default', 'nameonly', 'static-owner', 'right-only', 'wrong-result', 'wrong-argument',
                  'compound', 'escape'] + ['bitwise-' + str(i) for i in range(6)]
    for filename in exclusions:
        check(filename, filename, kind='reject')
    check('requires-refresh', feature=False, args=['--user-defined-operators'], kind='reject',
          diagnostic='currently requires --type-system-refresh')
    check('trusted-input', args=['--library', str(sources / 'github-issue-194-named.dfy')], kind='reject',
          diagnostic='does not support library/precompiled')
    check('standard-library', args=['--standard-libraries'], kind='reject', diagnostic='does not support')
    check('record-input', command='build', args=['--translation-record', str(output / 'missing.dtr')], kind='reject', diagnostic='translation record')
    check('record-output', command='translate py', args=['--translation-record-output', str(output / 'requested.dtr')], kind='reject', diagnostic='translation record')
    check('library-output', command='build', args=['--target', 'lib'], kind='reject', diagnostic='.doo output')
    check('compiled-rank', 'compiled-rank', command='build', args=['--target', 'py', '--no-verify'], kind='reject')
    for suffix in ['doo', 'dbin']:
        artifact = output / ('untrusted.' + suffix)
        artifact.write_bytes(b'not a trusted operator artifact')
        check('precompiled-' + suffix, args=[str(artifact)], kind='reject', diagnostic='does not support')
    for target in ['py', 'cs']:
        check('run-' + target, command='run', args=['--target', target, '--no-verify'], runtime='ok')
        check('run-named-' + target, 'named', command='run', args=['--target', target, '--no-verify'], runtime='ok')
        check('by-method-' + target, 'by-method', command='run', args=['--target', target, '--no-verify'], runtime='3')
    # Same source path and following token positions for notation versus ordinary expansion.
    parity = output / 'expansion-parity'
    parity.mkdir(exist_ok=True)
    local = parity / 'test.dfy'
    template = (sources / 'github-issue-194-expansion.dfy').read_text()
    snapshots = {}
    for mode, body in [('named', 'x.plus(y)'), ('notation', 'x + y    ')]:
        local.write_text(template.replace('BODY', body))
        observation = parity / 'observation'
        observation.mkdir(exist_ok=True)
        cmd = launcher + ['verify', str(local)] + common + enabled + [
            '--boogie', '/normalizeDeclarationOrder:0', '--boogie', '/restartProver',
            '--bprint', str(observation / 'program.bpl'),
            '--log-format', 'json;LogFileName=' + str(observation / 'verification.json')]
        process = subprocess.run(cmd, cwd=parity, capture_output=True, text=True, timeout=180)
        (observation / 'stdout.txt').write_text(process.stdout)
        (observation / 'stderr.txt').write_text(process.stderr)
        (observation / 'command.json').write_text(json.dumps(cmd, indent=2))
        log = observation / 'verification.json'
        bpl = observation / 'program.bpl'
        resources = None
        if log.exists():
            data = json.loads(log.read_text())
            resources = sorted([[d['name'], v['vcNum'], v['outcome'], v['resourceCount']]
                for d in data.get('verificationResults', []) for v in d.get('vcResults', [])])
        snapshots[mode] = {'exit': process.returncode, 'resources': resources,
                           'boogie': bpl.read_bytes() if bpl.exists() else None}
        shutil.move(str(observation), str(parity / mode))
    records.append({'name': 'expansion-parity', 'resources': snapshots['notation']['resources']})
    if snapshots['named'] != snapshots['notation'] or snapshots['notation']['exit'] != 0 or not snapshots['notation']['resources'] or not snapshots['notation']['boogie']:
        failures.append({'name': 'expansion-parity', 'errors': ['ordinary-call Boogie or resource parity failed']})
    # CLI false must override a project setting, including inert malformed attributes.
    project = output / 'project'
    project.mkdir(exist_ok=True)
    (project / 'test.dfy').write_text('datatype D = D { function {:operator 1} f(): int { 0 } }\n')
    (project / 'dfyconfig.toml').write_text('includes = ["test.dfy"]\n[options]\nuser-defined-operators = true\ntype-system-refresh = true\n')
    for mode, expected in [('true', False), ('false', True)]:
        cmd = launcher + ['resolve', str(project / 'dfyconfig.toml'), '--user-defined-operators:' + mode]
        result = subprocess.run(cmd, cwd=project, text=True, capture_output=True, timeout=60)
        records.append({'name': 'project-' + mode, 'exit': result.returncode})
        if (result.returncode == 0) != expected:
            failures.append({'name': 'project-' + mode, 'output': result.stdout + result.stderr})
    (output / 'results.json').write_text(json.dumps({'checks': records, 'failures': failures}, indent=2))
    if failures:
        for failure in failures:
            print(json.dumps(failure, indent=2))
        raise SystemExit(1)
    print('user-defined operators M1: all checks passed')


if __name__ == '__main__':
    main()
