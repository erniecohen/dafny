"""Exercise admission with real advisory locks, including owner loss and termination."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest

ACTION = Path(__file__).resolve().parents[1] / 'actions/self-hosted-job'
spec = importlib.util.spec_from_file_location('admission', ACTION / 'admit.py')
admission = importlib.util.module_from_spec(spec)
spec.loader.exec_module(admission)


class AdmissionTests(unittest.TestCase):
    def wait_ready(self, path):
        for _ in range(100):
            if path.exists():
                return
            time.sleep(.05)
        self.fail('Admission did not complete')

    def test_serializes_and_recovers_after_keeper_termination(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owner = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            keepers = []
            try:
                time.sleep(.2)  # macOS Python launchers can re-exec before reaching the sleep.
                identity = admission.identity(owner.pid)
                for n in range(2):
                    keepers.append(subprocess.Popen([sys.executable, str(ACTION / 'admit.py'),
                        str(root / 'lock'), str(root / str(n)), str(owner.pid), identity, '1']))
                    if n == 0:
                        self.wait_ready(root / '0')
                time.sleep(.2)
                self.assertFalse((root / '1').exists())
                keepers[0].terminate()
                keepers[0].wait(timeout=5)
                self.wait_ready(root / '1')
                owner.terminate()
                owner.wait(timeout=5)
                self.assertEqual(keepers[1].wait(timeout=5), 0)
            finally:
                for child in [*keepers, owner]:
                    if child.poll() is None:
                        child.terminate()
                    child.wait(timeout=5)

    def test_wrong_owner_identity_never_admits(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            child = subprocess.run([sys.executable, str(ACTION / 'admit.py'),
                str(root / 'lock'), str(root / 'ready'), str(__import__('os').getpid()), 'wrong identity'], timeout=5)
            self.assertEqual(child.returncode, 0)
            self.assertFalse((root / 'ready').exists())

    def test_default_two_slots_hold_third_job_until_release(self):
        import os
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            keepers = []
            try:
                for n in range(3):
                    keepers.append(subprocess.Popen([sys.executable, str(ACTION / 'admit.py'),
                        str(root / 'lock'), str(root / str(n)), str(os.getpid()),
                        admission.identity(os.getpid())]))
                    if n < 2:
                        self.wait_ready(root / str(n))
                time.sleep(1.2)
                self.assertFalse((root / '2').exists())
                self.assertTrue(all(child.poll() is None for child in keepers))
                keepers[0].terminate(); keepers[0].wait(timeout=5)
                self.wait_ready(root / '2')
                self.assertIsNone(keepers[1].poll())
            finally:
                for child in keepers:
                    if child.poll() is None:
                        child.terminate()
                    child.wait(timeout=5)

    def test_invalid_capacity_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'positive'):
            admission.hold('unused', 'unused', 0, '', 0)

class MemoryAdmissionTests(unittest.TestCase):
    def setUp(self):
        self.saved = admission.free_percent, admission.pressure_level, admission.MEMORY_POLL_SECONDS
        admission.MEMORY_POLL_SECONDS = 1

    def tearDown(self):
        admission.free_percent, admission.pressure_level, admission.MEMORY_POLL_SECONDS = self.saved

    def probes(self, free, level):
        admission.free_percent = lambda: free
        admission.pressure_level = lambda: level

    def test_thresholds_are_validated_and_empty_drops_them(self):
        self.assertEqual(admission.thresholds(), (None, None))
        self.assertEqual(admission.thresholds('40', 'normal'), (40, 1))
        self.assertEqual(admission.thresholds('', 'warn'), (None, 2))
        for bad in ['-1', '101', '4.5', 'forty', ' 40']:
            with self.assertRaisesRegex(ValueError, 'percentage'):
                admission.thresholds(bad, '')
        with self.assertRaisesRegex(ValueError, 'pressure'):
            admission.thresholds('', 'high')

    def test_healthy_only_at_or_above_floor_and_at_or_below_ceiling(self):
        self.probes(40, 1)
        self.assertTrue(admission.memory_healthy(40, 1))
        self.probes(39, 1)
        self.assertFalse(admission.memory_healthy(40, 1))
        self.probes(90, 2)
        self.assertFalse(admission.memory_healthy(40, 1))
        self.assertTrue(admission.memory_healthy(40, 2))
        self.probes(None, 1)
        self.assertFalse(admission.memory_healthy(40, 1))
        self.probes(90, None)
        self.assertFalse(admission.memory_healthy(None, 1))
        # Vacuity: with no thresholds the probes are never consulted.
        def fail():
            raise AssertionError('probe consulted without a threshold')
        admission.free_percent = admission.pressure_level = fail
        self.assertTrue(admission.memory_healthy(None, None))

    def test_real_probes_read_this_host(self):
        if sys.platform != 'darwin':
            self.skipTest('The probes are macOS commands')
        free = self.saved[0]()
        self.assertIsInstance(free, int)
        self.assertTrue(0 <= free <= 100)
        self.assertIn(self.saved[1](), admission.PRESSURE.values())

    def test_unhealthy_memory_waits_without_holding_a_slot_then_admits(self):
        import fcntl
        import threading
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owner = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            try:
                time.sleep(.2)
                state = {'free': 10}
                admission.free_percent = lambda: state['free']
                admission.pressure_level = lambda: 1
                keeper = threading.Thread(target=admission.hold, daemon=True, args=(
                    str(root / 'lock'), str(root / 'ready'), owner.pid, admission.identity(owner.pid), 1, '40', 'normal'))
                keeper.start()
                time.sleep(2.5)
                self.assertFalse((root / 'ready').exists())
                # The waiter released the slot while memory was low, so another job could take it.
                with open(root / 'lock.0', 'a') as stream:
                    fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    fcntl.flock(stream, fcntl.LOCK_UN)
                state['free'] = 80
                AdmissionTests.wait_ready(self, root / 'ready')
                # Admitted jobs are never stopped when memory drops afterwards.
                state['free'] = 0
                time.sleep(1.5)
                self.assertTrue(keeper.is_alive())
                with open(root / 'lock.0', 'a') as stream:
                    with self.assertRaises(BlockingIOError):
                        fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
                owner.terminate(); owner.wait(timeout=5)
                keeper.join(timeout=5)
                self.assertFalse(keeper.is_alive())
            finally:
                if owner.poll() is None:
                    owner.terminate()
                owner.wait(timeout=5)

    def test_command_line_rejects_bad_thresholds(self):
        import os
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            child = subprocess.run([sys.executable, str(ACTION / 'admit.py'), str(root / 'lock'), str(root / 'ready'),
                str(os.getpid()), admission.identity(os.getpid()), '1', '40', 'high'],
                capture_output=True, text=True, timeout=5)
            self.assertNotEqual(child.returncode, 0)
            self.assertIn('pressure', child.stderr)
            self.assertFalse((root / 'ready').exists())


class CapacityInputTests(unittest.TestCase):
    def test_default_and_configured_capacity_and_invalid_inputs(self):
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        script = """
const assert = require('node:assert/strict');
const {slots} = require(process.argv[1]);
delete process.env.INPUT_SLOTS;
assert.equal(slots(), 2);
process.env.INPUT_SLOTS = '3';
assert.equal(slots(), 3);
assert.equal(slots('1'), 1);
for (const bad of ['0', '-1', '2.5', 'NaN', '3garbage', '9007199254740992']) {
  assert.throws(() => slots(bad), /positive safe integer/);
}
"""
        subprocess.run([node, '-e', script, str(ACTION / 'index.cjs')], check=True, timeout=5)

    def test_memory_threshold_inputs_pass_through_and_default_empty(self):
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        script = """
const assert = require('node:assert/strict');
const {memoryThresholds} = require(process.argv[1]);
assert.deepEqual(memoryThresholds({}), ['', '']);
assert.deepEqual(memoryThresholds({'INPUT_MIN-FREE-PERCENT': '40', 'INPUT_MAX-PRESSURE': 'normal'}), ['40', 'normal']);
assert.deepEqual(memoryThresholds({'INPUT_MIN-FREE-PERCENT': ' 40 ', 'INPUT_MAX-PRESSURE': ''}), ['40', '']);
"""
        subprocess.run([node, '-e', script, str(ACTION / 'index.cjs')], check=True, timeout=5)


class CleanupTests(unittest.TestCase):
    def test_post_removes_only_job_outputs_and_stops_its_keeper(self):
        import os
        import shutil
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner for the post action')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repo = root / 'repo'; repo.mkdir()
            tmp = root / 'fork-job.example'; tmp.mkdir()
            unrelated = root / 'unrelated'; unrelated.mkdir()
            subprocess.run(['git', 'init', '-q', str(repo)], check=True)
            (repo / 'tracked').touch()
            subprocess.run(['git', '-C', str(repo), 'add', 'tracked'], check=True)
            (repo / 'output').touch()
            keeper = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            try:
                time.sleep(.2)
                env = dict(os.environ, STATE_keeper=str(keeper.pid), STATE_keeperIdentity=admission.identity(keeper.pid),
                           STATE_tmp=str(tmp), RUNNER_TEMP=str(root), GITHUB_WORKSPACE=str(repo))
                subprocess.run([node, str(ACTION / 'index.cjs')], env=env, check=True, timeout=5)
                self.assertTrue((repo / 'tracked').exists())
                self.assertFalse((repo / 'output').exists())
                self.assertFalse(tmp.exists())
                self.assertTrue(unrelated.exists())
                self.assertNotEqual(keeper.wait(timeout=5), 0)
            finally:
                if keeper.poll() is None:
                    keeper.terminate()
                keeper.wait(timeout=5)


class WaitingCancellationTests(unittest.TestCase):
    def test_waiting_keeper_exits_when_its_owner_disappears(self):
        import os
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owner = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])
            keepers = []
            try:
                time.sleep(.2)
                for n, pid in enumerate([os.getpid(), owner.pid]):
                    keepers.append(subprocess.Popen([sys.executable, str(ACTION / 'admit.py'),
                        str(root / 'lock'), str(root / str(n)), str(pid), admission.identity(pid), '1']))
                    if n == 0:
                        AdmissionTests.wait_ready(self, root / '0')
                time.sleep(.2)
                self.assertFalse((root / '1').exists())
                owner.terminate(); owner.wait(timeout=5)
                self.assertEqual(keepers[1].wait(timeout=5), 0)
                self.assertIsNone(keepers[0].poll())
            finally:
                for child in [*keepers, owner]:
                    if child.poll() is None:
                        child.terminate()
                    child.wait(timeout=5)


if __name__ == '__main__':
    unittest.main()
