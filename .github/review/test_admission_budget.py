"""Admission protocol tests: real flocks/processes, synthetic host configuration."""
from contextlib import ExitStack
import fcntl
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

ACTION = Path(__file__).resolve().parents[1] / 'actions/self-hosted-job'
spec = importlib.util.spec_from_file_location('budget_admission', ACTION / 'admit.py')
admission = importlib.util.module_from_spec(spec)
spec.loader.exec_module(admission)
POLICY = dict(reserve_mb=100, default_peak_mb=1000, peak_min_samples=3, peak_history_days=30,
              probe_timeout_s=2, slot_poll_s=.05, memory_backoff_s=[.05, .1, .2])
FREE_PROBE = admission.free_percent
PRESSURE_PROBE = admission.pressure_level


class AdmissionFixture(unittest.TestCase):
    def setUp(self):
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.root = Path(self.stack.enter_context(tempfile.TemporaryDirectory()))
        self.locks = self.root / 'locks'
        self.locks.mkdir()
        self.projects = self.root / 'projects'
        self.projects.mkdir()
        self.runners = self.root / 'runners.json'
        env = {k: v for k, v in os.environ.items() if not k.startswith('ADMISSION_')}
        env.update(ADMISSION_RUNNERS_JSON=str(self.runners), ADMISSION_PROJECTS_DIR=str(self.projects),
                   ADMISSION_LOCK_DIR=str(self.locks))
        self.stack.enter_context(patch.dict(os.environ, env, clear=True))
        self.stack.enter_context(patch.object(admission, 'free_percent', return_value=100))
        self.stack.enter_context(patch.object(admission, 'pressure_level', return_value=1))
        self.stack.enter_context(patch.object(admission, 'physical_mb', return_value=1000000))
        self.write_config()
        self.entry = {'name': 'example', 'repos': ['example/repo'],
                      'peak_mb': {'ci:large': 13000, 'ci:proof': 3500, 'ci:small': 3400,
                                  'heavy_step': 9000, 'ci_job': 7000, '_note': 'synthetic'}}
        self.write_project()

    def write_config(self, budget=13000, slots=6, **extra):
        self.runners.write_text(json.dumps(dict(studio_ci_budget_mb=budget, admission_slots=slots, **extra)))

    def write_project(self):
        (self.projects / 'example.json').write_text(json.dumps(self.entry))

    def attempt(self, kind='ci:small', repository='example/repo', slots=6, policy=None, caller=None):
        return admission.try_admit(self.locks / 'active.lock', slots, repository, kind, policy or POLICY, caller, owner_pid=os.getpid())

    def take(self, kind='ci:small', **kwargs):
        stream, reason = self.attempt(kind, **kwargs)
        self.assertIsNone(reason)
        self.assertIsNotNone(stream)
        self.addCleanup(lambda: admission.release(stream) if not stream.closed else None)
        return stream

    def occupy(self, name, amount=9000, **metadata):
        stream = admission.try_lock(self.locks / name)
        self.assertIsNotNone(stream)
        self.addCleanup(lambda: stream.close() if not stream.closed else None)
        data = dict(pid=os.getpid(), owner_pid=os.getpid(), project='other', kind='heavy_step', declared_mb=amount, started=time.time())
        data.update(metadata)
        Path(str(stream.name) + '.json').write_text(json.dumps(data))
        return stream

    def assert_free(self, name):
        stream = admission.try_lock(self.locks / name)
        self.assertIsNotNone(stream, name)
        stream.close()

    def denied(self, category='budget', **kwargs):
        stream, reason = self.attempt(**kwargs)
        if stream:
            admission.release(stream)
        self.assertIsNone(stream)
        self.assertTrue(reason.startswith(category), reason)
        self.assert_free('budget.lock')


