#!/usr/bin/env python3
"""The vacuity control of github-issue-241-release-verified-modules.dfy's window runs.

    github-issue-241-release-verified-modules.py <profile.jsonl> [parallel]

A window run's output equals the output without --release-verified-modules.  That shows that
no dropped body was read only if later modules were translated after the modules they use
released their bodies.  With a window of 4, a Late module is admitted only when an earlier
module is reported, so Late3 and Late4 come after Library and Impl are reported.  This
requires that Library and Impl released bodies, and that Late3's and Late4's translations
started after both releases.  With `parallel` (a run with --translation-workers), it also
requires that every Late module was translated on the parallel path, which commits each
translation; whether two of them overlap depends on timing, and is not required.
"""
import json
import sys

events = [json.loads(line) for line in open(sys.argv[1])]
released = {e['module']: e['t'] for e in events if e.get('ev') == 'release' and e.get('bodies', 0) > 0}
assert {'Library', 'Impl'} <= set(released), 'expected Library and Impl to release bodies: %s' % sorted(released)
after = max(released['Library'], released['Impl'])
lates = ['Late1', 'Late2', 'Late3', 'Late4']
starts = {e['module']: e['start'] for e in events if e.get('stage') == 'translate'}
late = [m for m in lates if starts.get(m, 0) > after]
assert {'Late3', 'Late4'} <= set(late), 'Late3 and Late4 should start after the releases: %s' % late
if sys.argv[2:] == ['parallel']:
    commits = {e['module'] for e in events if e.get('stage') == 'translate.commit'}
    assert set(lates) <= commits, 'a Late module was not translated on the parallel path: %s' % sorted(commits)
