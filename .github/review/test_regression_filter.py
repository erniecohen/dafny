import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('filters', Path(__file__).with_name('regression-filter.py'))
filters = importlib.util.module_from_spec(spec)
spec.loader.exec_module(filters)


class RegressionPartitionTests(unittest.TestCase):
    def test_complete_disjoint_partition(self):
        result = filters.partition(Path(__file__).parent)
        self.assertEqual(set(result['portable']) | set(result['linux']), set(result['all']))
        self.assertFalse(set(result['portable']) & set(result['linux']))
        self.assertEqual(result['linux'], ['dafny0/CoinductiveProofs.dfy'])

    def test_unknown_linux_entry_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'regressions').write_text('a.dfy\n')
            (root / 'regressions-linux').write_text('b.dfy\n')
            with self.assertRaises(ValueError):
                filters.partition(root)


if __name__ == '__main__':
    unittest.main()