class BudgetTests(AdmissionFixture):
    def test_distinct_weights_and_equality(self):
        self.take('ci:proof')
        self.take('ci:small')
        self.take('ci:small')
        self.denied()
        self.write_config(13700)
        self.take('ci:small')

    def test_one_large_fits_two_do_not(self):
        self.take('ci:large')
        self.denied(kind='ci:large')
        self.assert_free('active.lock.1')
        self.assertFalse((self.locks / 'active.lock.1.json').exists())

    def test_three_small_fit_fourth_does_not(self):
        for _ in range(3):
            self.take()
        self.denied()

    def test_reserve_and_high_indices_share_budget(self):
        for name in ('agent-reserve.lock', 'active.lock.91'):
            with self.subTest(name=name):
                stream = self.occupy(name, 10000)
                self.denied()
                self.assert_free('active.lock.0')
                admission.release(stream)
        self.take()

    def test_multiple_repositories_share_pool(self):
        other = dict(name='second', repos=['other/repo'], peak_mb=9700, studio_peak_mb=7200)
        (self.projects / 'other.json').write_text(json.dumps(other))
        self.take(repository='other/repo')
        self.denied()
        data = json.loads((self.locks / 'active.lock.0.json').read_text())
        self.assertEqual(data['declared_mb'], 9700)

    def test_exact_kind_unknown_and_missing_use_largest(self):
        for kind in ('ci:large', 'ci:small suffix', 'unknown', ''):
            with self.subTest(kind=kind):
                stream = self.take(kind)
                data = json.loads(admission.sidecar_path(stream).read_text())
                self.assertEqual(data['declared_mb'], 13000)
                self.assertEqual(data['kind'], kind or 'unknown')
                admission.release(stream)
        self.assertEqual(admission.declared_mb(self.entry, 'ci:proof'), 3500)

    def test_scalar_peak_applies_to_every_kind(self):
        self.entry.update(peak_mb=9700, studio_peak_mb=7200)
        self.write_project()
        stream = self.take('missing')
        self.assertEqual(json.loads(admission.sidecar_path(stream).read_text())['declared_mb'], 9700)

    def test_invalid_budget_and_slots(self):
        for key in ('studio_ci_budget_mb', 'admission_slots'):
            for value in (None, True, False, '13000', 13000.0, 0, -1):
                with self.subTest(key=key, value=value):
                    data = dict(studio_ci_budget_mb=13000, admission_slots=6)
                    data[key] = value
                    self.runners.write_text(json.dumps(data))
                    with self.assertRaises(ValueError):
                        self.attempt()

    def test_invalid_peak_declarations(self):
        for value in (None, True, '1', 1.5, 0, -1, {}, {'_note': 100}, {'ci:small': True},
                      {'ci:small': 1, 'other': 0}):
            with self.subTest(value=value):
                self.entry['peak_mb'] = value
                self.write_project()
                with self.assertRaises(ValueError):
                    self.attempt()

    def test_unregistered_ambiguous_and_invalid_registrations(self):
        with self.assertRaisesRegex(ValueError, 'exactly one'):
            self.attempt(repository='example/repo-extra')
        (self.projects / 'duplicate.json').write_text(json.dumps(self.entry))
        with self.assertRaisesRegex(ValueError, 'exactly one'):
            self.attempt()
        (self.projects / 'duplicate.json').write_text('{')
        with self.assertRaises(ValueError):
            self.attempt()
        (self.projects / 'duplicate.json').write_text(json.dumps({'name': 'invalid', 'repos': 'example/repo'}))
        with self.assertRaisesRegex(ValueError, 'registration'):
            self.attempt()

    def test_partial_empty_missing_and_malformed_configuration_fail(self):
        for variable in ('ADMISSION_RUNNERS_JSON', 'ADMISSION_PROJECTS_DIR', 'ADMISSION_LOCK_DIR'):
            with self.subTest(variable=variable), patch.dict(os.environ, {variable: ''}):
                with self.assertRaises(ValueError):
                    self.attempt()
        with patch.dict(os.environ):
            del os.environ['ADMISSION_PROJECTS_DIR']
            with self.assertRaises(ValueError):
                self.attempt()
        self.runners.write_text('{')
        with self.assertRaises(ValueError):
            self.attempt()
        self.runners.unlink()
        with self.assertRaises(OSError):
            self.attempt()
        self.write_config()
        with patch.dict(os.environ, ADMISSION_PROJECTS_DIR=str(self.root / 'missing')):
            with self.assertRaises(ValueError):
                self.attempt()

    def test_oversized_request_errors_before_locking(self):
        self.write_config(12999)
        with self.assertRaisesRegex(ValueError, 'exceeds entire'):
            self.attempt('ci:large')
        self.assertEqual(list(self.locks.iterdir()), [])

    def test_missing_probes_release_candidate(self):
        for free, pressure in ((None, 1), (80, None), (80, 4), (80, 3)):
            with self.subTest(free=free, pressure=pressure), patch.object(admission, 'free_percent', return_value=free), patch.object(admission, 'pressure_level', return_value=pressure):
                self.denied('memory')
                self.assertFalse((self.locks / 'active.lock.0.json').exists())
                self.assert_free('active.lock.0')

    def test_free_sidecars_ignored_even_with_live_or_dead_pid(self):
        for pid in (os.getpid(), 99999999):
            stream = self.occupy('active.lock.90', 13000, pid=pid)
            stream.close()
            taken = self.take()
            admission.release(taken)
        # Ignore unrelated files, partial temporary files, and metadata without a lock.
        (self.locks / 'unrelated').write_text('invalid')
        (self.locks / 'active.lock.90.json.1.tmp').write_text('invalid')
        self.take()

    def test_occupied_unaccounted_holders_refuse(self):
        stream = self.occupy('active.lock.90')
        sidecar = admission.sidecar_path(stream)
        for data in (None, '{', '[]', json.dumps({'pid': 0}), json.dumps(dict(
            pid=99999999, project='other', kind='heavy_step', declared_mb=1, started=time.time()))):
            with self.subTest(data=data):
                if data is None:
                    sidecar.unlink(missing_ok=True)
                else:
                    sidecar.write_text(data)
                self.denied('unaccounted')
                self.assert_free('active.lock.0')
                self.assertFalse((self.locks / 'active.lock.0.json').exists())

    def test_metadata_fields_are_validated(self):
        stream = self.occupy('agent-reserve.lock')
        good = json.loads(admission.sidecar_path(stream).read_text())
        for key, bad in (('pid', True), ('pid', -1), ('project', ''), ('project', 1),
                         ('kind', ''), ('kind', []), ('declared_mb', True), ('declared_mb', 0),
                         ('declared_mb', 1.5), ('started', False), ('started', True), ('started', 0), ('started', '1'),
                         ('started', float('nan')), ('started', float('inf'))):
            with self.subTest(key=key, bad=bad):
                data = dict(good, **{key: bad})
                admission.sidecar_path(stream).write_text(json.dumps(data))
                self.denied('unaccounted')

    def test_eperm_means_live(self):
        with patch.object(os, 'kill', side_effect=PermissionError):
            self.assertTrue(admission.pid_alive(123))
        with patch.object(os, 'kill', side_effect=ProcessLookupError):
            self.assertFalse(admission.pid_alive(123))

    def test_slots_clamped_and_mutex_never_retained_on_denial(self):
        self.write_config(slots=1)
        self.take()
        self.denied('slots', slots=100)
        self.assertFalse((self.locks / 'active.lock.1').exists())

    def test_busy_mutex_does_not_take_a_candidate(self):
        mutex = admission.try_lock(self.locks / 'budget.lock')
        try:
            stream, reason = self.attempt()
            self.assertIsNone(stream)
            self.assertEqual(reason, 'mutex')
            self.assertFalse((self.locks / 'active.lock.0').exists())
        finally:
            mutex.close()
        self.take()

    def test_publish_under_both_locks_and_release_order(self):
        original = os.replace
        def replace(source, target):
            for name in ('budget.lock', 'active.lock.0'):
                self.assertIsNone(admission.try_lock(self.locks / name))
            self.assertFalse((self.root / 'ready').exists())
            original(source, target)
        with patch.object(os, 'replace', side_effect=replace):
            stream = self.take()
        self.assert_free('budget.lock')
        data = json.loads(admission.sidecar_path(stream).read_text())
        self.assertEqual(set(data), {'pid', 'owner_pid', 'project', 'kind', 'declared_mb', 'expected_mb', 'started'})
        self.assertEqual(data['pid'], os.getpid())
        self.assertEqual(data['owner_pid'], os.getpid())
        self.assertGreater(data['started'], 0)
        unlink = Path.unlink
        def checked_unlink(path, *args, **kwargs):
            if path == admission.sidecar_path(stream):
                self.assertIsNone(admission.try_lock(stream.name))
            return unlink(path, *args, **kwargs)
        with patch.object(Path, 'unlink', checked_unlink):
            admission.release(stream)
        self.assertFalse(admission.sidecar_path(stream).exists())
        self.assertTrue(Path(stream.name).exists())
        self.assert_free('active.lock.0')

    def test_publication_failure_releases_locks_without_ready(self):
        with patch.object(os, 'replace', side_effect=OSError('publication failed')):
            with self.assertRaises(OSError):
                self.attempt()
        self.assertEqual(list(self.locks.glob('*.json*')), [])
        self.assert_free('active.lock.0')
        self.assert_free('budget.lock')

    def test_ready_failure_cleans_reservation(self):
        with patch.object(admission, 'identity', return_value='owner'), patch.object(Path, 'touch', side_effect=OSError):
            with self.assertRaises(OSError):
                admission.hold(self.locks / 'active.lock', self.root / 'ready', 1, 'owner', 2, POLICY, kind='ci:small', repository='example/repo')
        self.assertEqual(list(self.locks.glob('*.json')), [])
        self.assert_free('active.lock.0')

    def test_owner_lost_between_publication_and_ready_releases_reservation(self):
        stream = self.take()
        with patch.object(admission, 'identity', side_effect=['owner', 'gone', 'gone', 'gone']), patch.object(admission, 'try_admit', return_value=(stream, None)):
            admission.hold(self.locks / 'active.lock', self.root / 'ready', 1, 'owner', 2, POLICY)
        self.assertFalse((self.root / 'ready').exists())
        self.assertFalse(admission.sidecar_path(stream).exists())
        self.assert_free('active.lock.0')

    def test_wait_checks_cancellation_before_sleeping(self):
        with patch.object(admission, 'identity', side_effect=['owner', 'gone']), patch.object(admission, 'try_admit', return_value=(None, 'budget')), patch.object(time, 'sleep', side_effect=AssertionError('slept after cancellation')):
            admission.hold(self.locks / 'active.lock', self.root / 'ready', 1, 'owner', 2, POLICY)
        self.assertFalse((self.root / 'ready').exists())

    def test_probe_exception_releases_both_locks(self):
        with patch.object(admission, 'memory_healthy', side_effect=RuntimeError('probe')):
            with self.assertRaises(RuntimeError):
                self.attempt()
        self.assert_free('active.lock.0')
        self.assert_free('budget.lock')

    def test_config_reloaded_each_attempt_including_paths_and_registry(self):
        self.write_config(6000)
        first = self.take()
        self.denied()
        self.write_config(13000)
        second = self.take()
        admission.release(first)
        admission.release(second)
        self.entry['peak_mb'] = 14000
        self.write_project()
        with self.assertRaises(ValueError):
            self.attempt()
        self.entry['peak_mb'] = 100
        self.write_project()
        self.write_config(slots=1)
        other = self.root / 'new-locks'
        with patch.dict(os.environ, ADMISSION_LOCK_DIR=str(other)):
            stream = self.take()
            self.assertEqual(Path(stream.name).parent, other)
            self.denied('slots')

    def test_budget_config_does_not_use_workflow_budget_variables(self):
        with patch.dict(os.environ, STUDIO_CI_BUDGET_MB='999999', SELF_HOSTED_JOB_SLOTS='999'):
            self.write_config(3300)
            with self.assertRaisesRegex(ValueError, 'exceeds'):
                self.attempt()

    def test_lock_fds_do_not_survive_exec_even_when_close_fds_false(self):
        children = []
        def probe(*args):
            # The probe runs while both reservation and transaction locks are held.
            child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'], close_fds=False)
            children.append(child)
            return True, 'synthetic'
        with patch.object(admission, 'memory_healthy', side_effect=probe):
            stream = self.take()
        self.assertFalse(os.get_inheritable(stream.fileno()))
        child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'], close_fds=False)
        try:
            admission.release(stream)
            self.assert_free('active.lock.0')
            self.assert_free('budget.lock')
        finally:
            for process in [child, *children]:
                process.terminate()
                process.wait(timeout=5)

    def test_no_host_budget_still_enforces_headroom_and_growth(self):
        with patch.dict(os.environ):
            del os.environ['ADMISSION_RUNNERS_JSON']
            del os.environ['ADMISSION_PROJECTS_DIR']
            stream = self.take(repository='unregistered')
            self.assertTrue(admission.sidecar_path(stream).exists())
            self.assertTrue((self.locks / 'budget.lock').exists())
            with patch.object(admission, 'free_percent', return_value=0):
                self.denied('memory')

    def test_invalid_budget_fails_cli_with_exit_two_and_no_ready(self):
        self.write_config(budget=True)
        result = subprocess.run([sys.executable, str(ACTION / 'admit.py'), str(self.locks / 'active.lock'), str(self.root / 'ready'), str(os.getpid()), admission.identity(os.getpid()), '6', json.dumps(POLICY), 'ci:small', 'example/repo'], capture_output=True, text=True, timeout=5)
        self.assertEqual(result.returncode, 2)
        self.assertIn('positive integer', result.stderr)
        self.assertFalse((self.root / 'ready').exists())
        self.assertEqual(list(self.locks.iterdir()), [])

    def test_bounded_missing_failed_and_malformed_probes(self):
        for error in (FileNotFoundError(), subprocess.TimeoutExpired('probe', 5)):
            with patch.object(subprocess, 'run', side_effect=error):
                self.assertIsNone(FREE_PROBE(2))
                self.assertIsNone(PRESSURE_PROBE(2))
        for stdout, code in (('invalid', 0), ('0', 0), ('3', 0), ('1', 1)):
            with patch.object(subprocess, 'run', return_value=subprocess.CompletedProcess([], code, stdout)) as run:
                self.assertIsNone(PRESSURE_PROBE(2))
                self.assertEqual(run.call_args.kwargs['timeout'], 2)


