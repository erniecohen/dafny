"""Infrastructure checks: missing rows, unsafe probes and resource-only changes."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('axioms', Path(__file__).with_name('additional-axioms.py'))
axioms = importlib.util.module_from_spec(spec)
spec.loader.exec_module(axioms)


class AxiomGates(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.expected = self.root / 'expected.tsv'
        self.actual = self.root / 'actual.tsv'
        self.output = self.root / 'comparison'
        self.expected.write_text('a.dfy\t0\t1 verified, 0 errors\t\n')
        self.actual.write_text('a.dfy\t0\t1 verified, 0 errors\t\t2.0\n')

    def gate(self, mode='on', kind='suite'):
        return axioms.gate(kind, self.expected, self.actual, mode, self.output)

    def test_missing_and_changed_programs_fail(self):
        self.assertEqual(0, self.gate())
        self.actual.write_text('')
        self.assertEqual(1, self.gate())
        self.actual.write_text('a.dfy\t4\t0 verified, 1 error\t5\t1.0\n')
        self.assertEqual(1, self.gate())
        self.actual.write_text('a.dfy\tTIMEOUT\t\t\t900\n')
        self.assertEqual(1, self.gate())

    def test_missing_expected_fails_except_scratch_probe(self):
        self.expected.unlink()
        self.assertEqual(1, self.gate())
        with patch.dict(os.environ, {'PROBE': 'true', 'GITHUB_REF': 'refs/heads/scratch/test'}):
            self.assertEqual(0, self.gate())
            self.assertEqual(1, self.gate('off'))
            self.assertTrue(json.loads((self.output / 'report.json').read_text())['differences'])
        with patch.dict(os.environ, {'PROBE': 'true', 'GITHUB_REF': 'refs/heads/review/4.11.0'}):
            with self.assertRaises(ValueError):
                self.gate()

    def test_library_resources_do_not_change_verdict(self):
        self.expected.write_text('Std lemma\tCorrect\t\t\t100\t1\n')
        self.actual.write_text('Std lemma\tCorrect\t\t\t900\t1\n')
        self.assertEqual(0, self.gate(kind='std'))
        self.actual.write_text('Std lemma\tOutOfResource\t\t\t900\t1\n')
        self.assertEqual(1, self.gate(kind='std'))

    def test_resource_union_keeps_removed_and_new_batches(self):
        result = axioms.pair({'old': ['Valid', 10], 'both': ['Valid', 20]},
                            {'new': ['Invalid', 40], 'both': ['Valid', 90]})
        self.assertEqual(['both', 'new', 'old'], [r['batch'] for r in result])
        self.assertEqual([70, None, None], [r['delta'] for r in result])

    def test_inventory_distinguishes_diagnostic_programs(self):
        directory = self.root / 'measurements'
        for name in ['a.dfy', 'error.dfy', 'empty.dfy']:
            dest = directory / name
            dest.mkdir(parents=True)
            (dest / 'command.json').write_text('[]')
        (directory / 'a.dfy' / 'results.json').write_text(json.dumps({'verificationResults': [
            {'name': 'lemma', 'vcResults': [{'vcNum': 0, 'outcome': 'Valid', 'resourceCount': 123}]}]}))
        (directory / 'empty.dfy' / 'results.json').write_text('')
        path = self.root / 'inventory.json'
        axioms.inventory(directory, path)
        data = json.loads(path.read_text())
        self.assertEqual(['a.dfy', 'empty.dfy', 'error.dfy'], data['programs'])
        self.assertEqual({'empty.dfy': 'empty log', 'error.dfy': 'no log'}, data['unavailable'])
        self.assertEqual({'a.dfy\tlemma\t0': ['Valid', 123]}, data['batches'])


class ExistingRunnerCommands(unittest.TestCase):
    def test_suite_observations_preserve_trailing_missing_argument(self):
        lit = axioms.runner('lit')
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(lit.subprocess, 'run') as run:
                run.return_value.stdout = ''
                run.return_value.stderr = 'Missing argument for --print'
                run.return_value.returncode = 1
                result = lit.verify(('.', 'dafny', 'solver', 'a.dfy', '--print', True, directory))
            command = run.call_args.args[0]
            self.assertEqual('--print', command[-1])
            self.assertIn('--additional-axioms', command)
            self.assertEqual('1', result[1])

    def test_suite_without_measurements_retains_original_command(self):
        lit = axioms.runner('lit')
        with patch.object(lit.subprocess, 'run') as run:
            run.return_value.stdout = 'Dafny program verifier finished with 1 verified, 0 errors'
            run.return_value.stderr = ''
            run.return_value.returncode = 0
            lit.verify(('.', 'dafny', 'solver', 'a.dfy', '--manual-triggers', False, None))
        self.assertEqual(['dafny', 'verify', 'a.dfy', '--solver-path', 'solver'] +
                         lit.FIXED + ['--manual-triggers'], run.call_args.args[0])
