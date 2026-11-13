"""Security controls for the explicit event guards on self-hosted CI jobs.

Actionlint checks workflow syntax. These controls exercise the guards against
fork PRs, other repositories, and events that must never reach the Mac pool.
"""
from pathlib import Path
import re
from types import SimpleNamespace as NS
import unittest

ROOT = Path(__file__).resolve().parents[1] / 'workflows'


def mac_jobs():
    for path in ROOT.glob('*.yml'):
        text = path.read_text().split('\njobs:\n', 1)[-1]
        for match in re.finditer(r'^  ([\w-]+):\n(.*?)(?=^  [\w-]+:\n|\Z)', text, re.M | re.S):
            body = match[2]
            if re.search(r'^    runs-on:.*mac-studio|^            os: mac-studio', body, re.M):
                condition = re.search(r'^    if: (.*)$', body, re.M)
                yield path.name + '/' + match[1], condition[1] if condition else ''


def enabled(expression, event, head, repository, flags, ref_type="branch", draft=False, head_ref="", ref=None, reuse='false'):
    expression = expression.removeprefix('${{').removesuffix('}}').strip()
    expression = expression.replace('&&', ' and ').replace('||', ' or ')
    expression = re.sub(r'!(?!=)', ' not ', expression)
    # Booleans only: a quoted 'true', as in needs.tree.outputs.reuse != 'true', is a string.
    expression = re.sub(r"'[^']*'|\btrue\b|\bfalse\b",
                        lambda m: m[0] if m[0].startswith("'") else m[0].capitalize(), expression)
    github = NS(repository=repository, event_name=event,
                ref=ref or ('refs/tags/v-test' if ref_type == 'tag' else 'refs/heads/scratch/probe'),
                head_ref=head_ref,
                ref_type=ref_type, event=NS(pull_request=NS(draft=draft, head=NS(repo=NS(full_name=head)))))
    options = dict(platform_parity=False, binaries=False, default_comparison=False,
                   decreases_order_comparison=False, resolver_probe=False, additional_axioms_probe=False, platform='macos')
    options.update(flags if isinstance(flags, dict) else {k: flags for k in options if k != 'platform'})
    inputs = NS(**options)
    return bool(eval(expression.strip(), {'__builtins__': {}}, {
        'github': github, 'inputs': inputs, 'cancelled': lambda: False,
        'startsWith': str.startswith, 'needs': NS(suite=NS(result='success'), tree=NS(outputs=NS(reuse=reuse)))}))


