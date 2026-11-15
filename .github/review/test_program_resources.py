import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('resources', Path(__file__).with_name('program-resources.py'))
resources = importlib.util.module_from_spec(spec)
spec.loader.exec_module(resources)


class ResourceTests(unittest.TestCase):
    def test_cli_limits_use_exact_units_and_last_option(self):
        self.assertEqual(16000000, resources.option_limit(['--resource-limit', '16e6']))
        self.assertEqual(1200, resources.option_limit(['--resource-limit=16e6', '--resource-limit:1200']))
        with self.assertRaises(ValueError):
            resources.option_limit([])

    def test_limit_overrides_multipliers_and_unlimited(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'test.dfy'
            path.write_text('lemma {:resource_limit "6e6"} A() {}\n'
                            'lemma {:timeLimitMultiplier 100} B() {}\n'
                            'lemma {:rlimit 12} C() {}\n'
                            'lemma {:resource_limit 0} D() {}\n'
                            '// lemma {:resource_limit 1} Fake() {}\n')
            values = resources.overrides([path])
            for name, limit in [('A', 6000000), ('B', 1000000000), ('C', 12000), ('D', 0), ('Fake', 10000000)]:
                self.assertEqual(limit, resources.limit_for('M.' + name + ' (correctness)', 10000000, values))

    def test_batch_headroom_does_not_use_total_program_cost(self):
        with tempfile.TemporaryDirectory() as tmp:
            log = Path(tmp) / 'results.json'
            log.write_text(json.dumps({'verificationResults': [{'name': 'M', 'vcResults': [
                {'vcNum': 1, 'outcome': 'Valid', 'resourceCount': 600},
                {'vcNum': 2, 'outcome': 'Valid', 'resourceCount': 600}]}]}))
            row, batches = resources.record('program', log, 1000, {})
            self.assertEqual(1200, row['total_ru'])
            self.assertEqual(600, row['max_batch_ru'])
            self.assertFalse(any(batch['near_limit'] for batch in batches))

    def test_threshold_includes_exact_boundary_and_overrun(self):
        with tempfile.TemporaryDirectory() as tmp:
            log = Path(tmp) / 'results.json'
            log.write_text(json.dumps({'verificationResults': [{'name': 'M', 'vcResults': [
                {'vcNum': n, 'outcome': 'Valid', 'resourceCount': ru}
                for n, ru in enumerate([749, 750, 1251])]}]}))
            _, batches = resources.record('program', log, 1000, {})
            self.assertEqual([False, True, True], [b['near_limit'] for b in batches])
            _, unlimited = resources.record('program', log, 0, {})
            self.assertFalse(any(b['near_limit'] for b in unlimited))

    def test_diagnostic_fixture_needs_no_source_or_valid_limit(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); directory = root / 'measurements' / 'missing-include.dfy'
            directory.mkdir(parents=True)
            (directory / 'command.json').write_text(json.dumps(['dafny', 'verify', '--resource-limit', 'invalid']))
            verdicts = root / 'verdicts.tsv'; verdicts.write_text('missing-include.dfy\t2\t\t1\t0\n')
            programs, batches = resources.collect('suite', root / 'measurements', root / 'no-source', verdicts)
            self.assertTrue(programs[0]['unavailable'])
            self.assertEqual([], batches)
            verdicts.write_text('different.dfy\t2\t\t1\t0\n')
            with self.assertRaisesRegex(ValueError, 'coverage'):
                resources.collect('suite', root / 'measurements', root, verdicts)

    def test_unsupported_limit_syntax_is_not_silently_guessed(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / 'source.dfy'
            source.write_text('@ResourceLimit(variable)\nlemma A() {}')
            with self.assertRaises(ValueError):
                resources.overrides([source])

    def test_modern_limits_are_qualified_by_module_and_trait(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'source.dfy'
            path.write_text('module Std.Test { trait A { @ResourceLimit("2e6") method Parse() {} }\n'
                            'trait B { @ResourceLimit("3e6") method Parse() {} } }')
            values = resources.overrides([path])
            self.assertEqual(2000000, resources.limit_for('Std.Test.A.Parse (correctness)', 100, values))
            self.assertEqual(3000000, resources.limit_for('Std.Test.B.Parse (correctness)', 100, values))

    def test_datatype_scope_and_character_braces(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'source.dfy'
            path.write_text("module Std.Test { const c := '}'; datatype D = D() { "
                            '@ResourceLimit("0") lemma L() {} } }')
            values = resources.overrides([path])
            self.assertEqual(0, resources.limit_for('Std.Test.D.L (correctness)', 100, values))

    def test_nonliteral_legacy_limit_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'source.dfy'
            path.write_text('lemma {:resource_limit namedConstant} L() {}')
            with self.assertRaises(ValueError):
                resources.overrides([path])


if __name__ == '__main__':
    unittest.main()
