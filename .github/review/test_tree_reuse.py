"""Controls for tree-reuse.py: what may be reused, and never anything else."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('tree_reuse', Path(__file__).with_name('tree-reuse.py'))
tr = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tr)

REPO = 'erniecohen/dafny'
WORKFLOW = '.github/workflows/review.yml'
TREE = 'a' * 40


def run(**overrides):
    info = {'id': 7, 'status': 'completed', 'conclusion': 'success', 'event': 'pull_request',
            'path': WORKFLOW, 'html_url': 'https://github.com/erniecohen/dafny/actions/runs/7',
            'repository': {'full_name': REPO}, 'head_repository': {'full_name': REPO}}
    info.update(overrides)
    return info


def fake_api(artifacts, runs):
    def api(path):
        if '/actions/artifacts' in path:
            return {'artifacts': artifacts}
        return runs[int(path.rsplit('/', 1)[1])]
    return api


def artifact(run_id=7, name=f'verified-review-{TREE}', expired=False):
    return {'name': name, 'expired': expired, 'workflow_run': {'id': run_id}}


def find(artifacts, runs, event='push', ref_type='branch', head_repo=REPO, run_id=99):
    return tr.find(REPO, WORKFLOW, 'verified-review', TREE, event, ref_type, head_repo, run_id,
                   api=fake_api(artifacts, runs))


class FindTests(unittest.TestCase):
    def test_reuses_a_successful_completed_run(self):
        reuse, url, _ = find([artifact()], {7: run()})
        self.assertTrue(reuse)
        self.assertEqual('https://github.com/erniecohen/dafny/actions/runs/7', url)

    def test_never_reuses_failed_cancelled_or_unfinished_runs(self):
        for changes in [dict(conclusion='failure'), dict(conclusion='cancelled'), dict(conclusion=None, status='in_progress'),
                        dict(conclusion='skipped'), dict(status='queued', conclusion=None)]:
            with self.subTest(changes=changes):
                self.assertFalse(find([artifact()], {7: run(**changes)})[0])

    def test_never_reuses_from_another_repository_or_a_fork_head(self):
        self.assertFalse(find([artifact()], {7: run(repository={'full_name': 'someone/dafny'})})[0])
        self.assertFalse(find([artifact()], {7: run(head_repository={'full_name': 'someone/dafny'})})[0])
        self.assertFalse(find([artifact()], {7: run(head_repository=None)})[0])

    def test_never_reuses_another_workflow_or_a_manual_run(self):
        self.assertFalse(find([artifact()], {7: run(path='.github/workflows/obligation-proofs.yml')})[0])
        self.assertTrue(find([artifact()], {7: run(path=WORKFLOW + '@refs/heads/x')})[0])
        self.assertFalse(find([artifact()], {7: run(event='workflow_dispatch')})[0])
        self.assertFalse(find([artifact()], {7: run(event='schedule')})[0])

    def test_ignores_expired_other_tree_and_own_markers(self):
        self.assertFalse(find([artifact(expired=True)], {7: run()})[0])
        self.assertFalse(find([artifact(name='verified-review-' + 'b' * 40)], {7: run()})[0])
        self.assertFalse(find([artifact()], {7: run()}, run_id=7)[0])

    def test_a_later_good_marker_is_used_after_a_bad_one(self):
        reuse, url, _ = find([artifact(8), artifact(7)], {8: run(id=8, conclusion='failure'), 7: run()})
        self.assertTrue(reuse)
        self.assertTrue(url.endswith('/7'))

    def test_pull_requests_reuse_only_pull_request_runs(self):
        self.assertFalse(find([artifact()], {7: run(event='push')}, event='pull_request')[0])
        self.assertTrue(find([artifact()], {7: run(event='pull_request')}, event='pull_request')[0])
        self.assertTrue(find([artifact()], {7: run(event='push')}, event='push')[0])

    def test_fork_pull_requests_and_branch_dispatches_never_reuse(self):
        self.assertFalse(find([artifact()], {7: run()}, event='pull_request', head_repo='someone/dafny')[0])
        for event in ['workflow_dispatch', 'schedule']:
            self.assertFalse(find([artifact()], {7: run()}, event=event)[0])
        self.assertTrue(find([artifact()], {7: run()}, event='workflow_dispatch', ref_type='tag')[0])

    def test_an_unreadable_api_means_a_full_run(self):
        def broken(path):
            raise OSError('no network')
        self.assertFalse(tr.find(REPO, WORKFLOW, 'verified-review', TREE, 'push', 'branch', REPO, 1, api=broken)[0])


class GateTests(unittest.TestCase):
    def needs(self, reuse, **results):
        needs = {name: {'result': result} for name, result in results.items()}
        needs['tree'] = {'result': 'success', 'outputs': {'reuse': reuse}}
        return needs

    def test_full_run_is_verified_only_when_everything_succeeded(self):
        self.assertEqual((True, True), tr.gate(self.needs('false', a='success', b='success'), ['a', 'b'], False)[:2])
        self.assertEqual((True, False), tr.gate(self.needs('false', a='success', b='skipped'), ['a', 'b'], False)[:2])
        self.assertFalse(tr.gate(self.needs('false', a='success', b='skipped'), ['a', 'b'], True)[0])

    def test_failure_or_cancellation_always_fails(self):
        for reuse in ['true', 'false']:
            for result in ['failure', 'cancelled']:
                self.assertFalse(tr.gate(self.needs(reuse, a='success', b=result), ['a', 'b'], False)[0])

    def test_reuse_accepts_skipped_jobs_and_records_no_marker(self):
        self.assertEqual((True, False), tr.gate(self.needs('true', a='skipped', b='success'), ['a', 'b'], True)[:2])

    def test_a_missing_job_fails(self):
        self.assertFalse(tr.gate(self.needs('false', a='success'), ['a', 'b'], False)[0])


if __name__ == '__main__':
    unittest.main()
