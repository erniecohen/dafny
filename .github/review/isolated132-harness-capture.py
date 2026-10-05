#!/usr/bin/env python3
"""Record scoped #132 harness outputs, then check retained off/on oracles.

This diagnostic runner records phase failures and exits zero. Its JSON acceptance
field is authoritative for the focused recording; it is not a full review gate.
"""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import signal
import subprocess
import time
import xml.etree.ElementTree as ET

ROOT = Path.cwd()
RESULTS = ROOT / 'results'
RESULTS.mkdir(exist_ok=True)
SOURCE = ROOT / 'Source/IntegrationTests/TestFiles/LitTests/LitTest'
COPIED = ROOT / 'Source/IntegrationTests/bin/Release/net8.0/TestFiles/LitTests/LitTest'
CASES = sorted((SOURCE / 'git-issues').glob('git-issue-132-newtype-*.dfy'))
TRX_NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
report = {
    'diagnostic_only': True,
    'full_review_gate': False,
    'source_sha': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
    'scope': '17 source unit cases; 16 existing-language files with explicit off/on RUNs; unchanged bounded-polymorphism compiler case separately',
    'phases': {},
    'files': [],
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_report():
    (RESULTS / 'capture-results.json').write_text(json.dumps(report, indent=2) + '\n')


def trx_counts(path):
    root = ET.parse(path).getroot()
    node = root.find('.//t:ResultSummary/t:Counters', TRX_NS)
    if node is None:
        return {'error': 'TRX counters absent'}
    return {k: int(v) for k, v in node.attrib.items()}


def run_phase(name, command, update=False, expected_count=None, timeout=1800):
    output = RESULTS / (name + '.txt')
    phase_env = dict(os.environ)
    phase_env['DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE'] = 'true' if update else 'false'
    phase_env['DAFNY_INTEGRATION_TESTS_ONLY_COMPILERS'] = 'cs,js,py,go,java'
    # Every proof's local RUN is explicit. Do not inject another axiom selector.
    phase_env.pop('DAFNY_EXTRA_TEST_ARGUMENTS', None)
    started = time.monotonic()
    with output.open('w') as log:
        proc = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT,
                                env=phase_env, start_new_session=True)
        timed_out = False
        try:
            exit_code = proc.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            os.killpg(proc.pid, signal.SIGTERM)
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(proc.pid, signal.SIGKILL)
                proc.wait()
            exit_code = proc.returncode
    phase = {'command': command, 'exit': exit_code, 'update_expect': update,
             'elapsed_seconds': round(time.monotonic() - started, 3),
             'timed_out': timed_out, 'wall_clock_role': 'safety cap only, not proof-cost comparison',
             'environment': {'DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE': phase_env['DAFNY_INTEGRATION_TESTS_UPDATE_EXPECT_FILE'],
                             'DAFNY_EXTRA_TEST_ARGUMENTS': None}}
    trx = RESULTS / name / (name + '.trx')
    if trx.exists():
        phase['trx_counts'] = trx_counts(trx)
    counts = phase.get('trx_counts', {})
    phase['expected_test_count'] = expected_count
    phase['passed'] = (exit_code == 0 and not timed_out and
                       (expected_count is None or
                        (counts.get('total') == expected_count and counts.get('passed') == expected_count
                         and counts.get('failed', 0) == 0 and counts.get('notExecuted', 0) == 0)))
    report['phases'][name] = phase
    (RESULTS / (name + '-exit.txt')).write_text(str(exit_code) + '\n')
    write_report()
    return phase['passed']


def test_command(project, phase, filter_text, no_build=True):
    args = ['dotnet', 'test', project, '-c', 'Release']
    if no_build:
        args.append('--no-build')
    args += ['--logger', 'console;verbosity=normal', '--logger', 'trx;LogFileName=' + phase + '.trx',
             '--results-directory', str(RESULTS / phase), '--filter', filter_text]
    return args


