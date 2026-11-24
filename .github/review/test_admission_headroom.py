"""Headroom, completed-peak estimation and outstanding growth with synthetic inputs."""
from io import StringIO
import json
import math
import os
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

from test_admission_budget import AdmissionFixture, POLICY, admission


class PolicyTests(unittest.TestCase):
    def test_requires_every_policy_field(self):
        for key in POLICY:
            policy = dict(POLICY)
            del policy[key]
            with self.subTest(key=key), self.assertRaises(ValueError):
                admission.validate_policy(policy)
        for policy in (None, [], ''):
            with self.assertRaises(ValueError):
                admission.validate_policy(policy)

    def test_positive_finite_numbers_and_integer_samples(self):
        for key in POLICY:
            for bad in (None, True, False, 0, -1, float('nan'), float('inf'), '2', [], {}):
                with self.subTest(key=key, bad=bad), self.assertRaises(ValueError):
                    admission.validate_policy(dict(POLICY, **{key: bad}))
        with self.assertRaises(ValueError):
            admission.validate_policy(dict(POLICY, peak_min_samples=1.5))
        for bad in ([0], [True], [float('nan')], [float('inf')], ['2'], [1, -1]):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                admission.validate_policy(dict(POLICY, memory_backoff_s=bad))
        self.assertEqual(admission.validate_policy(POLICY), POLICY)

    def test_backoff_caps_and_slot_or_mutex_wait_resets(self):
        backoff = admission.Backoff(POLICY)
        self.assertEqual([backoff.delay('memory: deficit') for _ in range(5)], [.05, .1, .2, .2, .2])
        for reason in ('slots', 'mutex'):
            self.assertEqual(backoff.delay(reason), .05)
            self.assertEqual(backoff.delay('budget'), .05)
            self.assertEqual(backoff.delay('unaccounted: lock'), .05)

    def test_missing_or_invalid_policy_fails_cli_without_ready(self):
        import sys
        import tempfile
        from test_admission_budget import ACTION
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            base = [sys.executable, str(ACTION / 'admit.py'), str(root / 'lock'), str(root / 'ready'),
                    str(os.getpid()), admission.identity(os.getpid())]
            for args in ([], ['1', '{}'], ['1', json.dumps(POLICY), 'ci:test', 'example/repo', '0']):
                result = subprocess.run(base + args, capture_output=True, text=True, timeout=5)
                self.assertEqual(result.returncode, 2)
                self.assertIn('Admission failed:', result.stderr)
                self.assertFalse((root / 'ready').exists())
                self.assertFalse((root / 'lock.0').exists())

    def test_invalid_policy_and_caller_fail_before_owner_check(self):
        for policy, peak in (({}, None), (POLICY, 0), (POLICY, True)):
            with self.assertRaises(ValueError):
                admission.hold('unused', 'unused', 0, '', 1, policy, caller=peak)


