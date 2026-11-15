#!/usr/bin/env python3
"""Reuse a verification that already succeeded on the identical git tree.

CI verifies a tree, not a commit.  The same tree is built three times on the way to
a release: by the pull request run, by the push run of the squash-merge commit, and by
the run on the release tag.  A run that verified a tree in full ends with a marker, the
artifact `<prefix>-<tree>`.  A later run of the same workflow computes its own tree
(`git rev-parse HEAD^{tree}`: for a pull request, the tree of the merge commit that is
checked out) and looks for such a marker.  When there is one, the expensive jobs skip.

    tree-reuse.py find    --workflow PATH --marker PREFIX   (job `tree`)
    tree-reuse.py gate    --jobs a,b,c [--strict]           (job `verdict`)
    tree-reuse.py marker  --out FILE                        (job `verdict`)

A marker is evidence only when all of these hold, and any doubt means no reuse:
  * it is not expired and belongs to a run of this repository, on a head of this repository;
  * the run is of the same workflow file, has completed, and its conclusion is success
    (never failed, cancelled, or still in progress, so never a rerun in progress);
  * the run was a push or a pull request (never a manual run, whose jobs may be probes);
  * a pull request run reuses only pull request runs, since some checks of a pull
    request depend on its base.  A push or a tag run reuses either.
The run that reuses is never a pull request from a fork, and a manual run reuses only
on a tag, which is how a release is built.  Trees that differ only in
`.github/review/base` are not identical: the stamp sets `dafny --version`, and the
version checks would then not run.
"""
import argparse
import json
import os
import subprocess
import sys

REPORTED_EVENTS = ('push', 'pull_request')


def gh_api(path):
    return json.loads(subprocess.check_output(['gh', 'api', path], text=True))


def tree_of_head():
    return subprocess.check_output(['git', 'rev-parse', 'HEAD^{tree}'], text=True).strip()


def eligible(event, ref_type, head_repo, repo):
    """May this run reuse a verification at all?  Returns (ok, reason)."""
    if event == 'push':
        return True, ''
    if event == 'pull_request':
        if head_repo != repo:
            return False, 'a pull request from another repository is never reused for'
        return True, ''
    if event == 'workflow_dispatch' and ref_type == 'tag':
        return True, ''
    return False, f'a {event} run on a {ref_type or "branch"} always verifies'


def accepted_events(event):
    return ('pull_request',) if event == 'pull_request' else REPORTED_EVENTS


def find(repo, workflow, marker, tree, event, ref_type, head_repo, run_id, api=gh_api):
    """Return (reuse, run_url_or_empty, reason)."""
    ok, reason = eligible(event, ref_type, head_repo, repo)
    if not ok:
        return False, '', reason
    name = f'{marker}-{tree}'
    try:
        artifacts = api(f'repos/{repo}/actions/artifacts?name={name}&per_page=100')['artifacts']
    except Exception as error:  # An unreachable API means a full run, never a failure.
        return False, '', f'the artifact list could not be read ({type(error).__name__})'
    seen = 0
    for artifact in artifacts:
        if artifact.get('name') != name or artifact.get('expired'):
            continue
        run = artifact.get('workflow_run') or {}
        if not run.get('id') or str(run['id']) == str(run_id):
            continue
        seen += 1
        try:
            info = api(f'repos/{repo}/actions/runs/{run["id"]}')
        except Exception:
            continue
        if (info.get('status') == 'completed' and info.get('conclusion') == 'success'
                and (info.get('repository') or {}).get('full_name') == repo
                and (info.get('head_repository') or {}).get('full_name') == repo
                and (info.get('path') or '').split('@')[0] == workflow
                and info.get('event') in accepted_events(event)):
            return True, info['html_url'], ''
    return False, '', (f'{seen} marker(s) for tree {tree}, none from a successful, completed run' if seen
                       else f'no verified run of tree {tree}')


def gate(needs, jobs, strict):
    """Judge the jobs a run depends on.  Returns (ok, verified, message)."""
    reuse = (needs.get('tree', {}).get('outputs') or {}).get('reuse') == 'true'
    results = {job: needs.get(job, {}).get('result', 'missing') for job in jobs}
    bad = {job: result for job, result in results.items() if result in ('failure', 'cancelled', 'missing')}
    if bad:
        return False, False, f'jobs did not succeed: {bad}'
    skipped = [job for job, result in results.items() if result == 'skipped']
    if reuse:
        return True, False, 'verification reused; skipped: ' + (', '.join(skipped) or 'none')
    if strict and skipped:
        return False, False, f'jobs were skipped without a reused verification: {skipped}'
    return True, not skipped, ('all jobs succeeded' if not skipped else f'not complete, skipped: {skipped}')


def output(key, value):
    line = f'{key}={value}'
    print(line)
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as handle:
            handle.write(line + '\n')


def summary(text):
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as handle:
            handle.write(text + '\n')


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('find')
    p.add_argument('--workflow', required=True, help='workflow path, as in .github/workflows/review.yml')
    p.add_argument('--marker', required=True, help='marker artifact prefix')
    p.add_argument('--tree', help='default: the tree of HEAD')
    p = sub.add_parser('gate')
    p.add_argument('--jobs', required=True)
    p.add_argument('--strict', action='store_true')
    p = sub.add_parser('marker')
    p.add_argument('--out', required=True)
    args = parser.parse_args(argv)
    env = os.environ
    if args.command == 'find':
        tree = args.tree or tree_of_head()
        reuse, url, reason = find(env['REPO'], args.workflow, args.marker, tree, env['EVENT'], env.get('REF_TYPE', ''),
                                  env.get('HEAD_REPO', ''), env.get('RUN_ID', ''))
        output('tree', tree)
        output('reuse', str(reuse).lower())
        output('run', url)
        summary('## Tree reuse\n\n| | |\n|---|---|\n' + f'| tree | `{tree}` |\n'
                + (f'| verification reused from | {url} |\n' if reuse else f'| verification | runs in full: {reason} |\n'))
        print(f'tree {tree}: ' + (f'reusing {url}' if reuse else f'no reuse: {reason}'))
    elif args.command == 'gate':
        ok, verified, message = gate(json.loads(env['NEEDS']), args.jobs.split(','), args.strict)
        output('verified', str(verified).lower())
        summary(f'## Verdict\n\n{message}')
        print(message)
        sys.exit(0 if ok else 1)
    else:
        with open(args.out, 'w') as handle:
            json.dump({'tree': tree_of_head(), 'commit': env['SHA'], 'event': env['EVENT'],
                       'run': env['RUN_ID']}, handle)
            handle.write('\n')


if __name__ == '__main__':
    main()
