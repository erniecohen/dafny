import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('filters', Path(__file__).with_name('regression-filter.py'))
filters = importlib.util.module_from_spec(spec)
spec.loader.exec_module(filters)


class RegressionPartitionTests(unittest.TestCase):
    def fragment(self, root, name, text=None):
        path = root / 'regressions.d' / (name + '.txt')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text if text is not None else name + '\n')
        return path

    def test_complete_disjoint_partition(self):
        result = filters.partition(Path(__file__).parent)
        self.assertEqual(set(result['portable']) | set(result['linux']), set(result['all']))
        self.assertFalse(set(result['portable']) & set(result['linux']))
        self.assertEqual(len(result['all']), len(set(result['all'])))
        self.assertEqual(result['linux'], ['dafny0/CoinductiveProofs.dfy'])

    def test_fragments_are_sorted_and_allow_comments(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fragment(root, 'z/b.dfy', '# Explanation\n\nz/b.dfy\n')
            self.fragment(root, 'a.dfy')
            (root / 'regressions-linux').write_text('z/b.dfy\n')
            self.assertEqual(filters.partition(root), {
                'all': ['a.dfy', 'z/b.dfy'], 'portable': ['a.dfy'], 'linux': ['z/b.dfy']})

    def test_unknown_linux_entry_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.fragment(root, 'a.dfy')
            (root / 'regressions-linux').write_text('b.dfy\n')
            with self.assertRaisesRegex(ValueError, 'not in the standing manifest'):
                filters.partition(root)

    def test_fragment_must_contain_exactly_its_own_test(self):
        for text in ['', '# comment only\n', 'b.dfy\n', 'a.dfy\nb.dfy\n', 'a.dfy\na.dfy\n']:
            with self.subTest(text=text), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                self.fragment(root, 'a.dfy', text)
                with self.assertRaises(ValueError):
                    filters.partition(root)

    def test_missing_manifest_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(ValueError, 'Empty regression manifest'):
                filters.partition(Path(tmp))

    def test_old_manifest_and_readers_are_absent(self):
        review = Path(__file__).resolve().parent
        self.assertFalse((review / 'regressions').exists(), 'Use one fragment per test')
        # Check workflow shell readers and Python/shell path literals, including
        # relative Python paths. Job names and regressions-linux are unrelated.
        old_path = r'\.github/review/regressions(?![\w.-])'
        relative_path = r'''(?:/\s*|(?:Path|open|with_name|joinpath)\(\s*)['"]regressions['"]'''
        readers = [*review.rglob('*.py'), *review.rglob('*.sh'),
                   *review.with_name('workflows').glob('*.yml'),
                   *review.with_name('workflows').glob('*.yaml')]
        for path in readers:
            if path.resolve() == Path(__file__).resolve():
                continue
            with self.subTest(reader=path.name):
                self.assertNotRegex(path.read_text(), old_path + '|' + relative_path)

    def test_workflows_use_the_shared_filter(self):
        workflows = Path(__file__).resolve().parents[1] / 'workflows'
        for name, partition in [('review.yml', 'portable'), ('review.yml', 'linux'),
                                ('platform-parity.yml', 'all')]:
            with self.subTest(workflow=name, partition=partition):
                self.assertIn('filter=$(python3 .github/review/regression-filter.py ' + partition + ')',
                              (workflows / name).read_text())


if __name__ == '__main__':
    unittest.main()
