"""The runtime/identity oracle must reject inert and vacuous comparisons."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('batch_gc', Path(__file__).with_name('batch-gc.py'))
batch_gc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch_gc)


def profile():
    return [{'ev': 'manifest', 'gcLatencyMode': 'Batch'},
            {'ev': 'task', 'outcome': 'Valid'}, {'ev': 'task', 'outcome': 'Invalid'},
            {'ev': 'summary', 'tasksSubmitted': 2, 'tasksCompleted': 2,
             'tasksUnfinished': 0, 'tasksCached': 0}]


def document():
    return {'verificationResults': [{'name': 'M.Control', 'vcResults': [
        {'vcNum': 1, 'outcome': 'Valid', 'resourceCount': 10, 'assertions': ['positive']},
        {'vcNum': 2, 'outcome': 'Invalid', 'resourceCount': 20, 'assertions': ['false']}]}]}


class BatchGcTests(unittest.TestCase):
    def test_nonempty_exercised_equal_control(self):
        self.assertEqual(2, batch_gc.check_profile(profile(), 'Batch'))
        batch_gc.compare(batch_gc.records(document()), batch_gc.records(copy.deepcopy(document())))

    def test_inert_activation_fails(self):
        with self.assertRaisesRegex(AssertionError, 'not applied'):
            batch_gc.check_profile(profile(), 'Interactive')

    def test_missing_or_duplicate_manifest_fails(self):
        for events in [profile()[1:], [profile()[0]] + profile()]:
            with self.subTest(events=events), self.assertRaisesRegex(AssertionError, 'manifest'):
                batch_gc.check_profile(events, 'Batch')

    def test_missing_or_duplicate_completion_fails(self):
        for events in [profile()[:-1], profile() + [profile()[-1]]]:
            with self.subTest(events=events), self.assertRaisesRegex(AssertionError, 'summary'):
                batch_gc.check_profile(events, 'Batch')

    def test_empty_verification_fails(self):
        events = [profile()[0], {**profile()[-1], 'tasksSubmitted': 0, 'tasksCompleted': 0}]
        with self.assertRaisesRegex(AssertionError, 'empty'):
            batch_gc.check_profile(events, 'Batch')

    def test_unfinished_or_cached_verification_fails(self):
        for key in ['tasksSubmitted', 'tasksCompleted', 'tasksUnfinished', 'tasksCached']:
            events = profile()
            events[-1][key] += 1
            with self.subTest(key=key), self.assertRaises(AssertionError):
                batch_gc.check_profile(events, 'Batch')

    def test_positive_and_negative_proofs_are_required(self):
        for outcome in ['Valid', 'Invalid']:
            events = profile()
            events[1]['outcome'] = events[2]['outcome'] = outcome
            with self.subTest(outcome=outcome), self.assertRaises(AssertionError):
                batch_gc.check_profile(events, 'Batch')
            log = document()
            for vc in log['verificationResults'][0]['vcResults']:
                vc['outcome'] = outcome
            with self.subTest(outcome=outcome), self.assertRaises(AssertionError):
                batch_gc.records(log)

    def test_duplicate_vc_fails(self):
        log = document()
        log['verificationResults'][0]['vcResults'][1]['vcNum'] = 1
        with self.assertRaisesRegex(AssertionError, 'duplicate'):
            batch_gc.records(log)

    def test_changed_membership_fails(self):
        a = batch_gc.records(document())
        b = copy.deepcopy(a)
        del b[('M.Control', 1)]
        with self.assertRaisesRegex(AssertionError, 'membership'):
            batch_gc.compare(a, b)

    def test_changed_verdict_resources_or_assertions_fail(self):
        a = batch_gc.records(document())
        for index, value in enumerate(['Undetermined', 11, ['changed']]):
            b = copy.deepcopy(a)
            values = list(b[('M.Control', 1)])
            values[index] = value
            b[('M.Control', 1)] = tuple(values)
            with self.subTest(index=index), self.assertRaisesRegex(AssertionError, 'changed'):
                batch_gc.compare(a, b)

    def test_empty_solver_log_fails(self):
        with self.assertRaisesRegex(AssertionError, 'empty'):
            batch_gc.records({'verificationResults': []})

    def test_no_solver_work_fails(self):
        for key, value in [('resourceCount', 0), ('assertions', [])]:
            log = document()
            for vc in log['verificationResults'][0]['vcResults']:
                vc[key] = value
            with self.subTest(key=key), self.assertRaisesRegex(AssertionError, 'solver work'):
                batch_gc.records(log)
