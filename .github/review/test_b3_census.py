import copy
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import Mock

REVIEW = Path(__file__).resolve().parent
ROOT = REVIEW.parents[1]
spec = importlib.util.spec_from_file_location('b3_census', REVIEW / 'b3-census.py')
census = importlib.util.module_from_spec(spec)
spec.loader.exec_module(census)


class B3CensusTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = json.loads((REVIEW / 'b3-corpus.json').read_text())
        cls.report = json.loads((ROOT / 'docs/dev/b3/inventory.json').read_text())
        cls.lock = json.loads((ROOT / 'docs/dev/b3/dependencies.json').read_text())

    def test_snapshot_covers_planned_strata_and_features(self):
        census.validate_report(self.report, self.config)
        self.assertEqual({'prelude', 'standard-library', 'soundness-regression'},
                         {c['stratum'] for c in self.report['cases']})
        self.assertEqual(self.lock['dafny']['commit'], self.report['source_commit'])
        self.assertEqual(census.sha(REVIEW / 'b3-corpus.json'), self.report['corpus_sha256'])
        for path, digest in self.report['generator_sha256'].items():
            self.assertEqual(census.sha(ROOT / path), digest, path)

    def reject(self, mutate):
        report = copy.deepcopy(self.report)
        mutate(report)
        with self.assertRaises(ValueError):
            census.validate_report(report, self.config)

    def test_source_hashes_ignore_checkout_line_endings(self):
        lf = Mock(read_bytes=lambda: b'line one\nline two\n')
        crlf = Mock(read_bytes=lambda: b'line one\r\nline two\r\n')
        self.assertEqual(census.sha(lf), census.sha(crlf))

    def test_missing_case_is_rejected(self):
        self.reject(lambda r: r['cases'].pop())

    def test_changed_roots_are_rejected(self):
        self.reject(lambda r: r['cases'][0].update(roots=['different.bpl']))

    def test_empty_and_duplicate_module_denominators_are_rejected(self):
        self.reject(lambda r: r['cases'][0].update(modules=[]))
        self.reject(lambda r: r['cases'][0].update(modules=['Prelude', 'Prelude']))

    def test_bad_counts_are_rejected(self):
        for count in [0, -1, True, '1']:
            self.reject(lambda r: r['cases'][0]['feature_counts'].update({'node.Axiom': count}))

    def test_missing_identity_is_rejected(self):
        self.reject(lambda r: r['cases'][0].update(input_sha256={}))

    def test_missing_control_form_is_rejected(self):
        def remove(report):
            for row in report['cases']:
                row['feature_counts'].pop('control.WhileCmd', None)
        self.reject(remove)

    def test_dependency_chain_has_complete_package_identity(self):
        boogie = self.lock['boogie']
        self.assertEqual(boogie['tag_commit'], boogie['nuspec_repository_commit'])
        self.assertEqual(14, len(boogie['packages']))
        self.assertEqual(14, len({p['name'] for p in boogie['packages']}))
        for package in boogie['packages']:
            self.assertRegex(package['sha256'], r'^[a-f0-9]{64}$')
            self.assertIn(boogie['package_version'], package['name'])
        self.assertFalse(self.lock['b3']['successor_selected'])
        self.assertFalse(self.lock['b3']['source_edits'])


if __name__ == '__main__':
    unittest.main()
