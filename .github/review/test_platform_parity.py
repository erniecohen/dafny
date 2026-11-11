"""Negative controls for completeness and resource aggregation in platform parity."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('parity', Path(__file__).with_name('platform-parity.py'))
parity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parity)


class ParityTests(unittest.TestCase):
    def test_cost_change_without_verdict_change(self):
        result = parity.resource_report({'a': 17, 'b': 3}, {'a': 20, 'b': 1})
        self.assertEqual(result['differing'], 2)
        self.assertEqual(result['max_absolute_difference'], 3)

    def test_missing_cost_is_visible(self):
        result = parity.resource_report({'a': 17, 'b': None}, {'a': 17, 'b': 0})
        self.assertEqual(result['differing'], 1)
        self.assertEqual(result['unavailable_linux'], 1)

    def test_duplicate_verdicts_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'verdicts.tsv'
            path.write_text('a\t0\na\t0\n')
            with self.assertRaises(ValueError):
                parity.tsv(path)

    def make_suite(self, root):
        for i in range(4):
            folder = root / f'parity-linux-verdicts-off-{i}'
            folder.mkdir()
            (folder / 'plan.tsv').write_text(''.join(f'{j}.dfy\t\n' for j in range(4)))
            (folder / f'verdicts-off-{i}.tsv').write_text(f'{i}.dfy\t0\tverified\t\t1.0\n')
            (folder / f'resources-off-{i}.json').write_text(json.dumps({
                'programs': [f'{i}.dfy'], 'unavailable': {},
                'batches': {f'{i}.dfy\tmethod\t1': ['Valid', 17], f'{i}.dfy\tmethod\t2': ['Valid', 3]}}))

    def test_suite_aggregates_per_program(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.make_suite(root)
            verdicts, costs, _ = parity.suite(root, 'linux', 'off')
            self.assertEqual(len(verdicts), 4)
            self.assertEqual(costs, {f'{i}.dfy': 20 for i in range(4)})

    def test_missing_shard_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.make_suite(root)
            (root / 'parity-linux-verdicts-off-3' / 'verdicts-off-3.tsv').unlink()
            with self.assertRaises(FileNotFoundError):
                parity.suite(root, 'linux', 'off')

    def test_missing_program_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.make_suite(root)
            (root / 'parity-linux-verdicts-off-3' / 'verdicts-off-3.tsv').write_text('')
            with self.assertRaises(ValueError):
                parity.suite(root, 'linux', 'off')

    def test_missing_test_results_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(ValueError):
                parity.test_results(Path(tmp), 'macos', 'regressions', 10)


if __name__ == '__main__':
    unittest.main()
