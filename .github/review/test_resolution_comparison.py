"""Fail-closed controls for the paired module-resolution audit; no solver runs."""
import contextlib
import copy
import importlib.util
import io
import json
import os
import shutil
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('resolution_comparison', Path(__file__).with_name('resolution-comparison.py'))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class ResolutionComparisonControls(unittest.TestCase):
    def setUp(self):
        parent = audit.ROOT / 'work/resolution-audit-tests'
        parent.mkdir(parents=True, exist_ok=True)
        temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.vector = dict(verdicts={'example.dfy': ['0', '1 verified, 0 errors', '']},
            proofs={'example.dfy\tF\t1': ['Valid', 100]},
            boogie={'example.dfy': {'program_M.bpl': 'digest'}}, total_ru=100, out_of_resource_vcs=0)
        for mode in audit.MODES:
            audit.dump(self.root / mode / 'vector.json', self.vector)
        self.expected = self.root / 'expected.tsv'
        self.expected.write_text('example.dfy\t0\t1 verified, 0 errors\t\n')

    def check(self):
        with contextlib.redirect_stdout(io.StringIO()), patch.dict(os.environ, {'GITHUB_STEP_SUMMARY': ''}):
            return audit.compare(SimpleNamespace(output=self.root, part='0', expected=self.expected))

    def test_complete_equal_vectors_pass(self):
        self.assertEqual(self.check(), 0)

    def test_n4_byte_difference_fails(self):
        changed = copy.deepcopy(self.vector)
        changed['boogie']['example.dfy']['program_M.bpl'] = 'different-bytes'
        audit.dump(self.root / '4/vector.json', changed)
        self.assertEqual(self.check(), 1)

    def test_default_ru_difference_fails(self):
        changed = copy.deepcopy(self.vector)
        changed['proofs']['example.dfy\tF\t1'][1] += 1
        audit.dump(self.root / 'off/vector.json', changed)
        self.assertEqual(self.check(), 1)

    def test_identical_worker_loss_of_a_proof_is_rejected(self):
        changed = copy.deepcopy(self.vector)
        changed['proofs'].clear()
        for mode in ('1', '2', '4', '8'):
            audit.dump(self.root / mode / 'vector.json', changed)
        self.assertEqual(self.check(), 1)

    def test_missing_reviewed_baseline_fails(self):
        self.expected.unlink()
        self.assertEqual(self.check(), 1)

    def test_seed_zero_reproduces_all_proofs(self):
        audit.validate_seed_zero(self.vector, 'example.dfy', [dict(name='F',
            vcResults=[dict(vcNum=1, outcome='Valid', resourceCount=100)])])

    def test_seed_zero_changed_name_history_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'does not reproduce'):
            audit.validate_seed_zero(self.vector, 'example.dfy', [dict(name='F',
                vcResults=[dict(vcNum=1, outcome='Valid', resourceCount=101)])])

    def test_seed_zero_filtered_proof_population_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'does not reproduce'):
            audit.validate_seed_zero(self.vector, 'example.dfy', [])

    def test_std_seeds_preserve_the_complete_native_invocation(self):
        source = 'Std.verification.json'
        changes = {}
        for mode in ('off', '1'):
            outcome, cost = ('Valid', 100) if mode == 'off' else ('OutOfResource', 110)
            proofs = {source + '\t' + name + '\t1': [outcome, cost] for name in ('F', 'G')}
            audit.dump(self.root / mode / 'vector.json', dict(proofs=proofs))
            for key, value in proofs.items():
                changes.setdefault(key, {})['before' if mode == 'off' else 'after'] = value
        audit.dump(self.root / 'report.json', dict(default_to_on=dict(proofs=changes)))
        audit.dump(self.root / 'off/measurements/Std.invocation.json', dict(command=[
            'adapter', 'verify', 'src/Std/dfyconfig.toml', '--log-format', 'json;LogFileName=old',
            '--boogie', '/deterministicLiteralHashes:1 /restartProver', '--solver-path', 'original-z3']))
        commands = []

        def fake_process(command, **kwargs):
            commands.append(command)
            enabled = '--resolution-threads' in command
            outcome, cost = ('OutOfResource', 110) if enabled else ('Valid', 100)
            records = [dict(name=name, outcome=outcome, resourceCount=cost,
                vcResults=[dict(vcNum=1, outcome=outcome, resourceCount=cost)]) for name in ('F', 'G')]
            log = command[command.index('--log-format') + 1].split('LogFileName=', 1)[1]
            audit.dump(Path(log), dict(verificationResults=records))
            return SimpleNamespace(returncode=4 if enabled else 0, communicate=lambda **kw: ('', None))

        with patch.object(audit.subprocess, 'Popen', side_effect=fake_process), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(audit.variance(SimpleNamespace(output=self.root, part='stdlib', dafny=Path('candidate'))), 0)
        self.assertEqual(len(commands), 6)
        for command in commands:
            self.assertNotIn('--filter-symbol', command)
            self.assertIn('--bprint', command)
            self.assertIn('/deterministicLiteralHashes:1 /restartProver', command)
        self.assertEqual(len(json.loads((self.root / 'seed-variance.json').read_text())), 6)
        shutil.rmtree(self.root / 'seed-variance')
        commands.clear()
        with patch.object(audit.subprocess, 'Popen', side_effect=fake_process), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(audit.variance(SimpleNamespace(output=self.root, part='stdlib', dafny=Path('candidate'),
                seed=0, mode='off', z3=Path('replacement-z3'))), 0)
        command, = commands
        self.assertNotIn('--resolution-threads', command)
        self.assertEqual(command[command.index('--solver-path') + 1], str(Path('replacement-z3').resolve()))
        self.assertEqual(len(json.loads((self.root / 'seed-variance.json').read_text())), 1)