class ProcessTests(AdmissionFixture):
    def spawn(self, number, owner=None, kind='ci:small', pause_publish=False, physical_mb=1000000, orphan=False):
        ready = self.root / f'ready{number}'
        gate = self.root / 'publish-gate'
        script = '''
import os, signal, sys, time
sys.path.insert(0, sys.argv[1])
import admit as a
a.free_percent = lambda timeout: 100
a.physical_mb = lambda timeout: float(sys.argv[8])
a.process_tree_mb = lambda pid, timeout: 100
a.pressure_level = lambda timeout: 1
signal.signal(signal.SIGTERM, a.stop)
signal.signal(signal.SIGINT, a.stop)
if sys.argv[6] == 'pause':
    publish = a.publish
    def paused(*args):
        open(sys.argv[7] + '.entered', 'w').close()
        while not os.path.exists(sys.argv[7]) and not a.STOPPED: time.sleep(.01)
        publish(*args)
    a.publish = paused
pid = int(sys.argv[3])
a.hold(sys.argv[2] + '/active.lock', sys.argv[4], pid, a.identity(pid), 6,
       dict(reserve_mb=100, default_peak_mb=1000, peak_min_samples=3, peak_history_days=30,
            probe_timeout_s=2, slot_poll_s=.05, memory_backoff_s=[.05, .1, .2]),
       kind=sys.argv[5], repository='example/repo')
'''
        command = [sys.executable, '-c', script, str(ACTION), str(self.locks),
                   str(owner or os.getpid()), str(ready), kind,
                   'pause' if pause_publish else '', str(gate), str(physical_mb)]
        if orphan:
            launcher = ('import json, subprocess, sys; '
                        'p = subprocess.Popen(json.loads(sys.argv[1]), stdin=subprocess.DEVNULL, '
                        'stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True); '
                        'print(p.pid)')
            result = subprocess.run([sys.executable, '-c', launcher, json.dumps(command)],
                                    capture_output=True, text=True, check=True, timeout=5)
            child_pid = int(result.stdout)
            def cleanup_orphan():
                try:
                    os.kill(child_pid, signal.SIGTERM)
                except ProcessLookupError:
                    return
                self.until(lambda: not admission.identity(child_pid))
            self.addCleanup(cleanup_orphan)
            return child_pid, ready
        child = subprocess.Popen(command, stdout=subprocess.DEVNULL)
        def cleanup():
            if child.poll() is None:
                child.terminate()
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(timeout=5)
        self.addCleanup(cleanup)
        return child, ready

    def until(self, condition):
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            if condition():
                return
            time.sleep(.02)
        self.fail('Timed out waiting for keeper state')

    def test_orphaned_keeper_records_worker_and_credits_worker_tree(self):
        keeper_pid, ready = self.spawn(1, orphan=True)
        self.until(ready.exists)
        parent = subprocess.run(['ps', '-p', str(keeper_pid), '-o', 'ppid='],
                                capture_output=True, text=True, check=True, timeout=2)
        self.assertEqual(int(parent.stdout), 1)  # The main action process has exited.
        data = json.loads((self.locks / 'active.lock.0.json').read_text())
        self.assertEqual(data['pid'], keeper_pid)  # Shared readers identify the flock holder.
        self.assertEqual(data['owner_pid'], os.getpid())  # The worker outlives the step.
        build_pid = max(os.getpid(), keeper_pid) + 10000
        table = (f'1 0 100 init\n{os.getpid()} 1 819200 /bin/Runner.Worker\n'
                 f'{build_pid} {os.getpid()} 614400 build\n{keeper_pid} 1 1024 python')
        with patch.object(admission, 'probe', return_value=table):
            total, outstanding, unknown = admission.pool_usage(self.locks, 'unused', 2)
        self.assertEqual((total, outstanding, unknown), (3400, 0, []))

    def test_concurrent_processes_cannot_spend_same_remainder(self):
        self.write_config(13000)
        first, ready1 = self.spawn(1, kind='ci:large')
        second, ready2 = self.spawn(2, kind='ci:large')
        self.until(lambda: ready1.exists() or ready2.exists())
        time.sleep(.3)
        self.assertEqual(int(ready1.exists()) + int(ready2.exists()), 1)
        winner, loser_ready = (first, ready2) if ready1.exists() else (second, ready1)
        winner.terminate()
        winner.wait(timeout=5)
        self.until(loser_ready.exists)

    def test_concurrent_growth_cannot_spend_same_headroom(self):
        self.write_config(50000)
        first, ready1 = self.spawn(1, physical_mb=2800)
        self.until(ready1.exists)
        second, ready2 = self.spawn(2, physical_mb=2800)
        self.until(ready2.exists)
        third, ready3 = self.spawn(3, physical_mb=2800)
        time.sleep(.3)
        self.assertFalse(ready3.exists())
        self.assertIsNone(third.poll())
        self.assert_free('active.lock.2')
        first.terminate()
        first.wait(timeout=5)
        self.until(ready3.exists)

    def test_ready_only_after_publication_and_no_lock_fds_in_probe_children(self):
        child, ready = self.spawn(1, pause_publish=True)
        self.until((self.root / 'publish-gate.entered').exists)
        self.assertFalse(ready.exists())
        self.assertFalse((self.locks / 'active.lock.0.json').exists())
        self.assertIsNone(admission.try_lock(self.locks / 'budget.lock'))
        (self.root / 'publish-gate').touch()
        self.until(ready.exists)
        data = json.loads((self.locks / 'active.lock.0.json').read_text())
        self.assertEqual(data['pid'], child.pid)
        self.assertEqual(data['owner_pid'], os.getpid())
        self.assert_free('budget.lock')

    def test_signals_cleanup_while_holding_and_waiting(self):
        for sig in (signal.SIGTERM, signal.SIGINT):
            with self.subTest(signal=sig):
                holder, ready = self.spawn(1, kind='ci:large')
                self.until(ready.exists)
                waiter, waiting = self.spawn(2)
                time.sleep(.15)
                waiter.send_signal(sig)
                waiter.wait(timeout=5)
                self.assertFalse(waiting.exists())
                self.assertIsNone(holder.poll())
                holder.send_signal(sig)
                holder.wait(timeout=5)
                self.assertEqual(list(self.locks.glob('*.json')), [])
                self.assert_free('active.lock.0')
                ready.unlink()

    def test_cancellation_during_publication_cleans_before_exit(self):
        child, ready = self.spawn(1, pause_publish=True)
        self.until((self.root / 'publish-gate.entered').exists)
        child.terminate()
        child.wait(timeout=5)
        self.assertFalse(ready.exists())
        self.assertEqual(list(self.locks.glob('*.json')), [])
        self.assert_free('active.lock.0')

    def test_waiter_rereads_registry_and_budget_before_next_attempt(self):
        reserve = self.occupy('agent-reserve.lock', 9000)
        self.write_config(10000)
        child, ready = self.spawn(1)
        self.until((self.locks / 'active.lock.0').exists)
        self.assertFalse(ready.exists())
        self.entry['peak_mb']['ci:small'] = 1000
        self.write_project()
        self.until(ready.exists)
        self.assertIsNone(child.poll())
        data = json.loads((self.locks / 'active.lock.0.json').read_text())
        self.assertEqual(data['declared_mb'], 1000)
        self.write_config(1)
        time.sleep(.1)
        self.assertIsNone(child.poll())  # Admitted work survives later configuration changes.

    def test_owner_loss_while_holding_and_waiting(self):
        owner = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
        try:
            time.sleep(.2)
            holder, ready = self.spawn(1, owner=owner.pid, kind='ci:large')
            self.until(ready.exists)
            waiter, waiting = self.spawn(2, owner=owner.pid)
            time.sleep(.15)
            owner.terminate()
            owner.wait(timeout=5)
            self.assertEqual(holder.wait(timeout=5), 0)
            self.assertEqual(waiter.wait(timeout=5), 0)
            self.assertFalse(waiting.exists())
            self.assertEqual(list(self.locks.glob('*.json')), [])
        finally:
            if owner.poll() is None:
                owner.terminate()
            owner.wait(timeout=5)


if __name__ == '__main__':
    unittest.main()
