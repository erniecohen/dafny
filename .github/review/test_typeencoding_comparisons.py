"""A failed verifier or missing translation must not satisfy a comparison gate."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch


def comparisons():
    for path in Path(__file__).parent.glob('typeencoding-*.py'):
        spec = importlib.util.spec_from_file_location(path.stem, path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        yield module


class TypeEncodingGateTests(unittest.TestCase):
    def test_identical_failures_without_translation_are_rejected(self):
        for module in comparisons():
            with self.subTest(module=module.__name__), tempfile.TemporaryDirectory() as directory:
                args = SimpleNamespace(output=directory, baseline='base', dafny='candidate', z3='solver')
                result = SimpleNamespace(returncode=4, stdout=b'', stderr=b'')
                with patch.object(module.subprocess, 'run', return_value=result), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(module.identity(args), 1)

    def test_expected_diagnostic_with_wrong_exit_is_rejected(self):
        for module in comparisons():
            with self.subTest(module=module.__name__), tempfile.TemporaryDirectory() as directory:
                args = SimpleNamespace(output=directory, dafny='candidate', z3='solver')

                def failed(command, **_):
                    fixture = Path(command[2])
                    suffix = '.legacy.expect' if '--unicode-char:false' in command else '.expect'
                    return SimpleNamespace(returncode=1, stdout=Path(str(fixture) + suffix).read_bytes(), stderr=b'')

                with patch.object(module.subprocess, 'run', side_effect=failed), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(module.controls(args), 1)

    def test_proof_outcomes_are_checked_even_when_summary_rows_agree(self):
        for module in comparisons():
            with self.subTest(module=module.__name__), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for group in ['suite-off', 'suite-on', 'std-off-5.1.0', 'std-on-5.1.0']:
                    for mode in ['legacy', 'default']:
                        out = root / (group + '-' + mode)
                        out.mkdir()
                        if group.startswith('suite'):
                            (out / 'verdicts.tsv').write_text('file.dfy\t4\t1 verified, 1 error\t10\t0.1\n')
                            outcome = 'Invalid' if group == 'suite-off' and mode == 'default' else 'Correct'
                            (out / 'proofs.json').write_text(json.dumps({
                                'declarations': {'file.dfy: F': outcome}, 'resource_units': 40,
                                'batches': {'file.dfy: F: 1': [outcome, 40]}}))
                        else:
                            (out / 'verdicts.tsv').write_text('Std F\tCorrect\t\t\t40\t1\n')
                args = SimpleNamespace(input=root, output=root / 'report')
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(module.compare(args), 1)
                report = json.loads((root / 'report/report.json').read_text())
                self.assertEqual(report['suite-off']['row_changes'], {})
                self.assertEqual(len(report['suite-off']['declaration_changes']), 1)
                for out in root.glob('suite-*-default'):
                    (out / 'proofs.json').write_text(json.dumps({
                        'declarations': {'file.dfy: F': 'Correct'}, 'resource_units': 100,
                        'batches': {'file.dfy: F: 1': ['Correct', 100]}}))
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(module.compare(args), 0)
                report = json.loads((root / 'report/report.json').read_text())
                self.assertEqual(report['suite-off']['delta_ru'], 60)
                self.assertEqual(report['suite-off']['changed_resource_rows'], 1)


if __name__ == '__main__':
    unittest.main()
