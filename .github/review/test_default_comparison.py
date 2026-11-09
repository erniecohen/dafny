"""Measurement controls only; no verifier or solver subprocesses."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('comparison', Path(__file__).with_name('default-comparison.py'))
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class ComparisonControls(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.measurements = self.root / 'measurements'
        self.measurements.mkdir()
        self.inventory = {'parts': {part: comparison.population(
            {'Example': 2, 'Other': 1} if part == 'Std' else {'Example': 1}) for part in comparison.PARTS}}
        rows = []
        for part in sorted(comparison.PARTS):
            failing = part == 'Std'
            code, summary = ('4', '2 verified, 0 errors, 1 out of resource') if failing else ('0', '1 verified, 0 errors')
            errors = 'example.dfy(1,1)' if failing else ''
            (self.measurements / (part + '.invocation.json')).write_text(json.dumps({
                'exit': code, 'verification_log_present': True}))
            declarations = [{'name': 'Example', 'outcome': 'OutOfResource' if failing else 'Correct',
                             'resourceCount': 200 if failing else 100, 'vcResults': [
                                 {'vcNum': 1, 'outcome': 'OutOfResource' if failing else 'Valid', 'resourceCount': 100, 'assertions': []}]}]
            if failing:
                declarations[0]['vcResults'].append({'vcNum': 2, 'outcome': 'Valid', 'resourceCount': 100, 'assertions': []})
                declarations.append({'name': 'Other', 'outcome': 'Correct', 'resourceCount': 100,
                                     'vcResults': [{'vcNum': 1, 'outcome': 'Valid', 'resourceCount': 100, 'assertions': []}]})
            (self.measurements / (part + '.verification.json')).write_text(json.dumps({'verificationResults': declarations}))
            (self.measurements / (part + '.stdout')).write_text(
                ('example.dfy(1,1): Verification out of resource\n' if failing else '') +
                'Dafny program verifier finished with ' + summary + '\n')
            (self.measurements / (part + '.stderr')).write_text('')
            rows.append('run ' + part + '\t' + code + '\t' + summary + '\t' + errors + '\t3.2')
            for declaration in declarations:
                rows.append(part + ' ' + declaration['name'] + '\t' + declaration['outcome'] + '\t\t\t' +
                            str(declaration['resourceCount']) + '\t' + str(len(declaration['vcResults'])))
        self.tsv = self.root / 'std-verdicts.tsv'
        self.tsv.write_text('\n'.join(rows) + '\n')

    def vector(self):
        return comparison.vector(self.root, self.inventory)

    def change_log(self, change):
        file = self.measurements / 'Std.verification.json'
        data = json.loads(file.read_text())
        change(data['verificationResults'])
        file.write_text(json.dumps(data))

    def test_raw_cost_differences_remain_visible(self):
        before = self.vector()
        self.assertEqual({kind: len(value) for kind, value in before.items()}, {'runs': 7, 'declarations': 8, 'vcs': 9})
        def change(declarations):
            declarations[0]['resourceCount'] += 1
            declarations[0]['vcResults'][0]['resourceCount'] += 1
        self.change_log(change)
        self.tsv.write_text(self.tsv.read_text().replace('Std Example\tOutOfResource\t\t\t200\t2', 'Std Example\tOutOfResource\t\t\t201\t2'))
        after = self.vector()
        self.assertFalse(comparison.differences(before['runs'], after['runs']))
        self.assertEqual(len(comparison.differences(before['declarations'], after['declarations'])), 1)
        self.assertEqual(len(comparison.differences(before['vcs'], after['vcs'])), 1)

    def test_outcome_fold_matches_every_producer_transition(self):
        # Columns follow SolverOutcome cases in JsonVerificationLogger.MergeOutcomes.
        columns = ['Valid', 'Invalid', 'OutOfMemory', 'TimeOut', 'OutOfResource', 'Undetermined']
        rows = {
            'Correct': ['Correct', 'Errors', 'OutOfMemory', 'TimedOut', 'OutOfResource', 'Inconclusive'],
            'Errors': ['Errors', 'Errors', 'Errors', 'Errors', 'Errors', 'Errors'],
            'Inconclusive': ['Inconclusive', 'Errors', 'Inconclusive', 'Inconclusive', 'Inconclusive', 'Inconclusive'],
            'OutOfMemory': ['OutOfMemory', 'Errors', 'OutOfMemory', 'TimedOut', 'OutOfResource', 'Inconclusive'],
            'TimedOut': ['TimedOut', 'Errors', 'OutOfMemory', 'TimedOut', 'OutOfResource', 'Inconclusive'],
            'OutOfResource': ['OutOfResource', 'Errors', 'OutOfMemory', 'TimedOut', 'OutOfResource', 'Inconclusive'],
        }
        for current, expected in rows.items():
            for new, outcome in zip(columns, expected):
                with self.subTest(current=current, new=new):
                    self.assertEqual(comparison.merge_outcomes(current, new), outcome)
        with self.assertRaisesRegex(ValueError, 'producer outcome'):
            comparison.merge_outcomes('Correct', 'Bounded')

    def test_complete_mixed_failure_reports_follow_emitted_order(self):
        summary = '1 verified, 0 errors, 1 out of memory, 1 out of resource'
        for left, right, expected in [('OutOfMemory', 'OutOfResource', 'OutOfResource'),
                                      ('OutOfResource', 'OutOfMemory', 'OutOfMemory')]:
            with self.subTest(left=left, right=right):
                def change(declarations):
                    declarations[0]['outcome'] = expected
                    declarations[0]['vcResults'][0]['outcome'] = left
                    declarations[0]['vcResults'][1]['outcome'] = right
                self.change_log(change)
                rows = self.tsv.read_text().splitlines()
                rows = ['run Std\t4\t' + summary + '\texample.dfy(1,1)\t3.2' if row.startswith('run Std\t')
                        else 'Std Example\t' + expected + '\t\t\t200\t2' if row.startswith('Std Example\t') else row
                        for row in rows]
                self.tsv.write_text('\n'.join(rows) + '\n')
                (self.measurements / 'Std.stdout').write_text('example.dfy(1,1): Verification out of resource\n'
                    'Dafny program verifier finished with ' + summary + '\n')
                self.assertEqual(self.vector()['declarations']['Std Example'][0], expected)

    def test_complete_pass_has_explicit_validation(self):
        comparison.validated_vector(self.root, self.inventory, self.root, 'mock', 'A1', 0)
        self.assertEqual(json.loads((self.root / 'validation.json').read_text())['status'], 'complete-inventory')

    def test_timeout_and_other_nonverification_process_exits_are_incomplete(self):
        file = self.measurements / 'Std.invocation.json'
        for code in ['TIMEOUT', '1', '2', '3', '5', '-9']:
            with self.subTest(code=code):
                file.write_text(json.dumps({'exit': code, 'verification_log_present': True}))
                with self.assertRaisesRegex(ValueError, 'non-verification process exit'):
                    self.vector()

    def test_wrong_part_names_do_not_pass_by_count(self):
        (self.measurements / 'TargetSpecific-py.invocation.json').rename(self.measurements / 'unexpected.invocation.json')
        with self.assertRaisesRegex(ValueError, 'exact seven'):
            self.vector()

    def test_missing_part_is_incomplete(self):
        (self.measurements / 'Std.invocation.json').unlink()
        with self.assertRaisesRegex(ValueError, 'exact seven'):
            self.vector()

    def test_empty_log_is_incomplete_and_cannot_produce_comparison_summary(self):
        self.change_log(lambda declarations: declarations.clear())
        (self.measurements / 'Std.invocation.json').write_text(json.dumps({'exit': '0', 'verification_log_present': True}))
        (self.measurements / 'Std.stdout').write_text('Dafny program verifier finished with 0 verified, 0 errors\n')
        rows = [line for line in self.tsv.read_text().splitlines() if not line.startswith('Std ')]
        rows = ['run Std\t0\t0 verified, 0 errors\t\t0.1' if line.startswith('run Std\t') else line for line in rows]
        self.tsv.write_text('\n'.join(rows) + '\n')
        with self.assertRaisesRegex(ValueError, 'inventory differs'):
            comparison.validated_vector(self.root, self.inventory, self.root, 'mock', 'A1', 0)
        self.assertEqual(json.loads((self.root / 'comparison-status.json').read_text())['status'], 'incomplete')
        self.assertEqual(json.loads((self.root / 'validation.json').read_text())['status'], 'incomplete')
        self.assertFalse((self.root / 'summary.json').exists())
        self.assertFalse((self.root / 'vectors.json').exists())
        self.assertTrue((self.measurements / 'Std.verification.json').is_file())
        self.assertTrue((self.measurements / 'Std.stdout').is_file())

    def test_truncated_declaration_inventory_is_incomplete(self):
        self.change_log(lambda declarations: declarations.pop())
        self.tsv.write_text('\n'.join(line for line in self.tsv.read_text().splitlines()
                                     if not line.startswith('Std Other\t')) + '\n')
        self.reduce_verified_summary()
        with self.assertRaisesRegex(ValueError, 'inventory differs'):
            self.vector()

    def test_truncated_vc_inventory_is_incomplete(self):
        def change(declarations):
            declarations[0]['vcResults'].pop()
            declarations[0]['resourceCount'] = 100
        self.change_log(change)
        self.tsv.write_text(self.tsv.read_text().replace('Std Example\tOutOfResource\t\t\t200\t2', 'Std Example\tOutOfResource\t\t\t100\t1'))
        self.reduce_verified_summary()
        with self.assertRaisesRegex(ValueError, 'inventory differs'):
            self.vector()

    def reduce_verified_summary(self):
        stdout = self.measurements / 'Std.stdout'
        stdout.write_text(stdout.read_text().replace('2 verified', '1 verified'))
        self.tsv.write_text(self.tsv.read_text().replace('2 verified', '1 verified'))

    def test_missing_proof_log_is_incomplete(self):
        file = self.measurements / 'Std.invocation.json'
        file.write_text(json.dumps({'exit': '4', 'verification_log_present': False}))
        with self.assertRaisesRegex(ValueError, 'missing proof log'):
            self.vector()

    def test_duplicate_vc_rejected(self):
        self.change_log(lambda declarations: declarations[0]['vcResults'].extend(declarations[0]['vcResults']))
        with self.assertRaisesRegex(ValueError, 'duplicate VC'):
            self.vector()

    def test_invocation_exit_must_match_raw_verification_outcomes(self):
        file = self.measurements / 'Std.invocation.json'
        file.write_text(json.dumps({'exit': '0', 'verification_log_present': True}))
        with self.assertRaisesRegex(ValueError, 'exit disagrees'):
            self.vector()

    def test_tsv_exit_must_match_invocation(self):
        self.tsv.write_text(self.tsv.read_text().replace('run Std\t4\t', 'run Std\t0\t'))
        with self.assertRaisesRegex(ValueError, 'run TSV disagrees'):
            self.vector()

    def test_tsv_declaration_must_match_raw_proof_log(self):
        self.tsv.write_text(self.tsv.read_text().replace('Std Example\tOutOfResource\t\t\t200\t2', 'Std Example\tOutOfResource\t\t\t201\t2'))
        with self.assertRaisesRegex(ValueError, 'declaration TSV disagrees'):
            self.vector()

    def test_duplicate_tsv_row_rejected(self):
        self.tsv.write_text(self.tsv.read_text() + self.tsv.read_text().splitlines()[0] + '\n')
        with self.assertRaisesRegex(ValueError, 'duplicate TSV'):
            self.vector()

    def test_failure_summary_cannot_omit_recorded_verification_failure(self):
        file = self.measurements / 'Std.stdout'
        file.write_text(file.read_text().replace(', 1 out of resource', ''))
        with self.assertRaisesRegex(ValueError, 'verification failure summary'):
            self.vector()

    def test_runner_failure_is_retained_as_incomplete(self):
        with self.assertRaisesRegex(ValueError, 'runner failed'):
            comparison.validated_vector(self.root, self.inventory, self.root, 'mock', 'A1', 1)
        self.assertEqual(json.loads((self.root / 'comparison-status.json').read_text())['status'], 'incomplete')
        self.assertFalse((self.root / 'summary.json').exists())


if __name__ == '__main__':
    unittest.main()
