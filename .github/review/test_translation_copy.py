import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('translation_copy', Path(__file__).with_name('translation-copy.py'))
copy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(copy)


class TranslationCopyControls(unittest.TestCase):
    def check(self, results):
        with tempfile.TemporaryDirectory() as directory:
            (Path(directory) / 'log.json').write_text(json.dumps({'verificationResults': results}))
            copy.controls(directory)

    @staticmethod
    def results(good='Valid', bad='Invalid', resources=13):
        return [{'name': name, 'vcResults': [{'outcome': outcome, 'resourceCount': resources}]} for
                name, outcome in [('B.Good', good), ('B.Bad', bad)]]

    def test_positive_and_false_assertion(self):
        self.check(self.results())

    def test_missing_or_failing_positive_is_rejected(self):
        for results in [self.results(good='Invalid'), self.results()[1:]]:
            with self.assertRaises(AssertionError):
                self.check(results)

    def test_false_assertion_must_fail(self):
        with self.assertRaises(AssertionError):
            self.check(self.results(bad='Valid'))

    def test_empty_or_solver_free_comparisons_are_rejected(self):
        for results in [[], self.results(resources=0)]:
            with self.assertRaises(AssertionError):
                self.check(results)
