"""Bounded collector controls reject inert, unbounded and vacuous observations."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('server_gc', Path(__file__).with_name('server-gc.py'))
server_gc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(server_gc)


def profile():
    return [{'ev': 'manifest', 'gcLatencyMode': 'Batch', 'serverGC': True},
            {'ev': 'verification-gc', 'heapCount': 1, 'maxHeapCount': 2, 'heapHardLimitBytes': 1024 ** 3},
            {'ev': 'task', 'outcome': 'Valid'}, {'ev': 'task', 'outcome': 'Invalid'},
            {'ev': 'summary', 'tasksSubmitted': 2, 'tasksCompleted': 2,
             'tasksUnfinished': 0, 'tasksCached': 0}]


class ServerGcTests(unittest.TestCase):
    def test_nonempty_bounded_observation(self):
        self.assertEqual(2, server_gc.check_profile(profile(), True))
        off = profile()
        del off[1]
        self.assertEqual(2, server_gc.check_profile(off, False))

    def test_inert_activation(self):
        events = profile()
        events[0]['serverGC'] = False
        with self.assertRaisesRegex(AssertionError, 'not activated'):
            server_gc.check_profile(events, True)

    def test_missing_duplicate_or_unrequested_policy(self):
        for events, bounded in [(profile()[:1] + profile()[2:], True),
                                (profile() + [profile()[1]], True), (profile(), False)]:
            with self.subTest(bounded=bounded), self.assertRaises(AssertionError):
                server_gc.check_profile(events, bounded)

    def test_unbounded_or_incorrect_runtime_configuration(self):
        for key, value in [('heapCount', 0), ('heapCount', 3), ('maxHeapCount', 3),
                           ('maxHeapCount', 0), ('heapHardLimitBytes', 0), ('heapHardLimitBytes', 1024 ** 4)]:
            events = profile()
            events[1][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(AssertionError):
                server_gc.check_profile(events, True)

    def test_empty_or_one_sided_verification(self):
        for outcome in ['Valid', 'Invalid']:
            events = profile()
            events[2]['outcome'] = events[3]['outcome'] = outcome
            with self.subTest(outcome=outcome), self.assertRaises(AssertionError):
                server_gc.check_profile(events, True)
        events = [profile()[0], profile()[1], {**profile()[-1], 'tasksSubmitted': 0, 'tasksCompleted': 0}]
        with self.assertRaises(AssertionError):
            server_gc.check_profile(events, True)


if __name__ == '__main__':
    unittest.main()
