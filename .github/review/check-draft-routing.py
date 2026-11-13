"""Compare PR workflow job conditions against a pre-change Git revision.

Usage: python3 .github/review/check-draft-routing.py --base <revision>
Prints a Markdown table of condition results (before -> after). Dependencies
and matrices are unchanged; false job conditions skip every matrix instance.
"""
import argparse
import itertools
from pathlib import Path
import re
import subprocess

from test_ci_routing import enabled

ROOT = Path(__file__).resolve().parents[2]
GUARD = "github.event_name != 'pull_request' || !github.event.pull_request.draft"
TYPES = '    types: [opened, synchronize, reopened, ready_for_review]\n'
JOB = re.compile(r'^  ([\w-]+):\n(.*?)(?=^  [\w-]+:\n|\Z)', re.M | re.S)
CONDITION = re.compile(r'^    if: (.*)\n', re.M)


def jobs(text):
    return dict(JOB.findall(text.split('\njobs:\n', 1)[1]))


def condition(body):
    match = CONDITION.search(body)
    return match[1] if match else 'true'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', required=True)
    args = parser.parse_args()
    cases = [('Draft PR', 'pull_request', True), ('Ready PR', 'pull_request', False),
             ('Push', 'push', False), ('Dispatch', 'workflow_dispatch', False)]
    print('| Workflow/job | ' + ' | '.join(c[0] for c in cases) + ' |')
    print('|---|' + '---|' * len(cases))
    count = 0
    for path in sorted((ROOT / '.github/workflows').glob('*.yml')):
        text = path.read_text()
        if '\n  pull_request:\n' not in text:
            continue
        base = subprocess.check_output(
            ['git', 'show', f'{args.base}:{path.relative_to(ROOT)}'], cwd=ROOT, text=True)
        assert text.split('\njobs:\n')[0] == base.split('\njobs:\n')[0].replace(
            '  pull_request:\n', '  pull_request:\n' + TYPES), path
        old_jobs, new_jobs = jobs(base), jobs(text)
        assert old_jobs.keys() == new_jobs.keys(), path
        for name, body in new_jobs.items():
            old = condition(old_jobs[name])
            new = condition(body)
            expression = old.removeprefix('${{').removesuffix('}}').strip()
            expected = '${{ (' + GUARD + ') && (' + expression + ') }}'
            assert new == expected or (old == 'true' and new == '${{ ' + GUARD + ' }}'), name
            # Check names, dependencies, matrices, runners, steps and all other
            # job content are byte-for-byte unchanged.
            assert CONDITION.sub('', body) == CONDITION.sub('', old_jobs[name]), name
            cells = []
            for _, event, draft in cases:
                before = enabled(old, event, 'erniecohen/dafny', 'erniecohen/dafny', False)
                after = enabled(new, event, 'erniecohen/dafny', 'erniecohen/dafny', False, draft=draft)
                cells.append(f'{"run" if before else "skip"} → {"run" if after else "skip"}')
            print(f'| {path.name}/{name} | ' + ' | '.join(cells) + ' |')
            # Exercise both runner routes, special branch routing, every
            # combination of dispatch switches, schedules and tag releases.
            flags = ['platform_parity', 'binaries', 'default_comparison', 'decreases_order_comparison']
            for event, draft, head, repository, ref in itertools.product(
                ['pull_request', 'push', 'workflow_dispatch', 'schedule'], [False, True],
                ['erniecohen/dafny', 'someone/dafny'], ['erniecohen/dafny', 'someone/dafny'],
                ['refs/heads/review/4.11.0', 'refs/heads/scratch/194-probe',
                 'refs/heads/codex/194-operators-m1', 'refs/tags/v-test']):
                for values in itertools.product([False, True], repeat=len(flags)):
                    kwargs = dict(ref_type='tag' if ref.startswith('refs/tags/') else 'branch',
                                  draft=draft, head_ref=ref.removeprefix('refs/heads/'), ref=ref)
                    before = enabled(old, event, head, repository, dict(zip(flags, values)), **kwargs)
                    after = enabled(new, event, head, repository, dict(zip(flags, values)), **kwargs)
                    assert after == (False if event == 'pull_request' and draft else before), (path, name, event)
            count += 1
    assert count, 'No PR workflow jobs found'
    print(f'\nValidated {count} jobs; draft PRs skip all jobs; all other conditions are unchanged.')


if __name__ == '__main__':
    main()
