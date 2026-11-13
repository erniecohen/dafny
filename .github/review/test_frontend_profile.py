"""Focused regressions for the profile oracle and its real resolution boundaries."""
import argparse
import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('frontend', Path(__file__).with_name('frontend-profile.py'))
frontend = importlib.util.module_from_spec(spec)
spec.loader.exec_module(frontend)


class IdentityTests(unittest.TestCase):
    def run_driver(self, changed=None, configs='ws-default-off,ws-default-on'):
        calls = {}

        def child(args, env, cwd):
            config = 'on' if '--pipeline-profile' in args else 'off'
            rep = calls.get(config, 0)
            calls[config] = rep + 1
            # Warm-ups agree; only a later measured round can change.
            data, code = (f'round {rep}'.encode(), 0)
            if changed and config == 'on' and rep == 2:
                data, code = changed
            return {'exit': code, 'wall_s': 0.01, 'user_s': 0.0, 'sys_s': 0.0, 'maxrss_mb': 1.0}, data

        with tempfile.TemporaryDirectory() as tmp:
            args = argparse.Namespace(out=tmp, configs=configs, reps=2, seed=1, corpus='test',
                                      dafny='unused', command='resolve', cores=2)
            with patch.object(frontend, 'machine', return_value={}), \
                    patch.object(frontend, 'corpus_programs', return_value=[('test.dfy', [], ROOT)]), \
                    patch.object(frontend, 'run_one', side_effect=child), contextlib.redirect_stdout(io.StringIO()):
                result = frontend.cmd_run(args)
            report = (Path(tmp) / 'identity-test.txt').read_text()
        return result, report, calls

    def test_equal_warmups_do_not_hide_measured_output_change(self):
        result, report, calls = self.run_driver((b'changed', 0))
        self.assertEqual(1, result)
        self.assertIn('round 2', report)
        self.assertEqual({'off': 3, 'on': 3}, calls)

    def test_equal_outputs_do_not_hide_measured_exit_change(self):
        result, report, _ = self.run_driver((b'round 2', 4))
        self.assertEqual(1, result)
        self.assertIn('exit code differs', report)

    def test_matching_rounds_can_differ_from_other_rounds(self):
        result, report, _ = self.run_driver()
        self.assertEqual(0, result)
        self.assertIn('every warm-up and measured round', report)

    def test_profile_without_off_fails(self):
        result, report, _ = self.run_driver(configs='ws-default-on')
        self.assertEqual(1, result)
        self.assertIn('missing matching ws-default-off observation', report)

    def test_each_required_observation_is_checked(self):
        configs = ['ws-default-off', 'ws-default-on', 'ws-default-gcev']
        rows = {('p', c, r): ('digest', 0) for c in configs for r in range(3)}
        self.assertEqual([], frontend.identity_errors(rows, ['p'], configs, 2))
        for key in rows:
            with self.subTest(missing=key):
                incomplete = dict(rows)
                del incomplete[key]
                self.assertTrue(frontend.identity_errors(incomplete, ['p'], configs, 2))
        rows[('p', 'ws-default-gcev', 1)] = ('different', 0)
        self.assertTrue(frontend.identity_errors(rows, ['p'], configs, 2))


class PhaseBoundaryTests(unittest.TestCase):
    def check_events(self, phases):
        events = [{'ev': 'fe', 'stage': 'resolve.module', 'module': 'Exported',
                   'phases': [{'phase': p} for p in phases]}]
        with patch.object(frontend, 'load_events', return_value=events), contextlib.redirect_stderr(io.StringIO()):
            return frontend.cmd_check_phases(argparse.Namespace(profile='unused'))

    def test_runtime_phase_order_and_export_view_are_required(self):
        primary = ['resolveSetup', 'signature', 'moduleAttributes', 'body', 'accessibleMembers']
        export = ['exportViewSetup', 'exportSignature', 'exportModuleAttributes', 'exportBody',
                  'exportAccessibleMembers']
        self.assertEqual(0, self.check_events(primary + export))
        self.assertEqual(1, self.check_events(primary))
        self.assertEqual(1, self.check_events(export))
        misplaced = primary[:]
        misplaced[1], misplaced[3] = misplaced[3], misplaced[1]
        self.assertEqual(1, self.check_events(misplaced + export))

    def test_signature_and_body_marks_bracket_actual_calls(self):
        source = (ROOT / 'Source/DafnyCore/AST/Modules/ModuleDefinition.cs').read_text()
        # Require adjacent boundaries at the calls, not just phase names somewhere in the file.
        self.assertRegex(source, r'PipelineProfile.ModulePhase\(resolver.Options, exportSetName == null '
                         r'\? "resolveSetup" : "exportViewSetup"\);\s*'
                         r'resolver.ResolveTopLevelDecls_Signatures\([^;]+;\s*'
                         r'PipelineProfile.ModulePhase\(resolver.Options, exportSetName == null '
                         r'\? "signature" : "exportSignature"\);')
        self.assertRegex(source, r'resolver.ResolveModuleAttributes\(this, allDeclarations\);\s*'
                         r'PipelineProfile.ModulePhase\(resolver.Options, exportSetName == null '
                         r'\? "moduleAttributes" : "exportModuleAttributes"\);\s*}\s*'
                         r'if \(resolver.reporter.Count\(ErrorLevel.Error\) == prevErrorCount\) {\s*'
                         r'resolver.ResolveTopLevelDecls_Core\([^;]+;\s*'
                         r'PipelineProfile.ModulePhase\(resolver.Options, exportSetName == null '
                         r'\? "body" : "exportBody"\);\s*}')
        self.assertRegex(source, r'PipelineProfile.ModulePhase\(resolver.Options, exportSetName == null '
                         r'\? "accessibleMembers" : "exportAccessibleMembers"\);\s*return true;')

    def test_registration_is_closed_before_resolve(self):
        source = (ROOT / 'Source/DafnyCore/AST/Modules/LiteralModuleDecl.cs').read_text()
        self.assertRegex(source, r'resolver.ResolveModuleExport\(this, sig\);\s*'
                         r'PipelineProfile.ModulePhase\(resolver.Options, "registrationAndExports"\);\s*'
                         r'var good = module.Resolve\(sig, resolver\);')
        self.assertNotRegex(source, r'ModulePhase\(resolver.Options, "(?:signature|body)"\)')

    def test_export_view_reaches_distinct_phase_boundaries(self):
        source = (ROOT / 'Source/DafnyCore/Resolver/ModuleResolver.cs').read_text()
        self.assertRegex(source, r'exportView.Resolve\(testSig, this, exportDecl.Name\)')
        profile = (ROOT / 'Source/DafnyCore/Pipeline/PipelineProfile.cs').read_text()
        method = profile[profile.index('public static void ModulePhase('):]
        # OFF exits before any counter sampling or phase-event allocation.
        self.assertLess(method.index('if (modulePhases == null)'), method.index('profile.BeginThread()'))
        self.assertLess(method.index('if (profile == null)'), method.index('new Dictionary'))


if __name__ == '__main__':
    unittest.main()