try:
    assert len(CASES) == 16, 'The intended sixteen-case manifest changed'
    for path in CASES:
        lines = path.read_text().splitlines()
        assert len(lines) >= 4
        assert lines[0].startswith('// RUN: ') and '--additional-axioms=false' in lines[0]
        assert lines[1] == '// RUN: %diff "%s.expect" "%t"'
        assert lines[2].startswith('// RUN: ') and '--additional-axioms=true' in lines[2]
        assert lines[3] == '// RUN: %diff "%s.axioms-on.expect" "%t"'
        record = {'file': path.name, 'source_sha256': sha(path),
                  'body_sha256': hashlib.sha256(''.join(path.read_text().splitlines(keepends=True)[4:]).encode()).hexdigest(),
                  'off_existing_expect_sha256': sha(Path(str(path) + '.expect')),
                  'off_run': lines[0], 'on_run': lines[2],
                  'on_expect': path.name + '.axioms-on.expect'}
        report['files'].append(record)
    # Install/checksum phases happened before any build, so the harness copies
    # checked binaries rather than an empty solver directory.
    prerequisites = ['solver-download', 'solver-checksum', 'solver-unzip',
                     'pinned-solver-download', 'pinned-solver-checksum', 'pinned-solver-unzip',
                     'boogie-setup', 'build', 'npm', 'goimports']
    report['setup_exits'] = {}
    for prerequisite in prerequisites:
        path = RESULTS / (prerequisite + '-exit.txt')
        report['setup_exits'][prerequisite] = path.read_text().strip() if path.exists() else 'missing'
    assert all(value == '0' for value in report['setup_exits'].values()), 'Setup failed; proof phases are not evidence'
    assert COPIED.is_dir(), 'Harness copied input directory missing'
    harness_solver = re.search(r'DefaultZ3Version = "([^"]+)"',
                               (ROOT / 'Source/DafnyCore/DafnyOptions.cs').read_text()).group(1)
    solver_copy = COPIED.parents[2] / 'z3/bin' / ('z3-' + harness_solver)
    # parents[2] is net8.0 for net8.0/TestFiles/LitTests/LitTest.
    assert solver_copy.is_file(), 'Source-selected solver was not copied into the harness'
    source_solver = ROOT / 'Binaries/z3/bin' / ('z3-' + harness_solver)
    assert sha(solver_copy) == sha(source_solver), 'Harness solver copy differs from checked source solver'
    report['solver'] = {'harness_version': harness_solver, 'copied_sha256': sha(solver_copy),
                        'source_selected': True, 'pinned_regression_solver': '5.1.0'}
    compiler = ROOT / 'Binaries/net8.0/Dafny.dll'
    assert compiler.is_file(), 'Built compiler entry point is missing'
    report['compiler_version'] = subprocess.check_output(
        ['dotnet', str(compiler), '--version'], text=True).strip()
    report['compiler_components'] = [
        {'path': str(path.relative_to(ROOT)), 'sha256': sha(path)}
        for directory in (compiler.parent, COPIED.parents[2])
        for path in sorted(directory.glob('*.dll'))
    ]
    unit = 'source-unit'
    run_phase(unit, test_command('Source/DafnyCore.Test', unit,
                                'FullyQualifiedName~NewtypeReferenceCharacteristicTests', no_build=False),
              expected_count=17, timeout=900)
    capture = 'newtype-capture'
    captured_ok = run_phase(capture, test_command('Source/IntegrationTests', capture,
                                                'DisplayName~git-issue-132-newtype-'),
                            update=True, expected_count=16)
    expectations = RESULTS / 'expectations'
    expectations.mkdir(exist_ok=True)
    for record, path in zip(report['files'], CASES):
        copied = COPIED / 'git-issues' / path.name
        off = Path(str(copied) + '.expect')
        on = Path(str(copied) + '.axioms-on.expect')
        record['off_captured_expect_sha256'] = sha(off) if off.exists() else None
        record['off_existing_oracle_matches_capture'] = record['off_captured_expect_sha256'] == record['off_existing_expect_sha256']
        if off.exists():
            shutil.copy2(off, expectations / (path.name + '.captured-off.expect'))
        if on.exists():
            record['on_actual_expect_sha256'] = sha(on)
            shutil.copy2(on, expectations / on.name)
        else:
            record['on_actual_expect_sha256'] = None
        # The strict off check uses the ORIGINAL committed oracle, regardless of
        # what UPDATE just wrote. Only on is compared with the new captured file.
        shutil.copy2(Path(str(path) + '.expect'), off)
        assert sha(off) == record['off_existing_expect_sha256']
    strict = 'newtype-strict'
    if captured_ok and all(record['on_actual_expect_sha256'] for record in report['files']):
        run_phase(strict, test_command('Source/IntegrationTests', strict,
                                       'DisplayName~git-issue-132-newtype-'),
                  update=False, expected_count=16)
    else:
        report['phases'][strict] = {'passed': False, 'skipped': 'capture did not complete all sixteen cases'}
    # The unchanged compiler fixture is strict from its existing oracle; never
    # run UPDATE for it or combine it with the new verifier-output recording.
    bounded = 'bounded-polymorphism-strict'
    run_phase(bounded, test_command('Source/IntegrationTests', bounded,
                                    'DisplayName~dafny0/BoundedPolymorphismCompilation.dfy'),
              update=False, expected_count=1)
    report['acceptance'] = {
        'focused_recording_and_strict_consistency_passed':
            all(phase.get('passed', False) for phase in report['phases'].values()) and
            all(record['off_existing_oracle_matches_capture'] for record in report['files']),
        'off_oracles_preserved': True,
        'on_outputs_generated_from_actual_capture': captured_ok,
        'new_on_goldens_reviewed_or_committed': False,
        'expected_verdict_tables_changed': False,
        'full_review_gate_completed': False,
    }
except Exception as error:
    report['runner_error'] = type(error).__name__ + ': ' + str(error)
    report['acceptance'] = {'focused_recording_and_strict_consistency_passed': False,
                            'full_review_gate_completed': False}
finally:
    write_report()
    summary = os.environ.get('GITHUB_STEP_SUMMARY')
    if summary:
        with open(summary, 'a') as stream:
            stream.write('## Isolated #132 explicit-axiom diagnostic\n\n')
            stream.write('Focused recording and strict consistency: `' +
                         str(report['acceptance']['focused_recording_and_strict_consistency_passed']) + '`\n\n')
            stream.write('This diagnostic records all actual exit codes. Review new on-mode outputs before committing expectations; it is not a strict full review gate.\n')
# Expected negative proof exits are handled by the unchanged harness headers.
# Infrastructure, harness, count, timeout, or consistency failures stay visible
# in the artifact and acceptance field; diagnostic dispatch itself exits zero.
