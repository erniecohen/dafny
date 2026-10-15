"""Measurement controls only; no verifier or solver subprocesses."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('comparison', Path(__file__).with_name('default-comparison.py'))
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class ComparisonControls(unittest.TestCase):
    def fixture(self, directory):
        measurements = directory / 'measurements'
        measurements.mkdir()
        rows = []
        for part in ['Std', 'notarget', 'cs', 'java', 'js', 'go', 'py']:
            (measurements / (part + '.invocation.json')).write_text(json.dumps({
                'exit': '4' if part == 'Std' else '0', 'verification_log_present': True}))
            (measurements / (part + '.verification.json')).write_text(json.dumps({'verificationResults': [{
                'name': 'Example', 'outcome': 'OutOfResource', 'resourceCount': 100, 'vcResults': [
                    {'vcNum': 1, 'outcome': 'OutOfResource', 'resourceCount': 100, 'assertions': []}]}]}))
            rows.append('run ' + part + '\t4\t0 verified, 1 out of resource\tfile(1,1)\t3.2')
        (directory / 'std-verdicts.tsv').write_text('\n'.join(rows) + '\n')
        return measurements

    def test_raw_cost_differences_remain_visible(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            measurements = self.fixture(root)
            before = comparison.vector(root)
            self.assertEqual({kind: len(value) for kind, value in before.items()}, {'runs': 7, 'declarations': 7, 'vcs': 7})
            file = measurements / 'Std.verification.json'
            log = json.loads(file.read_text())
            log['verificationResults'][0]['resourceCount'] += 1
            log['verificationResults'][0]['vcResults'][0]['resourceCount'] += 1
            file.write_text(json.dumps(log))
            after = comparison.vector(root)
            self.assertFalse(comparison.differences(before['runs'], after['runs']))
            self.assertEqual(len(comparison.differences(before['declarations'], after['declarations'])), 1)
            self.assertEqual(len(comparison.differences(before['vcs'], after['vcs'])), 1)

    def test_timeout_or_missing_part_is_incomplete(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            measurements = self.fixture(root)
            file = measurements / 'Std.invocation.json'
            file.write_text(json.dumps({'exit': 'TIMEOUT', 'verification_log_present': False}))
            with self.assertRaisesRegex(ValueError, 'Incomplete'):
                comparison.vector(root)
            file.unlink()
            with self.assertRaisesRegex(ValueError, 'all seven'):
                comparison.vector(root)

    def test_duplicate_vc_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            measurements = self.fixture(root)
            file = measurements / 'Std.verification.json'
            log = json.loads(file.read_text())
            log['verificationResults'][0]['vcResults'] *= 2
            file.write_text(json.dumps(log))
            with self.assertRaisesRegex(ValueError, 'Duplicate VC'):
                comparison.vector(root)


if __name__ == '__main__':
    unittest.main()
