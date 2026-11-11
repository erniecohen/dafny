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


def enabled(expression, event, head, repository, flags):
    expression = expression.removeprefix('${{').removesuffix('}}').strip()
    expression = expression.replace('&&', ' and ').replace('||', ' or ')
    expression = re.sub(r'!(?!=)', ' not ', expression)
    expression = re.sub(r'\btrue\b', 'True', expression)
    expression = re.sub(r'\bfalse\b', 'False', expression)
    github = NS(repository=repository, event_name=event, ref='refs/heads/scratch/probe',
                ref_type='branch', event=NS(pull_request=NS(head=NS(repo=NS(full_name=head)))))
    inputs = NS(platform_parity=flags, binaries=flags, default_comparison=flags,
                resolver_probe=flags, additional_axioms_probe=flags)
    return bool(eval(expression.strip(), {'__builtins__': {}}, {
        'github': github, 'inputs': inputs, 'cancelled': lambda: False,
        'startsWith': str.startswith, 'needs': NS(suite=NS(result='success'))}))


class RoutingTests(unittest.TestCase):
    def test_self_hosted_jobs_are_present(self):
        self.assertGreater(len(list(mac_jobs())), 10)

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