class RoutingTests(unittest.TestCase):
    def test_mac_job_worker_is_reniced_before_checkout(self):
        for name, _ in mac_jobs():
            workflow, job = name.split('/')
            text = (ROOT / workflow).read_text()
            body = re.search(r'^  ' + re.escape(job) + r':\n(.*?)(?=^  [\w-]+:|\Z)', text, re.M | re.S)[1]
            with self.subTest(job=name):
                steps = body.split('    steps:\n', 1)[1]
                self.assertTrue(steps.startswith('      - name: Yield to interactive work\n'))
                first = steps.split('      - uses:', 1)[0]
                self.assertIn("if: runner.os == 'macOS'", first)
                self.assertIn('*/Runner.Worker|Runner.Worker)', first)
                self.assertIn('renice 10 -p "$PPID" "$$"', first)
                self.assertIn('ps -o nice= -p "$PPID"', first)

    def test_only_approved_library_version_routes_to_mac(self):
        text = (ROOT / 'review.yml').read_text()
        body = re.search(r'^  stdlib:\n(.*?)(?=^  [\w-]+:|\Z)', text, re.M | re.S)[1]
        expression = re.search(r'^    runs-on: \$\{\{ (.*) \}\}', body, re.M)[1]
        expression = expression.replace('&&', ' and ').replace('||', ' or ')
        for z3, harness, expected in [('5.1.0', '4.12.1', 'ubuntu-24.04'),
                                       ('of the tests', '4.12.1', 'mac-studio'),
                                       ('of the tests', '4.16.0', 'ubuntu-24.04')]:
            with self.subTest(z3=z3, harness=harness):
                self.assertEqual(expected, eval(expression, {'__builtins__': {}}, {
                    'matrix': NS(z3=z3), 'needs': NS(build=NS(outputs=NS(harness_z3=harness)))}))

    def test_self_hosted_jobs_are_present(self):
        self.assertGreater(len(list(mac_jobs())), 10)

    def test_each_mac_job_admits_an_authorized_event(self):
        cases = [('workflow_dispatch', False, 'branch'), ('workflow_dispatch', True, 'branch'),
                 ('workflow_dispatch', {'binaries': True}, 'branch'), ('push', False, 'tag')]
        for name, guard in mac_jobs():
            with self.subTest(job=name):
                self.assertTrue(any(enabled(guard, event, 'erniecohen/dafny', 'erniecohen/dafny', flags, ref_type)
                                    for event, flags, ref_type in cases))

    def test_maintenance_tag_dispatch_keeps_the_release_path(self):
        jobs = dict(mac_jobs())
        for name in ['review.yml/binaries', 'review.yml/release']:
            with self.subTest(job=name):
                self.assertTrue(enabled(jobs[name], 'workflow_dispatch', 'erniecohen/dafny',
                                        'erniecohen/dafny', False, 'tag'))
        self.assertFalse(enabled(jobs['review.yml/release'], 'workflow_dispatch', 'erniecohen/dafny',
                                 'erniecohen/dafny', {'binaries': True}, 'branch'))

    def test_a_reused_verification_skips_every_verifier_job(self):
        # Jobs of review.yml and obligation-proofs.yml are keyed on the tree.  The build and the
        # binaries stay for a tag, which still builds the release; nothing else runs on reuse.
        for name, guard in mac_jobs():
            if not name.startswith(('review.yml/', 'obligation-proofs.yml/')) or name in (
                    'review.yml/build', 'review.yml/binaries', 'review.yml/release', 'review.yml/parity-macos',
                    'review.yml/parity-report'):
                continue
            for event, flags, ref_type in [('push', False, 'branch'), ('pull_request', False, 'branch'),
                                           ('workflow_dispatch', True, 'branch'), ('push', False, 'tag'),
                                           ('workflow_dispatch', False, 'tag')]:
                with self.subTest(job=name, event=event, ref_type=ref_type):
                    head = 'erniecohen/dafny'
                    self.assertFalse(enabled(guard, event, head, 'erniecohen/dafny', flags, ref_type, reuse='true'))

    def test_build_stays_for_a_tag_but_not_for_a_reused_branch(self):
        build = dict(mac_jobs())['review.yml/build']
        r = 'erniecohen/dafny'
        self.assertFalse(enabled(build, 'push', r, r, False, 'branch', reuse='true'))
        self.assertFalse(enabled(build, 'pull_request', r, r, False, 'branch', reuse='true'))
        self.assertTrue(enabled(build, 'push', r, r, False, 'tag', reuse='true'))
        self.assertTrue(enabled(build, 'workflow_dispatch', r, r, False, 'tag', reuse='true'))
        self.assertTrue(enabled(build, 'push', r, r, False, 'branch', reuse='false'))

    def test_without_reuse_the_verifier_jobs_run_as_before(self):
        jobs = dict(mac_jobs())
        for name in ['review.yml/suite', 'review.yml/regressions', 'obligation-proofs.yml/shard']:
            with self.subTest(job=name):
                self.assertTrue(enabled(jobs[name], 'push', 'erniecohen/dafny', 'erniecohen/dafny', False))

    def test_fork_pull_requests_never_reach_mac_pool(self):
        for name, guard in mac_jobs():
            for flags in [False, True]:
                with self.subTest(job=name, flags=flags):
                    self.assertTrue(guard)
                    self.assertFalse(enabled(guard, 'pull_request', 'someone/dafny', 'erniecohen/dafny', flags))

    def test_other_repositories_never_reach_mac_pool(self):
        for name, guard in mac_jobs():
            for event in ['push', 'workflow_dispatch', 'pull_request']:
                with self.subTest(job=name, event=event):
                    self.assertFalse(enabled(guard, event, 'erniecohen/dafny', 'someone/dafny', True))

    def test_unsupported_events_never_reach_mac_pool(self):
        for name, guard in mac_jobs():
            for event in ['schedule', 'pull_request_target', 'workflow_run', 'issue_comment']:
                with self.subTest(job=name, event=event):
                    self.assertFalse(enabled(guard, event, 'erniecohen/dafny', 'erniecohen/dafny', True))


if __name__ == '__main__':
    unittest.main()
