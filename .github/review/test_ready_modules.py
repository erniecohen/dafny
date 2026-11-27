import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


spec = importlib.util.spec_from_file_location('ready_modules', Path(__file__).with_name('ready-modules.py'))
ready = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ready)


class ReadyModuleOracleTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.path = Path(self.directory.name) / 'log.json'
        self.rows = [{'name': name, 'vcResults': [
            {'vcNum': i, 'outcome': outcome, 'resourceCount': 10 + i,
             'assertions': [{'filename': 'control.dfy', 'line': i, 'col': 1, 'description': 'assertion'}]}
            for i in numbers]}
            for name, outcome, numbers in [('Good', 'Valid', [1, 2]), ('Bad', 'Invalid', [1])]]

    def observe(self, rows):
        self.path.write_text(json.dumps({'verificationResults': rows}))
        return ready.observations(self.path)

    def test_records_every_batch_and_its_evidence(self):
        result = self.observe(self.rows)
        self.assertEqual({('Good', 1), ('Good', 2), ('Bad', 1)}, set(result))
        self.assertEqual(('Invalid', 11, json.dumps(self.rows[1]['vcResults'][0]['assertions'], sort_keys=True)),
                         result[('Bad', 1)])
        ready.require_equal(result, result, 'positive equality control')

    def test_changed_assertions_resources_and_outcomes_are_observable(self):
        baseline = self.observe(self.rows)
        for field, value in [('resourceCount', 99), ('outcome', 'Invalid'), ('assertions', [])]:
            with self.subTest(field=field):
                changed = copy.deepcopy(self.rows)
                changed[0]['vcResults'][0][field] = value
                with self.assertRaises(AssertionError):
                    ready.require_equal(baseline, self.observe(changed), field)

    def test_requires_a_positive_proof(self):
        with self.assertRaisesRegex(AssertionError, 'positive proof absent'):
            self.observe(self.rows[1:])

    def test_requires_a_failing_false_assertion(self):
        changed = copy.deepcopy(self.rows)
        changed[1]['vcResults'][0]['outcome'] = 'Valid'
        with self.assertRaisesRegex(AssertionError, 'false assertion did not fail'):
            self.observe(changed)

    def test_rejects_empty_evidence(self):
        with self.assertRaisesRegex(AssertionError, 'no VCs exercised'):
            self.observe([])

    def test_rejects_duplicate_batches(self):
        with self.assertRaisesRegex(AssertionError, 'duplicate VC'):
            self.observe(self.rows + [self.rows[0]])
