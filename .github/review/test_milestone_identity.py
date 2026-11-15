"""Keep M1 historical identity strict without freezing later compiler or proof edits."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('identity', Path(__file__).with_name('issue193-identity.py'))
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)


class IdentityTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix='milestone-identity-')
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.git('init', '-q')
        self.git('config', 'user.name', 'Identity test')
        self.git('config', 'user.email', 'test@example.invalid')
        self.git('config', 'commit.gpgsign', 'false')
        (self.root / 'hooks').mkdir()
        self.git('config', 'core.hooksPath', str(self.root / 'hooks'))
        self.write('Source/Compiler.cs', 'original compiler\n')
        self.base = self.commit()
        self.add_optional()
        self.introduced = self.commit()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    def add_optional(self, missing=None):
        for path in sorted(identity.ADDED - ({missing} if missing else set())):
            self.write(path, 'optional proof\n')

    def commit(self):
        self.git('add', '.')
        self.git('commit', '-qm', 'test input')
        return self.git('rev-parse', 'HEAD')

    def check(self):
        with patch.multiple(identity, ROOT=self.root, BASE=self.base, INTRODUCED=self.introduced):
            return identity.check()

    def test_later_independent_compiler_change_retains_the_bounded_claim(self):
        self.write('Source/Compiler.cs', 'independently reviewed compiler fix\n')
        head = self.commit()
        result = self.check()
        self.assertEqual(result['introduced'], self.introduced)
        self.assertEqual(result['head'], head)
        self.assertTrue(result['existing_product_input_identity'])
        self.assertNotIn('current_optional_input_identity', result)

    def test_introducing_commit_cannot_change_existing_product_inputs(self):
        self.git('checkout', '-q', self.base)
        self.add_optional()
        self.write('Source/Compiler.cs', 'unexpected compiler change\n')
        self.introduced = self.commit()
        with self.assertRaisesRegex(ValueError, 'Old product input changed'):
            self.check()

    def test_later_optional_input_changes_preserve_the_historical_claim(self):
        self.write(sorted(identity.ADDED)[0], 'revised proof for a later milestone\n')
        head = self.commit()
        result = self.check()
        self.assertEqual(result['introduced'], self.introduced)
        self.assertEqual(result['head'], head)
        self.assertTrue(result['existing_product_input_identity'])
        self.assertNotIn('current_optional_input_identity', result)

    def test_missing_introduced_input_is_rejected(self):
        self.git('checkout', '-q', self.base)
        self.add_optional(missing=sorted(identity.ADDED)[0])
        self.introduced = self.commit()
        with self.assertRaisesRegex(ValueError, 'every optional input'):
            self.check()

    def test_scope_cli_uses_merge_base_for_a_diverged_product_pr(self):
        self.git('checkout', '-qb', 'target')
        self.write('Source/Target.cs', 'later target fix\n')
        target = self.commit()
        self.git('checkout', '-qb', 'candidate', self.introduced)
        self.write('Source/Compiler.cs', 'candidate product fix\n')
        head = self.commit()
        script = Path(__file__).with_name('milestone-scope.py').resolve()
        output = subprocess.check_output(['python3', str(script), '--event', 'pull_request',
                                          '--base', target, '--head', head], cwd=self.root, text=True)
        result = dict(line.split('=', 1) for line in output.splitlines())
        self.assertEqual(result, dict(base=self.introduced, candidate=head,
                                     integer_bitwise='false', source_contracts='false'))

    def test_advancing_the_base_cannot_make_the_check_vacuous(self):
        self.base = self.introduced
        with self.assertRaisesRegex(ValueError, 'introducing commit parent'):
            self.check()


scope_spec = importlib.util.spec_from_file_location('scope', Path(__file__).with_name('milestone-scope.py'))
scope = importlib.util.module_from_spec(scope_spec)
scope_spec.loader.exec_module(scope)


class ScopeTests(unittest.TestCase):
    def test_product_only_and_workflow_only_prs(self):
        for path in ['Source/DafnyCore/Verifier/BoogieGenerator.cs',
                     '.github/workflows/review.yml', '.github/workflows/issue211-contracts.yml']:
            self.assertEqual(scope.scope('pull_request', [path]),
                             dict(integer_bitwise=False, source_contracts=False))

    def test_each_milestone_file_triggers_its_own_gate(self):
        for path in [*identity.ADDED, '.github/review/issue193-identity.py',
                     'Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-reference-laws.dfy']:
            self.assertEqual(scope.scope('pull_request', [path]),
                             dict(integer_bitwise=True, source_contracts=False))
        for path in scope.CONTRACTS:
            self.assertEqual(scope.scope('pull_request', [path]),
                             dict(integer_bitwise=False, source_contracts=True))

    def test_push_is_not_a_historical_comparison(self):
        self.assertEqual(scope.scope('push', [*identity.ADDED, *scope.CONTRACTS]),
                         dict(integer_bitwise=False, source_contracts=False))

    def test_dispatch_replays_both_historical_comparisons(self):
        self.assertEqual(scope.scope('workflow_dispatch', []),
                         dict(integer_bitwise=True, source_contracts=True))

    def test_workflow_conditions(self):
        # Exercise actual YAML expressions, rather than a duplicate routing formula.
        import re
        from types import SimpleNamespace as NS
        workflows = Path(__file__).resolve().parents[1] / 'workflows'
        guards = {}
        for name, jobs in [('review.yml', ['milestone-scope', 'integer-bitwise-reference']),
                           ('issue211-contracts.yml', ['milestone-scope', 'products', 'identity'])]:
            text = (workflows / name).read_text()
            for job in jobs:
                body = re.search(r'^  ' + job + r':\n(.*?)(?=^  [\w-]+:|\Z)', text, re.M | re.S)[1]
                guards[name + '/' + job] = re.search(r'^    if: (.*)$', body, re.M)[1]
            if name == 'review.yml':
                guards['M1 identity step'] = "${{ " + re.search(
                    r'if: (needs.milestone-scope.outputs.integer_bitwise[^\n]+)', text)[1] + ' }}'
        cases = [('product PR', 'pull_request', [], False),
                 ('milestone PR', 'pull_request', [*identity.ADDED, *scope.CONTRACTS], False),
                 ('push', 'push', [], False), ('dispatch', 'workflow_dispatch', [], False),
                 ('draft milestone PR', 'pull_request', [*identity.ADDED, *scope.CONTRACTS], True)]
        for label, event, paths, draft in cases:
            outputs = scope.scope(event, paths)
            for name, guard in guards.items():
                expr = guard.removeprefix('${{').removesuffix('}}').strip()
                for key, value in outputs.items():
                    expr = expr.replace('needs.milestone-scope.outputs.' + key, repr(str(value).lower()))
                expr = expr.replace('&&', ' and ').replace('||', ' or ')
                expr = re.sub(r'!(?!=)', ' not ', expr)
                value = bool(eval(expr.strip(), {'__builtins__': {}}, {
                    'github': NS(event_name=event, event=NS(pull_request=NS(draft=draft))),
                    'needs': NS(tree=NS(outputs=NS(reuse='false')))}))
                expected = not draft
                if name.endswith(('/products', '/identity')):
                    expected &= outputs['source_contracts']
                if name == 'M1 identity step':
                    # Step guard is evaluated only in a running job.
                    expected = outputs['integer_bitwise']
                self.assertEqual(value, expected, (label, name))


if __name__ == '__main__':
    unittest.main()
