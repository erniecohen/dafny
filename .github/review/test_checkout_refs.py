"""Run the checkout helpers against real Git history with colliding remote names."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ACTIONS = Path(__file__).resolve().parents[1] / 'actions'


class CheckoutRefsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        self.git(self.source, 'init', '-q', '-b', 'review/4.11.0')
        self.git(self.source, 'config', 'user.name', 'Checkout control')
        self.git(self.source, 'config', 'user.email', 'checkout@example.invalid')
        (self.source / 'shared').write_text('base')
        self.git(self.source, 'add', '.')
        self.git(self.source, 'commit', '-qm', 'Common ancestor')
        self.common = self.git(self.source, 'rev-parse', 'HEAD')
        self.git(self.source, 'checkout', '-qb', 'feature')
        (self.source / 'feature').write_text('head')
        self.git(self.source, 'add', '.')
        self.git(self.source, 'commit', '-qm', 'Feature')
        self.head = self.git(self.source, 'rev-parse', 'HEAD')
        self.git(self.source, 'checkout', '-q', 'review/4.11.0')
        (self.source / 'base').write_text('base update')
        self.git(self.source, 'add', '.')
        self.git(self.source, 'commit', '-qm', 'Base advanced')
        self.base = self.git(self.source, 'rev-parse', 'HEAD')
        self.git(self.source, 'merge', '-qm', 'PR merge', '--no-ff', 'feature')
        self.merge = self.git(self.source, 'rev-parse', 'HEAD')
        self.git(self.source, 'update-ref', 'refs/pull/1/merge', self.merge)
        self.git(self.source, 'tag', 'v-control')
        self.remote = self.root / 'remote.git'
        self.git(self.root, 'clone', '-q', '--bare', str(self.source), str(self.remote))
        self.git(self.remote, 'update-ref', 'refs/pull/1/merge', self.merge)
        self.git(self.remote, 'pack-refs', '--all')
        packed = self.remote / 'packed-refs'
        records = [line for line in packed.read_text().splitlines() if not line.startswith('#')]
        records += [self.base + ' refs/heads/CaseCollision', self.head + ' refs/heads/casecollision']
        packed.write_text('# pack-refs with: sorted\n' + '\n'.join(sorted(records, key=lambda s: s.split()[1])) + '\n')
        self.assertIn('refs/heads/CaseCollision', self.git(self.remote, 'show-ref'))
        self.assertIn('refs/heads/casecollision', self.git(self.remote, 'show-ref'))
        self.workspace = self.root / 'workspace'
        self.workspace.mkdir()
        self.initialize_checkout()

    def git(self, cwd, *args):
        return subprocess.check_output(['git', *args], cwd=cwd, text=True, stderr=subprocess.PIPE).strip()

    def initialize_checkout(self):
        self.git(self.workspace, 'init', '-q')
        self.git(self.workspace, 'remote', 'add', 'origin', self.remote.as_uri())
        self.git(self.workspace, 'fetch', '-q', '--no-tags', '--depth=1', '--refmap=', 'origin', self.merge)
        self.git(self.workspace, 'checkout', '-q', '--detach', 'FETCH_HEAD')

    def fetch(self, refs, full='false', check=True):
        result = subprocess.run(['bash', str(ACTIONS / 'fetch-refs/fetch.sh')], cwd=self.workspace,
            env=dict(os.environ, REQUIRED_REFS='\n'.join(refs), FULL_HISTORY=full),
            capture_output=True, text=True)
        if check:
            self.assertEqual(result.returncode, 0, result.stderr)
        return result

    def assert_no_unrelated_refs(self):
        refs = self.git(self.workspace, 'for-each-ref', '--format=%(refname)')
        self.assertNotIn('CaseCollision', refs)
        self.assertNotIn('casecollision', refs)
        self.assertNotIn('refs/tags/', refs)
        self.assertNotIn('refs/remotes/', refs)

    def test_targeted_ancestry_merge_base_and_complete_repository_retry(self):
        self.assertEqual(self.git(self.workspace, 'rev-parse', '--is-shallow-repository'), 'true')
        self.fetch([self.base, self.head], 'true')
        self.assertEqual(self.git(self.workspace, 'merge-base', self.base, self.head), self.common)
        scoped = subprocess.run([sys.executable, str(ACTIONS.parent / 'review/milestone-scope.py'),
            '--event', 'pull_request', '--base', self.base, '--head', self.head],
            cwd=self.workspace, capture_output=True, text=True, check=True)
        self.assertIn('base=' + self.common, scoped.stdout)
        self.assertIn('candidate=' + self.head, scoped.stdout)
        self.assert_no_unrelated_refs()
        # Exercise the complete-repository path too: unshallow must not be retried there.
        self.git(self.workspace, 'fetch', '-q', '--no-tags', '--refmap=', 'origin', self.merge)
        if self.git(self.workspace, 'rev-parse', '--is-shallow-repository') == 'true':
            self.git(self.workspace, 'fetch', '-q', '--no-tags', '--refmap=', '--unshallow', 'origin', self.merge)
        self.assertEqual(self.git(self.workspace, 'rev-parse', '--is-shallow-repository'), 'false')
        self.fetch([self.base, self.head], 'true')
        self.assertEqual(self.git(self.workspace, 'merge-base', self.base, self.head), self.common)
        self.assert_no_unrelated_refs()

    def test_pinned_baseline_needs_only_one_commit(self):
        self.fetch([self.common])
        self.git(self.workspace, 'worktree', 'add', '--detach', str(self.root / 'baseline'), self.common)
        self.assertEqual(self.git(self.root / 'baseline', 'rev-parse', 'HEAD'), self.common)
        self.assert_no_unrelated_refs()

    def test_explicit_branch_and_pull_refs_are_supported_without_other_branches(self):
        self.fetch(['refs/heads/feature', 'refs/pull/1/merge'])
        self.assertEqual(self.git(self.workspace, 'rev-parse', 'refs/ci/required/0'), self.head)
        self.assertEqual(self.git(self.workspace, 'rev-parse', 'refs/ci/required/1'), self.merge)
        self.assert_no_unrelated_refs()

    def test_control_full_branch_fetch_fails_on_case_insensitive_filesystem(self):
        (self.root / 'case-probe').touch()
        if not (self.root / 'CASE-PROBE').exists():
            self.skipTest('Collision failure requires a case-insensitive filesystem')
        wide = self.root / 'wide'; wide.mkdir()
        self.git(wide, 'init', '-q')
        self.git(wide, 'remote', 'add', 'origin', self.remote.as_uri())
        result = subprocess.run(['git', 'fetch', 'origin'], cwd=wide, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('case-insensitive', result.stderr)

    def test_wildcards_options_and_empty_refs_are_rejected(self):
        for refs in [[], ['refs/heads/*'], ['--all'], ['feature']]:
            with self.subTest(refs=refs):
                self.assertNotEqual(self.fetch(refs, check=False).returncode, 0)
        self.assertNotEqual(self.fetch([self.base], full='invalid', check=False).returncode, 0)
        self.assert_no_unrelated_refs()

    def test_stale_workspace_is_cleared_before_shallow_checkout(self):
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        # Simulate a failed prior checkout, including packed case-colliding refs and
        # an obsolete shallow boundary. Never clean another workspace or follow links.
        shutil.copyfile(self.remote / 'packed-refs', self.workspace / '.git/packed-refs')
        outside = self.root / 'outside'; outside.mkdir()
        (outside / 'keep').touch()
        (self.workspace / 'old-output').symlink_to(outside, target_is_directory=True)
        script = "const {prepareWorkspace} = require(process.argv[1]); prepareWorkspace(process.argv[2], process.argv[3]);"
        subprocess.run([node, '-e', script, str(ACTIONS / 'self-hosted-job/index.cjs'),
                        str(self.workspace), str(self.root)], check=True)
        self.assertEqual(list(self.workspace.iterdir()), [])
        self.assertTrue((outside / 'keep').exists())
        # If checkout never initialized a repository, the post action still cleans
        # this job's scratch without trying git clean in an empty directory.
        scratch = self.root / 'fork-job.failed-checkout'; scratch.mkdir()
        (self.root / 'dotnet').write_text('#!/bin/sh\nexit 0\n')
        (self.root / 'dotnet').chmod(0o755)
        subprocess.run([node, str(ACTIONS / 'self-hosted-job/index.cjs')], check=True,
            env=dict(os.environ, GITHUB_WORKSPACE=str(self.workspace), RUNNER_TEMP=str(self.root),
                STATE_tmp=str(scratch), STATE_keeper=str(os.getpid()), STATE_keeperIdentity='different identity',
                PATH=str(self.root) + os.pathsep + os.environ['PATH']))
        self.assertFalse(scratch.exists())
        self.initialize_checkout()
        self.fetch([self.base, self.head], 'true')
        self.assertEqual(self.git(self.workspace, 'merge-base', self.base, self.head), self.common)
        self.assert_no_unrelated_refs()

    def test_workspace_cleanup_rejects_unrelated_directory(self):
        node = shutil.which('node')
        if not node:
            self.skipTest('Node is provided by the Actions runner')
        script = "const {prepareWorkspace} = require(process.argv[1]); prepareWorkspace(process.argv[2], process.argv[3]);"
        result = subprocess.run([node, '-e', script, str(ACTIONS / 'self-hosted-job/index.cjs'),
            str(self.workspace), str(self.source)], capture_output=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue((self.workspace / '.git').is_dir())


if __name__ == '__main__':
    unittest.main()
