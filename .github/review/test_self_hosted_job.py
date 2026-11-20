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
