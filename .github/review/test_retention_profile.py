"""Evidence-integrity controls for the opt-in F4-M comparison."""
import argparse
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('retention', Path(__file__).with_name('retention-profile.py'))
retention = importlib.util.module_from_spec(spec)
spec.loader.exec_module(retention)


def profile(release=False):
    events = [{'ev': 'manifest', 'heap': True},
              {'ev': 'heap', 'where': 'parsed', 'index': 1, 'liveBytes': 300},
              {'ev': 'heap', 'where': 'resolved', 'index': 2, 'liveBytes': 400}]
    for index, module in enumerate(('A', 'B'), 3):
        events.append({'ev': 'span', 'stage': 'translate', 'module': module})
        if release:
            events.append({'ev': 'release', 'module': module, 'bodies': 1})
        events.append({'ev': 'heap', 'where': 'module', 'module': module, 'index': index,
                       'liveBytes': 300 if release else 500})
    events.append({'ev': 'summary', 'tasksSubmitted': 2, 'tasksCompleted': 2, 'tasksUnfinished': 0,
                   'translations': 2, 'preparations': 2, 'peakWorkingSetBytes': 2000})
    return events


class RetentionTests(unittest.TestCase):
    def test_threshold_includes_equality_and_uses_final_point(self):
        result = retention.compare(profile(), profile(True))
        self.assertEqual('go', result['decision'])
        self.assertEqual(.1, result['points'][-1]['savedShareOfBaselinePeak'])
        self.assertEqual(2, result['releasedBodies'])
        changed = profile(True)
        changed[-2]['liveBytes'] = 501
        result = retention.compare(profile(), changed)
        self.assertEqual('no-go', result['decision'])
        self.assertEqual(-1, result['points'][-1]['savedBytes'])

    def test_external_rss_is_bytes_and_overrides_unavailable_runtime_peak(self):
        baseline = profile()
        baseline[-1]['peakWorkingSetBytes'] = 0
        with self.assertRaisesRegex(ValueError, 'peak RSS'):
            retention.compare(baseline, profile(True))
        result = retention.compare(baseline, profile(True), 4000)
        self.assertEqual(.05, result['points'][-1]['savedShareOfBaselinePeak'])
        self.assertEqual('no-go', result['decision'])

    def test_required_observations_cannot_be_omitted(self):
        events = profile(True)
        for index, event in enumerate(events):
            if event['ev'] == 'release':
                continue  # One remaining release still exercises body release.
            with self.subTest(index=index), self.assertRaises(ValueError):
                retention.compare(profile(), events[:index] + events[index + 1:])

    def test_unfinished_or_absent_tasks_are_rejected(self):
        for key, value in [('tasksSubmitted', 0), ('tasksCompleted', 1), ('tasksUnfinished', 1)]:
            events = profile(True)
            events[-1][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                retention.compare(profile(), events)

    def test_non_heap_profiles_and_invalid_sizes_are_rejected(self):
        for index, key, value in [(0, 'heap', False), (1, 'liveBytes', 0), (2, 'liveBytes', '400'),
                                  (1, 'index', 2), (8, 'module', 'A')]:
            events = profile(True)
            events[index][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                retention.compare(profile(), events)

    def test_release_must_be_exercised_only_in_candidate(self):
        with self.assertRaisesRegex(ValueError, 'vacuous'):
            retention.compare(profile(), profile())
        with self.assertRaisesRegex(ValueError, 'baseline'):
            retention.compare(profile(True), profile(True))
        events = profile(True)
        next(e for e in events if e['ev'] == 'release')['module'] = 'Other'
        with self.assertRaisesRegex(ValueError, 'unreported'):
            retention.compare(profile(), events)

    def test_same_coverage_different_reporting_order_is_rejected(self):
        events = profile(True)
        for e in events:
            if e['ev'] == 'heap' and e['where'] == 'module':
                e['module'] = 'B' if e['module'] == 'A' else 'A'
        with self.assertRaisesRegex(ValueError, 'order'):
            retention.compare(profile(), events)

    def test_preparation_coverage_must_match(self):
        events = profile(True)
        events[-1]['preparations'] = 1
        with self.assertRaisesRegex(ValueError, 'preparations'):
            retention.compare(profile(), events)

    def test_translation_identity_must_match_reported_modules(self):
        events = profile(True)
        next(e for e in events if e['ev'] == 'span')['module'] = 'Other'
        with self.assertRaisesRegex(ValueError, 'coverage'):
            retention.compare(profile(), events)

    def test_bad_evidence_never_writes_a_decision(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'before').write_text('{}\n')
            (root / 'after').write_text('{}\n')
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
                retention.main(['compare', str(root / 'before'), str(root / 'after'), '--out', str(root / 'result')])
            self.assertEqual(1, error.exception.code)
            self.assertFalse((root / 'result').exists())

    def test_solver_oracle_requires_obligations_outcomes_and_resources(self):
        def log(name='Positive', outcome='Valid', resources=100, assertions=None):
            return {'verificationResults': [{'name': name, 'vcResults': [
                {'vcNum': 1, 'assertions': assertions or [], 'outcome': outcome, 'resourceCount': resources}]}]}
        with tempfile.TemporaryDirectory() as tmp:
            baseline, released = Path(tmp) / 'baseline', Path(tmp) / 'released'
            baseline.write_text(json.dumps(log()))
            released.write_text(json.dumps(log()))
            retention.check_verification_logs(baseline, released)
            for changed in (log(name='Other'), log(outcome='Invalid'), log(resources=101),
                            log(assertions=[{'line': 2}])):
                released.write_text(json.dumps(changed))
                with self.assertRaisesRegex(ValueError, 'per-VC'):
                    retention.check_verification_logs(baseline, released)
            for empty in ({'verificationResults': []}, log(resources=0)):
                baseline.write_text(json.dumps(empty))
                released.write_text(json.dumps(empty))
                with self.assertRaisesRegex(ValueError, 'no solver'):
                    retention.check_verification_logs(baseline, released)

    def test_runner_checks_outputs_boogie_and_exit(self):
        for mutation in (None, 'output', 'boogie', 'exit', 'both-exit', 'missing', 'release'):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as tmp:
                args = argparse.Namespace(out=str(Path(tmp) / 'out'), reps=1, windows=[1], cores=2,
                                          dafny='unused', project='unused')
                frontend = retention.sibling('frontend-profile')
                translation = retention.sibling('translation-workers')
                def child(cmd, env, cwd):
                    release = '--release-verified-modules' in cmd
                    path = Path(cmd[cmd.index('--pipeline-profile') + 1])
                    events = profile(release and mutation != 'release')
                    if mutation == 'missing' and release:
                        events.pop()
                    path.write_text(''.join(json.dumps(e) + '\n' for e in events))
                    boogie = path.parent / 'program_A.bpl'
                    boogie.write_bytes(b'changed' if release and mutation == 'boogie' else b'program')
                    (path.parent / 'program_B.bpl').write_bytes(b'program')
                    return {'exit': 1 if mutation == 'both-exit' or release and mutation == 'exit' else 4, 'maxrss_mb': 1}, \
                        b'changed' if release and mutation == 'output' else b'output'
                with patch.object(retention, 'sibling', side_effect=[frontend, translation]), \
                        patch.object(frontend, 'run_one', side_effect=child), contextlib.redirect_stdout(io.StringIO()):
                    if mutation:
                        with self.assertRaises(ValueError):
                            retention.run(args)
                    else:
                        self.assertEqual(0, retention.run(args))
                decisions = list(Path(args.out).rglob('comparison.json'))
                self.assertEqual(0 if mutation else 1, len(decisions))


if __name__ == '__main__':
    unittest.main()