class HeadroomTests(AdmissionFixture):
    def test_exact_reserve_boundary_and_one_mib_below(self):
        with patch.object(admission, 'physical_mb', return_value=1100):
            stream = self.take()
            admission.release(stream)
        with patch.object(admission, 'physical_mb', return_value=1099):
            self.denied('memory')
            self.assert_free('active.lock.0')
            self.assertFalse((self.locks / 'active.lock.0.json').exists())

    def test_normal_and_warn_admit_critical_and_unknown_refuse(self):
        for pressure in (1, 2):
            with patch.object(admission, 'pressure_level', return_value=pressure):
                stream = self.take()
                admission.release(stream)
        for pressure in (4, 0, 3, None):
            with patch.object(admission, 'pressure_level', return_value=pressure):
                self.denied('memory')

    def test_each_missing_probe_refuses_and_percentage_is_scaled(self):
        for name in ('free_percent', 'pressure_level', 'physical_mb'):
            with patch.object(admission, name, return_value=None):
                self.denied('memory')
        with patch.object(admission, 'physical_mb', return_value=2200), patch.object(admission, 'free_percent', return_value=50):
            stream = self.take()
            admission.release(stream)
        with patch.object(admission, 'physical_mb', return_value=2198), patch.object(admission, 'free_percent', return_value=50):
            self.denied('memory')

    def test_two_low_rss_holders_refuse_third_despite_incoming_headroom(self):
        self.write_config(50000)
        for name in ('active.lock.0', 'active.lock.1'):
            self.occupy(name, 1000, expected_mb=1200)
        with patch.object(admission, 'physical_mb', return_value=3000), patch.object(admission, 'process_tree_mb', return_value=100):
            # 3000 - 1000 >= 100, but 3000 - 1000 - 2*(1200-100) < 100.
            self.denied('memory')
            self.assert_free('active.lock.2')
            self.assertFalse((self.locks / 'active.lock.2.json').exists())
        with patch.object(admission, 'physical_mb', return_value=3300), patch.object(admission, 'process_tree_mb', return_value=100):
            self.take()  # Equality, including outstanding growth, admits.

    def test_growth_uses_expected_peak_unknown_tree_and_nonnegative_difference(self):
        self.occupy('agent-reserve.lock', 2000, expected_mb=1000)
        for rss, growth in ((None, 1000), (100, 900), (1000, 0), (2000, 0)):
            with patch.object(admission, 'process_tree_mb', return_value=rss) as tree:
                total, outstanding, unknown = admission.pool_usage(self.locks, 'unused', 7)
                self.assertEqual((total, outstanding, unknown), (2000, growth, []))
                tree.assert_called_once_with(os.getpid(), 7)

    def test_legacy_sidecar_uses_full_declaration_on_unreadable_tree(self):
        self.occupy('active.lock.90', 2000)
        with patch.object(admission, 'process_tree_mb', return_value=None):
            self.assertEqual(admission.pool_usage(self.locks, 'unused', 2), (2000, 2000, []))

    def test_invalid_expected_metadata_refuses(self):
        for peak in (0, True, '1', float('nan')):
            stream = self.occupy('active.lock.90', expected_mb=peak)
            self.denied('unaccounted')
            admission.release(stream)

    def test_publishes_selected_peak_and_uses_runner_project(self):
        with patch.dict(os.environ, ADMISSION_PROJECT='registered-history-name'):
            with patch.object(admission, 'expected_peak', return_value={'mb': 2048.5, 'source': 'caller', 'n': 0}) as select:
                stream = self.take(caller=2048.5)
                self.assertEqual(select.call_args.args[0], 'registered-history-name')
        data = json.loads(admission.sidecar_path(stream).read_text())
        self.assertEqual(data['expected_mb'], 2048.5)
        self.assertEqual(data['declared_mb'], 3400)

    def test_slot_wait_precedes_memory_and_never_takes_reserve(self):
        self.write_config(slots=1)
        self.occupy('active.lock.0')
        with patch.object(admission, 'memory_healthy', side_effect=AssertionError('probed before slot')):
            self.denied('slots')
        self.assert_free('agent-reserve.lock')

    def test_scanning_reserve_never_acquires_it(self):
        (self.locks / 'agent-reserve.lock').touch()
        original = admission.try_lock
        calls = []
        def tracked(path):
            calls.append(Path(path).name)
            return original(path)
        with patch.object(admission, 'try_lock', side_effect=tracked):
            stream = self.take()
        self.assertNotIn('agent-reserve.lock', calls)
        admission.release(stream)

    def test_release_gap_uses_slot_poll_instead_of_memory_backoff(self):
        stream = self.take()
        backoff = admission.Backoff(dict(POLICY, slot_poll_s=.03))
        backoff.delay('memory: deficit')
        backoff.delay('memory: deficit')
        original = Path.unlink
        observed = []
        def unlink(path, *args, **kwargs):
            original(path, *args, **kwargs)
            if path == admission.sidecar_path(stream):
                # Pause release after unlink, while the holder is still locked.
                self.assertIsNone(admission.try_lock(stream.name))
                other, reason = self.attempt()
                self.assertIsNone(other)
                observed.append(reason)
                self.assertEqual(backoff.delay(reason), .03)
        with patch.object(Path, 'unlink', unlink):
            admission.release(stream)
        self.assertEqual(len(observed), 1)
        self.assertEqual(backoff.delay('memory: deficit'), .05)

    def test_growth_charges_the_same_peak_used_at_admission(self):
        with patch.object(admission, 'physical_mb', return_value=1100):
            stream = self.take(caller=1000)
        data = json.loads(admission.sidecar_path(stream).read_text())
        self.assertEqual(data['declared_mb'], 3400)
        self.assertEqual(data['expected_mb'], 1000)
        with patch.object(admission, 'process_tree_mb', return_value=0):
            total, outstanding, unknown = admission.pool_usage(self.locks, 'unused', 2)
        self.assertEqual((total, outstanding, unknown), (3400, 1000, []))

    def test_repeated_reason_is_logged_again_after_it_changes(self):
        output = StringIO()
        reasons = ['memory: available_mb=1 expected_mb=2 reserve_mb=3 pressure=4', 'slots',
                   'memory: available_mb=1 expected_mb=2 reserve_mb=3 pressure=4']
        with patch.object(admission, 'identity', side_effect=['owner'] * 6 + ['gone']), patch.object(admission, 'try_admit', side_effect=[(None, r) for r in reasons]), patch.object(admission.time, 'monotonic', side_effect=[0, 1] * 3), patch('sys.stdout', output):
            admission.hold('unused', 'unused', 1, 'owner', 1, POLICY)
        self.assertEqual(output.getvalue().count('Waiting for admission: memory:'), 2)


