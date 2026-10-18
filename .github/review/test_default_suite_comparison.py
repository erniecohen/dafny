"""Mocked inventory/retention controls only: never build or invoke a verifier."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import tarfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import default_suite_comparison as suite

spec = importlib.util.spec_from_file_location('comparison', Path(__file__).with_name('default-comparison.py'))
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class SuiteControls(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.plan = {'dafny0/good.dfy': [''], 'dafny0/bad-source.dfy': [''], 'dafny0/no-proofs.dfy': ['']}
        self.raw = [{'name': 'Good (correctness)', 'outcome': 'Correct', 'resourceCount': 30, 'vcResults': [
            {'vcNum': 1, 'randomSeed': 0, 'outcome': 'Valid', 'resourceCount': 10,
             'assertions': [{'filename': 'good.dfy', 'line': 1, 'col': 1, 'description': 'assertion'}]},
            {'vcNum': 2, 'randomSeed': 0, 'outcome': 'Valid', 'resourceCount': 20, 'assertions': []}]}]
        self.good = self.root / 'measurements/dafny0/good.dfy'
        self.inventory = {'plan': copy.deepcopy(self.plan), 'programs': {}}
        rows = []
        for program in self.plan:
            folder = self.root / 'measurements' / program
            folder.mkdir(parents=True)
            (folder / 'command.json').write_text(json.dumps(suite.expected_command(
                program, '', '/mock/dafny', '/mock/z3', folder / 'results.json')))
            if program == 'dafny0/good.dfy':
                (folder / 'results.json').write_text(json.dumps({'verificationResults': self.raw}))
                output = 'Dafny program verifier finished with 2 verified, 0 errors\n'
                verdict = ['0', '2 verified, 0 errors', '']
                self.inventory['programs'][program] = {'mode': 'proof', 'profile': suite.profile(self.raw)}
            else:
                if 'bad-source' in program:
                    output, verdict = 'bad-source.dfy(1,1): Error: expected source diagnostic\n', ['2', '', '1']
                    mode = 'missing'
                else:
                    output, verdict = 'Dafny program verifier finished with 0 verified, 0 errors\n', ['0', '0 verified, 0 errors', '']
                    (folder / 'results.json').write_text(json.dumps({'verificationResults': []}))
                    mode = 'empty'
                self.inventory['programs'][program] = {'mode': mode, 'verdict': verdict, 'diagnostics': suite.diagnostic_profile(output)}
            (folder / 'output.txt').write_text(output)
            rows.append('\t'.join([program] + verdict + ['0.1']))
        (self.root / 'plan.tsv').write_text(''.join(program + '\t' + options[0] + '\n' for program, options in self.plan.items()))
        self.tsv = self.root / 'verdicts.tsv'
        self.tsv.write_text('\n'.join(rows) + '\n')

    def vector(self):
        return suite.vector(self.root, self.inventory, Path('/mock/dafny'), Path('/mock/z3'), comparison.merge_outcomes)

    def write_raw(self):
        (self.good / 'results.json').write_text(json.dumps({'verificationResults': self.raw}))

    def test_complete_original_population_includes_explicit_nonproof_cases(self):
        result = self.vector()
        self.assertEqual({kind: len(value) for kind, value in result.items()},
                         {'plan': 3, 'verdicts': 3, 'commands': 3, 'nonproof': 2, 'declarations': 1, 'vcs': 2})

    def test_missing_program_is_not_accepted_by_intersection(self):
        self.tsv.write_text('\n'.join(self.tsv.read_text().splitlines()[:-1]) + '\n')
        with self.assertRaisesRegex(ValueError, 'program population'):
            self.vector()

    def test_duplicate_or_unexpected_program_is_rejected(self):
        original = self.tsv.read_text()
        for extra in [original.splitlines()[0], 'dafny0/unexpected.dfy\t0\t0 verified, 0 errors\t\t0.1']:
            with self.subTest(extra=extra):
                self.tsv.write_text(original + extra + '\n')
                with self.assertRaises(ValueError):
                    self.vector()

    def test_truncated_plan_is_rejected_even_when_both_passes_match(self):
        (self.root / 'plan.tsv').write_text('dafny0/good.dfy\t\n')
        with self.assertRaisesRegex(ValueError, 'plan differs'):
            self.vector()

    def test_missing_command_is_incomplete(self):
        (self.good / 'command.json').unlink()
        with self.assertRaisesRegex(ValueError, 'per-program command'):
            self.vector()

    def test_empty_positive_log_cannot_become_an_intentional_diagnostic(self):
        (self.good / 'results.json').write_text(json.dumps({'verificationResults': []}))
        (self.good / 'output.txt').write_text('good.dfy(1,1): Error: unexpected resolution failure\n')
        self.tsv.write_text(self.tsv.read_text().replace('0\t2 verified, 0 errors\t\t', '2\t\t1\t'))
        with self.assertRaisesRegex(ValueError, 'proof/diagnostic population'):
            self.vector()

    def test_truncated_scope_is_incomplete(self):
        self.raw[0]['name'] = 'Replacement (correctness)'
        self.write_raw()
        with self.assertRaisesRegex(ValueError, 'scope/VC/assertion inventory'):
            self.vector()

    def test_truncated_vc_is_incomplete_even_with_consistent_summary(self):
        self.raw[0]['vcResults'].pop()
        self.raw[0]['resourceCount'] = 10
        self.write_raw()
        (self.good / 'output.txt').write_text('Dafny program verifier finished with 1 verified, 0 errors\n')
        self.tsv.write_text(self.tsv.read_text().replace('2 verified', '1 verified'))
        with self.assertRaisesRegex(ValueError, 'scope/VC/assertion inventory'):
            self.vector()

    def test_duplicate_vc_is_rejected(self):
        self.raw[0]['vcResults'][1]['vcNum'] = 1
        self.write_raw()
        with self.assertRaisesRegex(ValueError, 'duplicate VC'):
            self.vector()

    def test_assertion_or_seed_substitution_is_rejected(self):
        original = copy.deepcopy(self.raw)
        for field, value in [('assertions', []), ('randomSeed', 1)]:
            self.raw = copy.deepcopy(original)
            self.raw[0]['vcResults'][0][field] = value
            self.write_raw()
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'scope/VC/assertion inventory'):
                self.vector()

    def test_wrong_process_exit_cannot_pass_with_valid_proofs(self):
        self.tsv.write_text(self.tsv.read_text().replace('good.dfy\t0\t', 'good.dfy\t2\t'))
        with self.assertRaisesRegex(ValueError, 'process exit disagrees'):
            self.vector()

    def test_verification_error_is_reported_as_difference_not_dropped(self):
        before = self.vector()
        self.raw[0]['outcome'] = 'Errors'
        self.raw[0]['vcResults'][1]['outcome'] = 'Invalid'
        self.write_raw()
        (self.good / 'output.txt').write_text('good.dfy(2,1): Error: assertion might not hold\nDafny program verifier finished with 1 verified, 1 error\n')
        self.tsv.write_text(self.tsv.read_text().replace('good.dfy\t0\t2 verified, 0 errors\t\t', 'good.dfy\t4\t1 verified, 1 error\t2\t'))
        after = self.vector()
        self.assertEqual(len(comparison.differences(before['verdicts'], after['verdicts'])), 1)
        self.assertEqual(len(comparison.differences(before['vcs'], after['vcs'])), 1)

    def test_cost_change_is_visible_without_rewriting_identity(self):
        before = self.vector()
        self.raw[0]['resourceCount'] += 1
        self.raw[0]['vcResults'][0]['resourceCount'] += 1
        self.write_raw()
        after = self.vector()
        self.assertFalse(comparison.differences(before['verdicts'], after['verdicts']))
        self.assertEqual(len(comparison.differences(before['vcs'], after['vcs'])), 1)

    def test_tsv_and_raw_output_must_agree(self):
        self.tsv.write_text(self.tsv.read_text().replace('2 verified', '1 verified'))
        with self.assertRaisesRegex(ValueError, 'TSV disagrees'):
            self.vector()

    def test_different_diagnostic_cannot_hide_behind_same_exit_and_error_line(self):
        folder = self.root / 'measurements/dafny0/bad-source.dfy'
        (folder / 'output.txt').write_text('bad-source.dfy(1,1): Error: different failure\n')
        with self.assertRaisesRegex(ValueError, 'intentional diagnostic'):
            self.vector()

    def test_proof_flags_are_not_normalized(self):
        file = self.good / 'command.json'
        command = json.loads(file.read_text())
        command[command.index('--resource-limit') + 1] = '1'
        file.write_text(json.dumps(command))
        with self.assertRaisesRegex(ValueError, 'actual command differs'):
            self.vector()

    def test_program_owned_options_keep_original_order_and_values(self):
        options = '--cores 2 --resource-limit=7 --boogie /randomSeed:3'
        command = suite.expected_command('a.dfy', options, 'compiler', 'solver', Path('log'))
        self.assertEqual(command.count('--cores'), 1)
        self.assertNotIn('16000000', command)
        self.assertEqual(command[-5:], ['--cores', '2', '--resource-limit=7', '--boogie', '/randomSeed:3'])

    def test_incomplete_pass_retains_raw_and_no_comparable_scope_report(self):
        def load(directory, name):
            (directory / 'raw-failure.txt').write_text('retained diagnostic')
            raise ValueError('mock missing intended proof')
        with patch.object(comparison.subprocess, 'run', return_value=SimpleNamespace(returncode=0)) as run:
            result = comparison.repeated_scope('mock-suite', self.root, lambda name, directory: ['mock'], load)
        self.assertEqual(run.call_count, 4)
        self.assertEqual(result['status'], 'incomplete')
        self.assertFalse((self.root / 'comparison-mock-suite.json').exists())
        self.assertFalse((self.root / 'summary.json').exists())
        for name in comparison.PASSES:
            self.assertTrue((self.root / 'mock-suite' / name / 'raw-failure.txt').exists())
            self.assertEqual(json.loads((self.root / 'mock-suite' / name / 'validation.json').read_text())['status'], 'incomplete')

    def test_nonzero_runner_exit_is_incomplete(self):
        with patch.object(comparison.subprocess, 'run', return_value=SimpleNamespace(returncode=1)):
            result = comparison.repeated_scope('failed-runner', self.root, lambda name, directory: ['mock'], lambda *_: self.fail('must not load failed runner'))
        self.assertEqual(result['status'], 'incomplete')
        self.assertFalse((self.root / 'comparison-failed-runner.json').exists())

    def test_packet_reuse_is_rejected_without_overwriting_old_evidence(self):
        summary = self.root / 'summary.json'
        summary.write_text('old immutable summary')
        with patch.object(comparison.sys, 'argv', ['probe', 'A', 'B', str(self.root), 'z3', 'z3h']), patch.object(comparison, 'main') as main:
            with self.assertRaisesRegex(ValueError, 'refusing to reuse'):
                comparison.run_probe()
        main.assert_not_called()
        self.assertEqual(summary.read_text(), 'old immutable summary')
        self.assertFalse((self.root / 'comparison-status.json').exists())
        self.assertFalse((self.root / 'raw-manifest.json').exists())

    def test_escaping_child_exception_is_fully_hashed_after_exit_and_stream_closure(self):
        packet = self.root / 'escaping-child'
        packet.mkdir()
        script = Path(__file__).with_name('default-comparison.py').resolve()
        child = """
