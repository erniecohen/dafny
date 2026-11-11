"""Fail-closed controls for the paired module-resolution audit; no solver runs."""
import contextlib
import copy
import importlib.util
import io
import json
import os
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
