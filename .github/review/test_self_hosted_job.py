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
                        str(root / 'lock'), str(root / str(n)), str(owner.pid), identity]))
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


if __name__ == '__main__':
    unittest.main()
