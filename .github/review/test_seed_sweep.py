"""Distribution and runner controls; no Dafny build or solver is required."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('seed_sweep', Path(__file__).with_name('seed-sweep.py'))
sweep = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sweep)


def sample(ru, success=True):
    return {'ru': ru, 'success': success, 'outcomes': ['Valid' if success else 'OutOfResource'], 'batches': 1}


def runs(a, b):
    return [{'lane': lane, 'seed': i + 1, 'members': {'M': s}}
            for lane, values in [('base', a), ('candidate', b)] for i, s in enumerate(values)]


class DistributionTests(unittest.TestCase):
    def compare(self, a, b):
        return sweep.compare(runs(a, b), list(range(1, len(a) + 1)), [5, 20], .05)

    def test_quantiles_and_strict_thresholds_exclude_censored_costs(self):
        d = sweep.distribution([sample(v) for v in [0, 5, 10, 20]] + [sample(100, False)], [5, 20])
        self.assertEqual({'q25': 3.75, 'median': 7.5, 'q75': 12.5, 'q90': 17.0}, d['successful_ru'])
        self.assertEqual({'5': 2, '20': 0}, d['successful_over'])
        self.assertEqual(1, d['exhausted'])
        self.assertIsNone(sweep.quantile([], .5))

    def test_ties_identical_distributions_and_reordered_seeds(self):
        a = [sample(v) for v in range(48)]
        row = self.compare(a, list(reversed(a)))
        self.assertEqual('indistinguishable', row['verdict'])
        test = row['members']['M']['rank_test']
        self.assertEqual(.5, test['probability_candidate_worse'])
        self.assertEqual(1, test['p'])
        self.assertEqual(1, self.compare([sample(1)] * 48, [sample(1)] * 48)['members']['M']['rank_test']['p'])

    def test_better_worse_and_effect_orientation(self):
        for a, b, verdict, probability in [(100, 1, 'better', 0), (1, 100, 'worse', 1)]:
            with self.subTest(verdict=verdict):
                row = self.compare([sample(a)] * 48, [sample(b)] * 48)
                self.assertEqual(verdict, row['verdict'])
                test = row['members']['M']['rank_test']
                self.assertEqual(probability, test['probability_candidate_worse'])
                self.assertLess(test['p'], 1e-15)

    def test_known_tie_corrected_normal_rank_statistic(self):
        # Candidate U=7.5, tie groups 1,2,1,1,1; variance=5.1; correction=.5.
        test = sweep.rank_test([sample(v) for v in [1, 2, 3]], [sample(v) for v in [2, 4, 5]])
        self.assertEqual(7.5, test['u_candidate'])
        self.assertAlmostEqual(.26828588367711736, test['p'])

    def test_failure_rank_is_worse_than_every_success_even_when_failure_ru_is_lower(self):
        row = self.compare([sample(1000)] * 48, [sample(1, False)] * 48)
        self.assertEqual('worse', row['verdict'])
        self.assertIsNone(row['members']['M']['candidate']['successful_ru']['median'])
        self.assertEqual('indistinguishable', self.compare([sample(2, False)] * 48, [sample(1, False)] * 48)['verdict'])

    def test_small_sweeps_never_claim_direction(self):
        row = self.compare([sample(100)] * 47, [sample(1)] * 47)
        self.assertTrue(row['exploratory'])
        self.assertEqual('indistinguishable', row['verdict'])

    def test_no_run_or_member_intersection_and_no_duplicates(self):
        original = runs([sample(1)] * 48, [sample(2)] * 48)
        for change in ['missing-run', 'duplicate-run', 'missing-member', 'empty', 'extra-member']:
            with self.subTest(change=change):
                data = copy.deepcopy(original)
                if change == 'missing-run': data.pop()
                if change == 'duplicate-run': data[-1] = data[0]
                if change == 'missing-member': data[-1]['members'] = {}
                if change == 'empty':
                    for r in data: r['members'] = {}
                if change == 'extra-member': data[-1]['members']['Extra'] = sample(1)
                with self.assertRaises(ValueError):
                    sweep.compare(data, list(range(1, 49)), [], .05)

    def test_holm_and_regression_priority(self):
        data = runs([sample(100)] * 48, [sample(1)] * 48)
        for r in data:
            r['members']['N'] = sample(1 if r['lane'] == 'base' else 100)
        with patch.object(sweep, 'rank_test', side_effect=[
                {'p': .03, 'probability_candidate_worse': 0},
                {'p': .04, 'probability_candidate_worse': 1}]):
            result = sweep.compare(data, list(range(1, 49)), [], .05)
        self.assertEqual(.06, result['members']['M']['rank_test']['p_holm'])
        self.assertEqual('indistinguishable', result['verdict'])
        self.assertEqual('worse', sweep.compare(data, list(range(1, 49)), [], .05)['verdict'])


class LogTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.path = Path(tmp.name) / 'results.json'
        self.raw = {'verificationResults': [{'name': 'M (correctness)', 'outcome': 'Correct',
                    'vcResults': [{'vcNum': 1, 'outcome': 'Valid', 'resourceCount': 12},
                                  {'vcNum': 2, 'outcome': 'Valid', 'resourceCount': 8}]}]}

    def read(self, code=0):
        self.path.write_text(json.dumps(self.raw))
        return sweep.read_members(self.path, code)

    def test_all_batches_count_towards_a_member(self):
        self.assertEqual(sample(20) | {'outcomes': ['Valid', 'Valid'], 'batches': 2}, self.read()['M (correctness)'])

    def test_resource_exhaustion_is_valid_data_at_exit_four(self):
        m = self.raw['verificationResults'][0]
        m['outcome'] = 'OutOfResource'
        m['vcResults'][1]['outcome'] = 'OutOfResource'
        self.assertFalse(self.read(4)['M (correctness)']['success'])
        with self.assertRaises(ValueError): self.read(0)

    def test_parse_exit_warning_crash_and_missing_results_do_not_become_success(self):
        for code in [1, 2, 3, 4, -9]:
            with self.subTest(code=code), self.assertRaises(ValueError): self.read(code)
        self.raw['verificationResults'] = []
        with self.assertRaises(ValueError): self.read()

    def test_duplicate_members_batches_and_empty_batches_are_rejected(self):
        original = copy.deepcopy(self.raw)
        for change in ['member', 'vc', 'empty']:
            self.raw = copy.deepcopy(original)
            m = self.raw['verificationResults'][0]
            if change == 'member': self.raw['verificationResults'].append(m)
            if change == 'vc': m['vcResults'].append(m['vcResults'][0])
            if change == 'empty': m['vcResults'] = []
            with self.subTest(change=change), self.assertRaises(ValueError): self.read()

    def test_unknown_failure_bad_ru_cached_and_inconsistent_outcomes_rejected(self):
        original = copy.deepcopy(self.raw)
        for outcome in ['Invalid', 'TimeOut', 'OutOfMemory', 'Undetermined', 'future']:
            self.raw = copy.deepcopy(original)
            self.raw['verificationResults'][0]['vcResults'][0]['outcome'] = outcome
            with self.subTest(outcome=outcome), self.assertRaises(ValueError): self.read(4)
        for ru in [-1, 1.5, '1', True]:
            self.raw = copy.deepcopy(original)
            self.raw['verificationResults'][0]['vcResults'][0]['resourceCount'] = ru
            with self.subTest(ru=ru), self.assertRaises(ValueError): self.read()
        for where in ['member', 'batch']:
            self.raw = copy.deepcopy(original)
            member = self.raw['verificationResults'][0]
            (member if where == 'member' else member['vcResults'][0])['timingReplayed'] = True
            with self.assertRaises(ValueError): self.read()
        self.raw = copy.deepcopy(original)
        self.raw['verificationResults'][0]['outcome'] = 'Errors'
        with self.assertRaises(ValueError): self.read()


class RunnerTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name).resolve()
        (self.root / 'proof.dfy').write_text('lemma M() {}')
        (self.root / 'dfyconfig.toml').write_text('includes = ["*.dfy"]')
        self.binary = self.root / 'fake-dafny'
        self.binary.write_text('''#!/usr/bin/env python3
import json, pathlib, sys
if '--version' in sys.argv:
    print('fixture 1'); sys.exit(0)
path = next(a.split('LogFileName=')[1] for a in sys.argv if 'LogFileName=' in a)
data = {'verificationResults': [{'name': 'M', 'outcome': 'Correct', 'vcResults': [
    {'vcNum': 1, 'outcome': 'Valid', 'resourceCount': 10}]}]}
pathlib.Path(path).write_text(json.dumps(data))
''')
        self.binary.chmod(0o755)

    def args(self):
        return ['--base', str(self.binary), '--candidate', str(self.binary), '--solver', str(self.binary),
                '--base-cwd', str(self.root), '--candidate-cwd', str(self.root), '--target', 'dfyconfig.toml',
                '--target', 'proof.dfy', '--filter-symbol', 'M', '--seeds', '2', '--resource-limit', '1000',
                '--out', str(self.root / 'out')]

    def run_main(self, args):
        with patch('sys.stdout', new_callable=io.StringIO), patch('sys.stderr', new_callable=io.StringIO):
            return sweep.main(args)

    def test_complete_cli_preserves_raw_logs_manifest_and_alternates_lanes(self):
        order = []
        execute = sweep.execute
        def record(command, cwd, directory, timeout):
            order.append((directory.parent.name, directory.name))
            return execute(command, cwd, directory, timeout)
        with patch.object(sweep, 'execute', side_effect=record):
            self.assertEqual(0, self.run_main(self.args()))
        self.assertEqual([('base', '1'), ('candidate', '1'), ('candidate', '2'), ('base', '2')], order)
        out = self.root / 'out'
        manifest = json.loads((out / 'manifest.json').read_text())
        self.assertIn('fake-dafny', manifest['lanes']['base']['binary']['files_sha256'])
        command = json.loads((out / 'base/1/command.json').read_text())['command']
        for token in ['dfyconfig.toml', 'proof.dfy', '--filter-symbol', '/randomSeed:1', '--cores']:
            self.assertIn(token, command)
        self.assertTrue((out / 'summary.md').is_file())
        summary = json.loads((out / 'summary.json').read_text())
        self.assertEqual(4, len(summary['runs']))
        self.assertEqual('indistinguishable', summary['verdict'])
        before = (out / 'summary.json').read_bytes()
        self.assertEqual(2, self.run_main(self.args()))
        self.assertEqual(before, (out / 'summary.json').read_bytes())

    def test_cli_failure_stops_and_reports_invalid_without_partial_verdict(self):
        with patch.object(sweep, 'execute', return_value=2):
            self.assertEqual(2, self.run_main(self.args()))
        result = json.loads((self.root / 'out/summary.json').read_text())
        self.assertEqual('invalid', result['verdict'])
        self.assertEqual([], result['runs'])

    def test_source_change_invalidates_comparison(self):
        execute = sweep.execute
        def changed(command, cwd, directory, timeout):
            (self.root / 'proof.dfy').write_text('lemma M() { assert false; }')
            return execute(command, cwd, directory, timeout)
        with patch.object(sweep, 'execute', side_effect=changed):
            self.assertEqual(2, self.run_main(self.args()))
        self.assertIn('changed during', json.loads((self.root / 'out/summary.json').read_text())['error'])

    def test_timeout_reaps_owned_process(self):
        self.binary.write_text('#!/usr/bin/env python3\nimport time\ntime.sleep(20)\n')
        with self.assertRaises(subprocess.TimeoutExpired):
            sweep.execute([str(self.binary)], self.root, self.root / 'timeout', .05)
        self.assertTrue(json.loads((self.root / 'timeout/exit.json').read_text())['interrupted'])

    def test_control_overrides_are_rejected_in_combined_boogie_flags(self):
        for option in ['--cores=8', '/randomSeed:0', '/foo /randomSeed:9', '--log-format=csv',
                       '--verification-cache=x', '/rlimit:1', '--solver-option=smt.random_seed=1',
                       '--filter-symbol=Different', '/proverOpt:PROVER_PATH:other', '/noVerify']:
            with self.subTest(option=option), self.assertRaises(ValueError):
                sweep.validate_options([option])
        sweep.validate_options(['--boogie', '/normalizeDeclarationOrder:0 /structuralLiterals', '--allow-warnings'])

    def test_dll_launcher_and_lane_options(self):
        dll = self.root / 'driver.dll'; dll.touch()
        command, path = sweep.launcher(str(dll), 'custom-dotnet')
        self.assertEqual(['custom-dotnet', str(dll)], command)
        args = sweep.parser().parse_args(self.args() + ['--arg=--allow-warnings', '--candidate-arg=--legacy-noncanonical-map-payloads'])
        c = sweep.command_for(args, command, 'candidate', 2, self.root)
        b = sweep.command_for(args, command, 'base', 2, self.root)
        self.assertIn('--legacy-noncanonical-map-payloads', c)
        self.assertNotIn('--legacy-noncanonical-map-payloads', b)
        self.assertIn('--allow-warnings', b)

    def test_standalone_project_hashes_glob_inputs_and_relative_includes(self):
        (self.root / 'child.dfy').write_text('lemma Child() {}')
        (self.root / 'proof.dfy').write_text('include "child.dfy"\nlemma M() {}')
        first = sweep.source_identity(self.root, ['dfyconfig.toml'])
        self.assertIn(str(self.root / 'child.dfy'), first)
        (self.root / 'child.dfy').write_text('lemma Child() { assert false; }')
        self.assertNotEqual(first, sweep.source_identity(self.root, ['dfyconfig.toml']))


if __name__ == '__main__':
    unittest.main()