class PeakTests(AdmissionFixture):
    def setUp(self):
        super().setUp()
        self.history = self.root / 'custom-history.jsonl'
        os.environ['ADMISSION_RSS_HISTORY'] = str(self.history)
        self.now = 200 * 86400

    def row(self, key='one', peak=100, **extra):
        return dict(dict(event='end', key=key, t=self.now, project='project', kind='ci:Workflow',
                         samples=1, peak_mb=peak), **extra)

    def write_rows(self, rows, file=None):
        (file or self.history).write_text('\n'.join(json.dumps(row) for row in rows) + '\n')

    def select(self, project='project', kind='ci:Workflow', caller=None):
        return admission.expected_peak(project, kind, POLICY, caller=caller, now=self.now)

    def test_caller_overrides_history_and_rejects_invalid_peak(self):
        self.write_rows([self.row(str(i)) for i in range(3)])
        self.assertEqual(self.select(caller=42.5), dict(mb=42.5, source='caller', n=0))
        for peak in (True, 0, -1, float('inf'), float('nan'), '3'):
            with self.assertRaises(ValueError):
                self.select(caller=peak)

    def test_project_kind_then_kind_across_projects(self):
        self.write_rows([self.row(str(i), 100+i) for i in range(3)] +
                        [self.row('other'+str(i), 1000, project='other') for i in range(3)])
        self.assertEqual(self.select(), dict(mb=102, source='project/kind p95', n=3))
        self.assertEqual(self.select(project='missing'), dict(mb=1000, source='kind p95', n=6))

    def test_sparse_group_never_mixes_kinds_and_missing_history_uses_default(self):
        self.write_rows([self.row('heavy', 500)] +
                        [self.row(str(i), 1, kind='ci:Light') for i in range(20)])
        self.assertEqual(self.select(), dict(mb=1000, source='default', n=0))
        self.assertEqual(self.select(kind=None), dict(mb=1, source='project p95', n=21))
        for path in ('', str(self.root / 'missing'), str(self.root)):
            with patch.dict(os.environ, ADMISSION_RSS_HISTORY=path):
                self.assertEqual(self.select()['source'], 'default')
        with patch.dict(os.environ):
            del os.environ['ADMISSION_RSS_HISTORY']
            self.assertEqual(self.select()['mb'], 1000)

    def test_completed_positive_finite_recent_sampled_peaks_only(self):
        bad = [self.row('running', event='tick'), self.row('old', t=self.now-31*86400),
               self.row('future', t=self.now+1), self.row('unsampled', samples=0),
               self.row('negative', peak=-1), self.row('zero', peak=0), self.row('nan', peak=math.nan),
               self.row('inf', peak=math.inf), self.row('bool', peak=True), self.row('str', peak='2'),
               self.row('badtime', t='now'), self.row('badsamples', samples='2'), {'event': 'end'}]
        for row in bad:
            with self.subTest(row=row):
                self.write_rows([dict(row, key=str(i)) for i in range(3)] + [None, []])
                with self.history.open('a') as stream:
                    stream.write('malformed JSON\n')
                self.assertEqual(self.select()['source'], 'default')
        self.write_rows([self.row(str(i), t=self.now-30*86400) for i in range(3)])
        self.assertEqual(self.select()['n'], 3)

    def test_current_file_wins_over_rotated_duplicate(self):
        self.write_rows([self.row(str(i), 9999) for i in range(3)],
                        self.history.with_name(self.history.name+'.19700719'))
        self.write_rows([self.row(str(i), 100) for i in range(3)])
        self.assertEqual(self.select(), dict(mb=100, source='project/kind p95', n=3))

    def test_dedup_rotations_and_round_to_even_quantile(self):
        # n=11: round(.95*10) = round(9.5) = 10. n=31: round(28.5) = 28.
        for n, expected in ((11, 11), (31, 29)):
            rows = [self.row(str(i), i+1) for i in range(n)]
            rotation = self.history.with_name(self.history.name+'.19700719')
            self.write_rows(rows, rotation)
            self.write_rows(rows)
            self.assertEqual(self.select(), dict(mb=expected, source='project/kind p95', n=n))
        # Non-daily siblings must never enter the distribution.
        self.write_rows([self.row('stray', 9999)], self.history.with_name(self.history.name+'.tmp'))
        self.assertEqual(self.select()['n'], 31)


