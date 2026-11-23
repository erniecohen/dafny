"""Resource accounting must preserve the verifier exit and report RSS in MiB."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('window', Path(__file__).with_name('issue241-module-window.py'))
window = importlib.util.module_from_spec(spec)
spec.loader.exec_module(window)


class AccountingTests(unittest.TestCase):
    def test_platform_units_and_failed_verifier_exit(self):
        for platform, rss in [('darwin', 100 * 1024 * 1024), ('linux', 100 * 1024)]:
            with self.subTest(platform=platform), tempfile.TemporaryDirectory() as directory:
                out = Path(directory) / 'result'
                process = SimpleNamespace(pid=123, returncode=None)
                usage = SimpleNamespace(ru_maxrss=rss, ru_utime=1.0, ru_stime=.5)
                with patch.object(window.subprocess, 'Popen', return_value=process), \
                     patch.object(window.os, 'wait4', return_value=(123, 9 << 8, usage)), \
                     patch.object(window.sys, 'platform', platform):
                    self.assertEqual(window.run(out, 'dafny', 'z3', directory, 1, [], cores=2), 9)
                data = window.measures(out)
                self.assertEqual(data['exit'], '9')
                self.assertEqual(data['rss_mib'], 100)
                self.assertEqual(data['cpu'], 1.5)
