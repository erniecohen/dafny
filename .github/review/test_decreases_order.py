"""Checks for the bookkeeping used to approve decreases-order baseline changes."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

spec = importlib.util.spec_from_file_location('decreases_order', Path(__file__).with_name('decreases-order.py'))
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class DecreasesOrderComparisonTests(unittest.TestCase):
    def test_outcomes_and_resources_are_independent(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for group in ['suite-off', 'suite-on', 'std-off-5.1.0', 'std-on-5.1.0', 'std-off-4.12.1', 'std-on-4.12.1']:
                for mode, outcome, ru in [('legacy', 'Correct', 40), ('default', 'Errors', 25)]:
                    (root / (group + '-' + mode + '.log')).write_text('recording output\n')
                    part = root / (group + '-' + mode)
                    part.mkdir()
                    if group.startswith('suite'):
                        (part / 'verdicts.tsv').write_text(f'file.dfy\t{0 if mode == "legacy" else 4}\t1 verified\t\t0.1\n')
                        (part / 'proofs.json').write_text(json.dumps({'declarations': {'file.dfy: F': outcome}, 'resource_units': ru}))
                    else:
                        (part / 'verdicts.tsv').write_text(f'Std F\t{outcome}\t\t\t{ru}\t1\n')
            with contextlib.redirect_stdout(io.StringIO()):
                comparison.compare(SimpleNamespace(input=root, output=root / 'report'))
            report = json.loads((root / 'report/report.json').read_text())
            for group, result in report.items():
                self.assertEqual(result['delta_ru'], -15)
                self.assertEqual(len(result['declaration_changes']), 1)
                self.assertEqual(len(result['row_changes']), 1)
            self.assertEqual((root / 'report/expected-std-verdicts-z3-5.1.0.tsv').read_text(), 'Std F\tErrors\t\t\n')

    def test_missing_cohort_is_an_error(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, 'missing measurements'):
                comparison.compare(SimpleNamespace(input=directory, output=Path(directory) / 'report'))

    def test_inventory_counts_every_vc_and_retains_unavailable_logs(self):
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            program = out / 'measurements/file.dfy'
            program.mkdir(parents=True)
            (program / 'command.json').write_text('[]')
            (program / 'results.json').write_text(json.dumps({'verificationResults': [{
                'name': 'F', 'outcome': 'Correct', 'vcResults': [
                    {'vcNum': 1, 'outcome': 'Valid', 'resourceCount': 13},
                    {'vcNum': 2, 'outcome': 'Valid', 'resourceCount': 17}]}]}))
            no_log = out / 'measurements/invalid.dfy'
            no_log.mkdir()
            (no_log / 'command.json').write_text('[]')
            comparison.inventory(out)
            result = json.loads((out / 'proofs.json').read_text())
            self.assertEqual(result['resource_units'], 30)
            self.assertEqual(len(result['batches']), 2)
            self.assertEqual(result['no_verification_log'], ['invalid.dfy'])


if __name__ == '__main__':
    unittest.main()