class ProbeTests(unittest.TestCase):
    def result(self, text, code=0):
        return subprocess.CompletedProcess([], code, text)

    def test_commands_timeouts_units_and_parsing(self):
        for function, command, text, expected in (
            (admission.free_percent, ['memory_pressure', '-Q'], 'System-wide memory free percentage: 50%', 50),
            (admission.pressure_level, ['sysctl', '-n', 'kern.memorystatus_vm_pressure_level'], '2', 2),
            (admission.physical_mb, ['sysctl', '-n', 'hw.memsize'], '1073741824', 1024)):
            with patch.object(subprocess, 'run', return_value=self.result(text)) as run:
                self.assertEqual(function(3.5), expected)
                self.assertEqual(run.call_args.args[0], command)
                self.assertEqual(run.call_args.kwargs['timeout'], 3.5)
            for error in (OSError(), subprocess.TimeoutExpired('probe', 3.5)):
                with patch.object(subprocess, 'run', side_effect=error):
                    self.assertIsNone(function(3.5))
            with patch.object(subprocess, 'run', return_value=self.result(text, 1)):
                self.assertIsNone(function(3.5))
        for function, texts in ((admission.free_percent, ['free percentage: 50%', 'System-wide memory free percentage: 101%', 'invalid']),
                                (admission.physical_mb, ['0', '-1', 'nan', '1.5']),
                                (admission.pressure_level, ['3', '0', 'unknown'])):
            for text in texts:
                with patch.object(subprocess, 'run', return_value=self.result(text)):
                    self.assertIsNone(function(2))

    def test_process_tree_includes_worker_siblings_but_excludes_unrelated(self):
        table = '1 0 100 init\n10 1 1024 /bin/Runner.Worker\n11 10 2048 python\n12 10 4096 build\n13 12 8192 solver\n20 1 99999 unrelated'
        with patch.object(admission, 'probe', return_value=table) as probe:
            self.assertEqual(admission.process_tree_mb(11, 2), 15)
            probe.assert_called_once_with(['ps', '-axo', 'pid=,ppid=,rss=,comm='], 2)
            self.assertEqual(admission.process_tree_mb(12, 2), 15)
            self.assertIsNone(admission.process_tree_mb(99, 2))
        with patch.object(admission, 'probe', return_value='10 1 1024 wrapper\n11 10 2048 child\n20 1 90000 other'):
            self.assertEqual(admission.process_tree_mb(10, 2), 3)
        for table in (None, '', 'garbage', '10 1 -1 bad'):
            with patch.object(admission, 'probe', return_value=table):
                self.assertIsNone(admission.process_tree_mb(10, 2))


if __name__ == '__main__':
    unittest.main()