import importlib.util
import io
from pathlib import Path
import sys
sys.path.insert(0, str(Path(sys.argv[1]).parent))
spec = importlib.util.spec_from_file_location('probe', sys.argv[1])
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
packet = Path(sys.argv[2])
def fail():
    print('stdout before the top-level exception', flush=True)
    print('stderr before the top-level exception', file=sys.stderr, flush=True)
    (packet / 'raw-control.txt').write_text('retained incomplete observation')
    raise ValueError('intentional escaping bookkeeping failure')
probe.main = fail
sys.argv = ['probe', 'A', 'B', str(packet), 'z3', 'z3h']
sys.exit(probe.run_probe())
"""
        with (packet / 'probe.stdout').open('w') as stdout, (packet / 'probe.stderr').open('w') as stderr:
            process = subprocess.run([sys.executable, '-c', child, str(script), str(packet)], stdout=stdout, stderr=stderr)
        self.assertNotEqual(process.returncode, 0)
        (packet / 'probe-exit.txt').write_text(str(process.returncode) + '\n')
        self.assertFalse((packet / 'raw-manifest.json').exists())
        self.assertIn('Traceback (most recent call last)', (packet / 'probe.stderr').read_text())
        self.assertIn('ValueError: intentional escaping bookkeeping failure', (packet / 'probe.stderr').read_text())
        self.assertEqual(json.loads((packet / 'comparison-status.json').read_text())['status'], 'incomplete')
        self.assertFalse((packet / 'summary.json').exists())
        finalized = subprocess.run([sys.executable, str(script), '--finalize-manifest', str(packet)], capture_output=True, text=True)
        self.assertEqual(finalized.returncode, 0, finalized.stderr)
        hashes = json.loads((packet / 'raw-manifest.json').read_text())
        files = {path.relative_to(packet).as_posix(): path for path in packet.rglob('*')
                 if path.is_file() and path.name != 'raw-manifest.json'}
        self.assertEqual(set(hashes), set(files))
        self.assertIn('probe.stderr', hashes)
        self.assertIn('probe-exit.txt', hashes)
        self.assertEqual((packet / 'probe-exit.txt').read_text(), str(process.returncode) + '\n')
        for name, path in files.items():
            with self.subTest(file=name):
                self.assertEqual(hashes[name], hashlib.sha256(path.read_bytes()).hexdigest())

    def test_finalization_cannot_overwrite_a_prior_manifest(self):
        for name in ['probe.stdout', 'probe.stderr', 'probe-exit.txt']:
            (self.root / name).write_text('0\n')
        manifest = self.root / 'raw-manifest.json'
        manifest.write_text('immutable earlier manifest')
        with self.assertRaisesRegex(ValueError, 'refusing to overwrite'):
            comparison.finalize_manifest(self.root)
        self.assertEqual(manifest.read_text(), 'immutable earlier manifest')

    def test_launch_failure_is_recorded_and_later_passes_still_attempted(self):
        with patch.object(comparison.subprocess, 'run', side_effect=OSError('mock launch failure')) as run:
            result = comparison.repeated_scope('launch-failed', self.root, lambda name, directory: ['mock'], lambda *_: self.fail('must not load'))
        self.assertEqual(run.call_count, 4)
        self.assertEqual(result['status'], 'incomplete')
        for name in comparison.PASSES:
            invocation = json.loads((self.root / 'launch-failed' / name / 'runner-invocation.json').read_text())
            self.assertEqual(invocation['exit'], 'launch-error')
            self.assertIn('mock launch failure', invocation['error'])

    def test_stdlib_native_commands_do_not_normalize_proof_flags_or_source_cwd(self):
        candidate = self.root / 'product-b'
        lib = candidate / 'Source/DafnyStandardLibraries'
        target = lib / 'src/Std/TargetSpecific'
        target.mkdir(parents=True)
        (target / 'helper-cs.dfy').write_text('mock source; never verified')
        measurements = self.root / 'native/measurements'
        measurements.mkdir(parents=True)
        for part in comparison.PARTS:
            cwd = lib if part == 'Std' else target
            args = ['src/Std/dfyconfig.toml'] if part == 'Std' else ['dfyconfig.toml']
            if part == 'TargetSpecific-cs':
                args += ['./helper-cs.dfy']
            command = ['/mock/dafny', 'verify'] + args + ['--solver-path', '/mock/z3', '--cores', '4',
                       '--verification-time-limit', '0', '--log-format', 'json;LogFileName=/mock/output.json']
            (measurements / (part + '.invocation.json')).write_text(json.dumps({'cwd': str(cwd), 'command': command}))
        result = comparison.std_commands(measurements.parent, candidate, Path('/mock/dafny'), Path('/mock/z3'))
        self.assertEqual(len(result), 7)
        file = measurements / 'Std.invocation.json'
        invocation = json.loads(file.read_text())
        original = copy.deepcopy(invocation)
        for field in ['limit', 'cwd']:
            invocation = copy.deepcopy(original)
            if field == 'limit':
                invocation['command'][invocation['command'].index('--verification-time-limit') + 1] = '1'
            else:
                invocation['cwd'] = '/unexpected/source'
            file.write_text(json.dumps(invocation))
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'actual command/cwd differs'):
                comparison.std_commands(measurements.parent, candidate, Path('/mock/dafny'), Path('/mock/z3'))

    def test_stdlib_repeat_requires_exact_vc_and_assertion_identity(self):
        vector = {'runs': {'Std': []}, 'declarations': {'Std Proof': []}, 'vcs': {'Std Proof\t1\t0': ['Valid', 1, ['assertion']]}}
        for replacement in [{'Std Proof\t2\t0': ['Valid', 1, ['assertion']]}, {'Std Proof\t1\t0': ['Valid', 1, []]}]:
            after = copy.deepcopy(vector)
            after['vcs'] = replacement
            with self.subTest(replacement=replacement), self.assertRaises(ValueError):
                comparison.std_repetition(vector, after)


class HarnessModeControls(unittest.TestCase):
    def identities(self, mode):
        return {'A': {'source': comparison.PRODUCT_A, 'lit_tree': 'baseline-tree'},
                'B': {'source': comparison.PRODUCT_A if mode == 'baseline-control' else comparison.PRODUCT_B,
                      'lit_tree': 'baseline-tree' if mode == 'baseline-control' else 'candidate-tree'},
                'harness': {'production_reference': comparison.PRODUCT_B, 'production_equals_reference': True,
                            'production_equals_product_B': mode == 'candidate'}}

    def inventory(self):
        return {'baseline_source': comparison.PRODUCT_A, 'candidate_source': comparison.PRODUCT_B,
                'baseline_lit_tree': 'baseline-tree', 'candidate_lit_tree': 'candidate-tree'}

    def test_both_modes_bind_exact_immutable_products_and_separate_harness(self):
        for mode in ['candidate', 'baseline-control']:
            identities = self.identities(mode)
            with self.subTest(mode=mode):
                comparison.validate_source_identities(mode, identities, self.inventory())
                self.assertEqual(comparison.product_revisions(mode)['A'], comparison.PRODUCT_A)
                self.assertEqual(identities['harness']['production_equals_product_B'], mode == 'candidate')
        self.assertEqual(comparison.product_revisions('baseline-control'), {'A': comparison.PRODUCT_A, 'B': comparison.PRODUCT_A})
        self.assertEqual(comparison.product_revisions('candidate')['B'], comparison.PRODUCT_B)

    def test_mode_cannot_substitute_another_product(self):
        for mode in ['candidate', 'baseline-control']:
            identities = self.identities(mode)
            identities['B']['source'] = comparison.PRODUCT_B if mode == 'baseline-control' else comparison.PRODUCT_A
            with self.subTest(mode=mode), self.assertRaisesRegex(ValueError, 'frozen mode-specific'):
                comparison.validate_source_identities(mode, identities, self.inventory())
        with self.assertRaisesRegex(ValueError, 'unknown comparison mode'):
            comparison.product_revisions('arbitrary-source')

    def test_baseline_control_retains_original_suite_population_authority(self):
        identities = self.identities('baseline-control')
        identities['B']['lit_tree'] = 'candidate-tree'
        with self.assertRaisesRegex(ValueError, 'authoritative suite'):
            comparison.validate_source_identities('baseline-control', identities, self.inventory())

    def test_control_harness_must_match_its_own_production_reference(self):
        identities = self.identities('baseline-control')
        identities['harness']['production_equals_reference'] = False
        identities['harness']['production_equals_product_B'] = True
        with self.assertRaisesRegex(ValueError, 'independent reference'):
            comparison.validate_source_identities('baseline-control', identities, self.inventory())


class RuntimeControls(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.sdks = '10.0.402 [' + str(self.root / 'sdk') + ']\n'
        self.runtimes = ''.join('Microsoft.NETCore.App ' + version + ' [' + str(self.root / 'shared/Microsoft.NETCore.App') + ']\n'
                                for version in ['8.0.32', '10.0.13'])

    def test_requires_only_sdk10_and_both_exact_runtimes_in_the_same_root(self):
        inventory = comparison.validate_dotnet_inventory(self.sdks, self.runtimes, self.root)
        self.assertEqual(inventory['sdk'], '10.0.402')
        self.assertEqual(set(inventory['core_runtimes']), {'8.0.32', '10.0.13'})
        for runtimes in [self.runtimes.splitlines()[1] + '\n', self.runtimes.replace('8.0.32', '8.0.31'),
                         self.runtimes.replace(str(self.root), '/another-root', 1)]:
            with self.subTest(runtimes=runtimes), self.assertRaisesRegex(ValueError, 'together in the isolated root'):
                comparison.validate_dotnet_inventory(self.sdks, runtimes, self.root)

    def test_sdk8_or_an_external_sdk_is_not_a_runtime_repair(self):
        for sdks in [self.sdks + '8.0.414 [' + str(self.root / 'sdk') + ']\n', self.sdks.replace(str(self.root), '/another-root')]:
            with self.subTest(sdks=sdks), self.assertRaisesRegex(ValueError, 'only SDK 10.0.402'):
                comparison.validate_dotnet_inventory(sdks, self.runtimes, self.root)

    def test_runtime_archive_extraction_preserves_the_sdk10_host(self):
        host = self.root / 'dotnet'
        host.write_bytes(b'existing host 10')
        sdk = self.root / 'sdk/10.0.402/compiler-marker'
        sdk.parent.mkdir(parents=True)
        sdk.write_bytes(b'existing compiler')
        archive = self.root / 'runtime8.tar.gz'
        with tarfile.open(archive, 'w:gz') as tar:
            for name, data in [('dotnet', b'old host'), ('./dotnet', b'old prefixed host'),
                               ('shared/Microsoft.NETCore.App/8.0.32/runtime-marker', b'runtime 8'),
                               ('host/fxr/8.0.32/fxr-marker', b'fxr 8')]:
                entry = tarfile.TarInfo(name)
                entry.size = len(data)
                tar.addfile(entry, io.BytesIO(data))
        result = subprocess.run(['tar', '-xzf', str(archive), '-C', str(self.root), '--exclude=dotnet', '--exclude=./dotnet'],
                                capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(host.read_bytes(), b'existing host 10')
        self.assertEqual(sdk.read_bytes(), b'existing compiler')
        self.assertEqual((self.root / 'shared/Microsoft.NETCore.App/8.0.32/runtime-marker').read_bytes(), b'runtime 8')
        self.assertEqual((self.root / 'host/fxr/8.0.32/fxr-marker').read_bytes(), b'fxr 8')


class WorkflowControls(unittest.TestCase):
    def test_comparison_only_skips_require_manual_scratch_dispatch(self):
        workflow = Path(__file__).parents[1] / 'workflows/review.yml'
        text = workflow.read_text()
        guards = {name: re.search(r'^  ' + name + r':\n(?:    name:[^\n]*\n)?    if: \$\{\{ (.*) \}\}', text, re.M)[1]
                  for name in ['build', 'regressions', 'resolver-tests', 'default-comparison']}
        for event in ['push', 'pull_request', 'workflow_dispatch']:
            for ref in ['refs/heads/dev', 'refs/heads/review/4.11.0', 'refs/tags/v1', 'refs/heads/scratch/test']:
                for flag in [False, True]:
                    wanted = event == 'workflow_dispatch' and flag and ref.startswith('refs/heads/scratch/')
                    for name, guard in guards.items():
                        expression = guard.replace('github.event_name', repr(event)).replace('github.ref', repr(ref)).replace('inputs.default_comparison', repr(flag))
                        expression = expression.replace('&&', ' and ').replace('!', ' not ')
                        actual = eval(expression.strip(), {'__builtins__': {}, 'startsWith': lambda a, b: a.startswith(b)})
                        with self.subTest(event=event, ref=ref, flag=flag, job=name):
                            self.assertEqual(actual, wanted if name == 'default-comparison' else not wanted)
        self.assertIn('ref: ' + comparison.PRODUCT_A, text)
        expected_selection = "${{ inputs.default_comparison_mode == 'baseline-control' && '" + comparison.PRODUCT_A + "' || '" + comparison.PRODUCT_B + "' }}"
        self.assertIn('ref: ' + expected_selection + '\n          path: comparison-candidate', text)
        for guard in guards.values():
            self.assertNotIn('default_comparison_mode', guard)

    def test_runtime_repair_and_both_builds_use_the_same_isolated_root(self):
        text = (Path(__file__).parents[1] / 'workflows/review.yml').read_text()
        section = text[text.index('  default-comparison:'):text.index('  resolver:')]
        self.assertIn('DOTNET_INSTALL_DIR: ${{ runner.temp }}/comparison-dotnet', section)
        self.assertIn('test "$DOTNET_ROOT" = "$RUNNER_TEMP/comparison-dotnet"', section)
        self.assertIn('dotnet-version: 10.0.402', section)
        self.assertIn('/Runtime/8.0.32/dotnet-runtime-8.0.32-linux-x64.tar.gz', section)
        self.assertIn('639a17d84e4d52fecd62c7d4e528e7bc0ae3c9860de2530cb41a2ee7196b69ea18271d8795849180742770e25b4f913df292a5a236fa197cf5d3cd0ec5ba665f', section)
        self.assertIn('-C "$DOTNET_ROOT" --exclude=dotnet --exclude=./dotnet', section)
        self.assertIn('--record-dotnet-environment "$PWD/default-comparison" "$DOTNET_ROOT"', section)
        self.assertLess(section.index('--record-dotnet-environment'), section.index('for variant in baseline candidate; do'))
        self.assertIn('test "$(sha256sum "$DOTNET_ROOT/dotnet")" = "$host_before"', section)


if __name__ == '__main__':
    unittest.main()
