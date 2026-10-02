"""Infrastructure tests use tiny stand-in processes, never Dafny or Z3."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("resolver_matrix", Path(__file__).with_name("resolver-matrix.py"))
matrix = importlib.util.module_from_spec(spec)
spec.loader.exec_module(matrix)


class ResolverMatrixTests(unittest.TestCase):
    def test_classification_does_not_accept_crashes_as_diagnostics(self):
        self.assertEqual(matrix.classify(0, "Dafny program verifier did not attempt verification"), "accepted")
        self.assertEqual(matrix.classify(2, "x.dfy(1,2): Error: unknown identifier"), "rejected")
        self.assertEqual(matrix.classify(2, "System.NullReferenceException: broken"), "crash")
        self.assertEqual(matrix.classify(-11, ""), "crash")
        self.assertEqual(matrix.classify(2, ""), "unexpected-exit")
        self.assertEqual(matrix.classify(1, "Error: invalid option"), "unexpected-exit")
        self.assertEqual(matrix.classify(-9, "", True), "timeout")

    def test_full_diagnostics_and_case_inventory_are_compared(self):
        expected = {"a.legacy": {"outcome": "rejected", "exit": 2, "stdout": "Error: first", "stderr": ""}}
        changed = {"a.legacy": dict(expected["a.legacy"], stdout="Error: different")}
        self.assertIn("different", matrix.differences(expected, changed))
        self.assertTrue(matrix.differences(expected, {}))
        self.assertTrue(matrix.differences({}, expected))
        self.assertEqual(matrix.differences(expected, expected), "")

    def test_both_output_pipes_are_drained(self):
        code = "import sys; sys.stderr.write('e' * 200000); sys.stdout.write('o' * 200000)"
        rc, stdout, stderr, timed_out = matrix.execute([sys.executable, "-c", code], Path.cwd(), 5)
        self.assertEqual((rc, len(stdout), len(stderr), timed_out), (0, 200000, 200000, False))

    @unittest.skipUnless(os.name == "posix", "CI runner uses POSIX process groups")
    def test_deadline_kills_parent_and_descendant_holding_output_pipe(self):
        code = "import subprocess,sys,time; subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)']); print('started', flush=True); time.sleep(60)"
        start = time.monotonic()
        rc, stdout, stderr, timed_out = matrix.execute([sys.executable, "-c", code], Path.cwd(), 0.5)
        self.assertTrue(timed_out)
        self.assertIn("started", stdout)
        self.assertLess(time.monotonic() - start, 5)

    def test_deadline_race_still_records_timeout_and_partial_output(self):
        process = mock.MagicMock()
        process.returncode = 0
        process.communicate.side_effect = [
            matrix.subprocess.TimeoutExpired(["fake"], 1), (b"partial", b"diagnostic")]
        with mock.patch.object(matrix.subprocess, "Popen") as popen, \
                mock.patch.object(matrix.os, "killpg", side_effect=ProcessLookupError):
            popen.return_value.__enter__.return_value = process
            result = matrix.execute(["fake"], Path.cwd(), 1)
        self.assertEqual(result, (0, "partial", "diagnostic", True))
        self.assertEqual(matrix.classify(result[0], result[1] + result[2], result[3]), "timeout")

    def test_refresh_only_feature_options_do_not_short_circuit_legacy_resolution(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            case = {"id": "feature", "file": "feature.dfy", "options": ["--allow-warnings"],
                    "refresh_options": ["--general-newtypes:true"]}
            with mock.patch.object(matrix, "execute", return_value=(0, "", "", False)) as execute:
                for refresh in (False, True):
                    matrix.run_case(case, refresh, root, root / "dafny", root)
                    command = execute.call_args.args[0]
                    self.assertIn("--type-system-refresh:" + str(refresh).lower(), command)
                    self.assertEqual("--general-newtypes:true" in command, refresh)
                    self.assertIn("--allow-warnings", command)

    def test_manifest_rejects_mode_override_duplicate_id_and_missing_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "test.dfy").write_text("")
            manifest = root / "manifest.json"
            case = {"id": "case", "file": "test.dfy", "options": []}
            manifest.write_text(json.dumps([case]))
            self.assertEqual(matrix.read_manifest(manifest, root), [case])
            for invalid in [[case, case], [dict(case, file="missing.dfy")],
                            [dict(case, options=["--type-system-refresh:false"])],
                            [dict(case, file="../test.dfy")]]:
                manifest.write_text(json.dumps(invalid))
                with self.assertRaises(ValueError):
                    matrix.read_manifest(manifest, root)

    @unittest.skipUnless(os.name == "posix", "CI runner uses POSIX executables")
    def test_probe_captures_failure_but_strict_run_rejects_unsafe_baseline(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "test.dfy").write_text("")
            fake = root / "fake"
            fake.write_text("#!/bin/sh\necho 'System.NullReferenceException: deliberate stand-in'\nexit 2\n")
            fake.chmod(0o755)
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps([{"id": "case", "file": "test.dfy"}]))
            expected = root / "expected.json"
            out = root / "out"
            args = ["--manifest", str(manifest), "--lit-dir", str(root), "--dafny", str(fake),
                    "--expected", str(expected), "--output", str(out)]
            # Unit-test output must not write a deliberate failure into a job summary.
            previous = os.environ.pop("GITHUB_STEP_SUMMARY", None)
            try:
                self.assertEqual(matrix.main(args + ["--probe"]), 0)
                actual = json.loads((out / "actual.json").read_text())
                self.assertEqual(set(actual), {"case.legacy", "case.refresh"})
                expected.write_text(json.dumps(actual))
                self.assertEqual(matrix.main(args), 1)
            finally:
                if previous is not None:
                    os.environ["GITHUB_STEP_SUMMARY"] = previous


if __name__ == "__main__":
    unittest.main()
