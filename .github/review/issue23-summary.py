#!/usr/bin/env python3
"""Summarize the downloaded issue23-* artifacts; fail on missing/inconsistent evidence."""
import hashlib
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
report = {'suite': {}, 'library': {}, 'focused': {}, 'boogie': []}
verdicts, resources = {}, {}
for variant in ['before', 'after']:
    rows, batches, missing = {}, {}, []
    for shard in range(4):
        path = root / f'issue23-{variant}-suite{shard}'
        assert (path / 'probe-exit.txt').read_text().strip() == '0', path
        for line in (path / 'verdicts.tsv').read_text().splitlines():
            row = line.split('\t')
            assert row[0] not in rows, row[0]
            rows[row[0]] = row[1:4]
        for command in (path / 'measurements').rglob('command.json'):
            sample = command.parent.relative_to(path / 'measurements').as_posix()
            log = command.with_name('results.json')
            if not log.exists() or not log.read_text().strip():
                missing.append(sample)
                continue
            for decl in json.loads(log.read_text()).get('verificationResults', []):
                for vc in decl['vcResults']:
                    key = '\t'.join([sample, decl['name'], str(vc['vcNum'])])
                    assert key not in batches, key
                    batches[key] = [vc['outcome'], vc['resourceCount']]
    verdicts[variant], resources[variant] = rows, batches
    selected = {key: value for key, value in batches.items()
                if ('dafny0/Maps.dfy' in key and 'GeneralMaps4' in key)
                or ('dafny4/UnionFind.dfy' in key and '.Join ' in key)}
    report['suite'][variant] = {'programs': len(rows), 'batches': len(batches),
        'total_RU': sum(v[1] for v in batches.values()), 'logs_unavailable': sorted(missing),
        'selected_batches': selected}
    path = root / f'issue23-{variant}-std'
    assert (path / 'probe-exit.txt').read_text().strip() == '0', path
    lib = [line.split('\t') for line in (path / 'verdicts.tsv').read_text().splitlines()]
    report['library'][variant] = {'verdicts': {row[0]: row[1:4] for row in lib},
        'total_RU': sum(int(row[4]) for row in lib if not row[0].startswith('run '))}
    focused = root / f'issue23-{variant}-focused'
    assert (focused / 'probe-exit.txt').read_text().strip() == '0', focused
    report['focused'][variant] = json.loads((focused / 'results.json').read_text())
report['suite']['verdict_differences'] = {k: [verdicts['before'].get(k), verdicts['after'].get(k)]
    for k in verdicts['before'].keys() | verdicts['after'].keys()
    if verdicts['before'].get(k) != verdicts['after'].get(k)}
a, b = (report['library'][v]['verdicts'] for v in ['before', 'after'])
report['library']['verdict_differences'] = {k: [a.get(k), b.get(k)] for k in a.keys() | b.keys() if a.get(k) != b.get(k)}
a = root / 'issue23-before-focused'
b = root / 'issue23-after-focused'
assert len(list(a.glob('*.bpl'))) >= 13, 'Missing printed Boogie samples'
assert {p.name for p in a.glob('*.bpl')} == {p.name for p in b.glob('*.bpl')}, 'Different Boogie sample sets'
for p in sorted(a.glob('*.bpl')):
    if p.name == 'prelude.bpl':
        continue
    q = b / p.name
    report['boogie'].append({'sample': p.name, 'identical': p.read_bytes() == q.read_bytes(),
        'before_sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
        'after_sha256': hashlib.sha256(q.read_bytes()).hexdigest()})
print(json.dumps(report, indent=2))
assert not report['suite']['verdict_differences'], 'Suite verdict differences'
assert not report['library']['verdict_differences'], 'Library verdict differences'
assert report['boogie'] and all(row['identical'] for row in report['boogie']), 'Boogie differs'
