#!/usr/bin/env python3
"""Inspect actual captured local-identity queries; this script does not invoke a solver."""
import argparse
import json
from pathlib import Path
import re

p = argparse.ArgumentParser(); p.add_argument('--input', required=True)
a = p.parse_args(); root = Path(a.input)
helpers = {}
exec(Path('.github/review/issue33-profile.py').read_text().split('p=argparse.ArgumentParser();')[0], helpers)
report = {'queries': [], 'checks': {}}
for refresh in ['false', 'true']:
    for name in ['github-issue-33-original.dfy', 'github-issue-74-isolation.dfy']:
        for mode in ['BE', 'E']:
            dest = root / refresh / name / mode
            for path in sorted(dest.glob('solver.smt2*')):
                for index, (vc, body) in enumerate(helpers['queries'](path)):
                    candidates = []
                    for command in body:
                        match = re.search(r'\(forall \(\((\S+) Int\)\s*\) \(! \(= \((\S+) \1\) \1\)', command)
                        if match:
                            candidates.append(match[2])
                    # LitInt is the unguarded unary Int identity in this prelude.
                    candidates = sorted(set(candidates))
                    literals = []
                    top = []
                    for lit in candidates:
                        pattern = r'\(= \(' + re.escape(lit) + r' (\d+|\(- \d+\))\) \1\)'
                        literals += [m[1] for command in body for m in re.finditer(pattern, command)]
                        top += [command for command in body if re.fullmatch(r'\(assert ' + pattern + r'\)', command)]
                    report['queries'].append({'refresh': refresh, 'file': name, 'mode': mode,
                        'vc': vc or None, 'query_index': index, 'solver_log': path.name, 'identity_symbols': candidates, 'closed_identity_numerals': literals,
                        'top_level_closed_identity_assertions': top})
rows = report['queries']
original = [r for r in rows if r['mode'] == 'E' and r['file'].endswith('original.dfy')]
isolated = [r for r in rows if r['mode'] == 'E' and r['file'].endswith('isolation.dfy')]
# Debug-free normalized logs omit VC names. Do not invent an ordinal-to-name map.
# The structural test checks named declarations; here inspect every actual query.
report['checks'] = {'original_queries_found': len(original) == 34,
    'original_zero_available_in_each_resolver': all(sum('0' in r['closed_identity_numerals'] for r in original
        if r['refresh'] == mode) == 1 for mode in ['false', 'true']),
    'original_closed_identities_only_zero': bool(original) and all(set(r['closed_identity_numerals']) <= {'0'} for r in original),
    'isolation_queries_found': len(isolated) == 10,
    'no_other_body_identity_777_propagation': bool(isolated) and all('777' not in r['closed_identity_numerals'] for r in isolated),
    'isolation_only_expected_local_numerals': bool(isolated) and all(set(r['closed_identity_numerals']) <= {'7', '17', '42'} for r in isolated),
    'isolation_has_six_queries_without_closed_identities': sum(not r['closed_identity_numerals'] for r in isolated) == 6,
    'no_top_level_closed_identity_assertions': bool(rows) and all(not r['top_level_closed_identity_assertions'] for r in rows)}
(root / 'smt-audit.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report['checks'], indent=2))
