import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('runners', Path(__file__).with_name('check-self-hosted-runners.py'))
runners = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runners)


class RunnerPolicyTests(unittest.TestCase):
    def test_all_workflows(self):
        count, errors = runners.check(Path(__file__).parents[1] / 'workflows')
        self.assertGreater(count, 50)
        self.assertEqual(errors, [])

    def test_every_admission_uses_repository_capacity_with_default_two(self):
        import re
        count = 0
        for workflow in (Path(__file__).parents[1] / 'workflows').glob('*.yml'):
            text = workflow.read_text()
            calls = re.findall(r'      - uses: erniecohen/dafny/\.github/actions/self-hosted-job@[^\n]+(?:\n        [^\n]*)*', text)
            for call in calls:
                self.assertIn("slots: ${{ vars.SELF_HOSTED_JOB_SLOTS || '2' }}", call, workflow.name)
            count += len(calls)
        self.assertEqual(count, 85)

    def test_rejects_hosted_labels_expressions_and_matrix_selectors(self):
        for label in ['ubuntu-24.04', 'macos-latest', 'windows-latest',
                      "${{ matrix.os }}", "${{ inputs.mac && 'mac-studio' || 'ubuntu-24.04' }}", '*labels']:
            with self.subTest(label=label), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / 'workflow.yml').write_text(f'on: push\njobs:\n  test:\n    runs-on: {label}\n')
                self.assertEqual(len(runners.check(root)[1]), 1)

    def test_rejects_external_reusable_workflows(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'workflow.yml').write_text('on: push\njobs:\n  test:\n    uses: example/repo/.github/workflows/test.yml@main\n')
            self.assertEqual(len(runners.check(root)[1]), 1)

class SolverPlatformTests(unittest.TestCase):
    def test_workflow_downloads_keep_pinned_macos_solvers(self):
        pins = {
            'z3-5.1.0-arm64-osx-13.3': '81d29e934fd863079a74af35eecaeaef8047e0e12414d33ca322b358d68383db',
            'z3-4.12.1-arm64-macos-15-bin': '5941a521e8868db9b57ed81d1cc0cc408b37e55443087bcb6eb207583b47183f',
            'z3-4.16.0-arm64-macos-15-bin': '9315fa2fb5087e3e21be98b11d58e4b443e023ed6627565c12b2f33f325f77eb',
        }
        import re
        for workflow in (Path(__file__).parents[1] / 'workflows').glob('*.yml'):
            text = workflow.read_text()
            with self.subTest(workflow=workflow.name):
                self.assertNotRegex(text, r'z3-[^\s"\']*(?:glibc|ubuntu)')
                for archive, digest in pins.items():
                    if archive in text:
                        self.assertIn(digest, text)
                self.assertNotRegex(text, r'--(?:jobs|cores) [4-9]\b')

class ParallelismPolicyTests(unittest.TestCase):
    def test_review_drivers_do_not_request_more_than_three_workers(self):
        import ast
        directory = Path(__file__).parent
        for script in directory.glob('*.py'):
            if script.name.startswith('test_'):
                continue
            tree = ast.parse(script.read_text())
            with self.subTest(script=script.name):
                for node in ast.walk(tree):
                    if isinstance(node, ast.Call):
                        for kw in node.keywords:
                            if kw.arg in ('jobs', 'cores') and isinstance(kw.value, ast.Constant) and isinstance(kw.value.value, int):
                                self.assertLessEqual(kw.value.value, 3)
                        if any(isinstance(arg, ast.Constant) and arg.value in ('--jobs', '--cores') for arg in node.args):
                            for kw in node.keywords:
                                if kw.arg == 'default' and isinstance(kw.value, ast.Constant) and isinstance(kw.value.value, int):
                                    self.assertLessEqual(kw.value.value, 3)
                    if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                        for arg, value in zip(node.args.args[-len(node.args.defaults):], node.args.defaults):
                            if arg.arg in ('jobs', 'cores') and isinstance(value, ast.Constant) and isinstance(value.value, int):
                                self.assertLessEqual(value.value, 3)
                    if isinstance(node, (ast.List, ast.Tuple)):
                        for before, after in zip(node.elts, node.elts[1:]):
                            if isinstance(before, ast.Constant) and before.value in ('--jobs', '--cores') and isinstance(after, ast.Constant):
                                if isinstance(after.value, str) and after.value.isdigit():
                                    self.assertLessEqual(int(after.value), 3)
