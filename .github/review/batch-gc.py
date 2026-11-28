#!/usr/bin/env python3
"""Nonvacuous mode and per-VC identity controls for --batch-gc (issue 241)."""
import json
from pathlib import Path
import sys


def check_profile(events, mode):
    manifests = [e for e in events if e.get('ev') == 'manifest']
    assert len(manifests) == 1, 'one manifest required'
    assert manifests[0]['gcLatencyMode'] == mode, 'GC option was not applied as requested'
    summaries = [e for e in events if e.get('ev') == 'summary']
    assert len(summaries) == 1, 'one completed summary required'
    summary = summaries[0]
    tasks = [e for e in events if e.get('ev') == 'task']
    assert summary['tasksSubmitted'] == summary['tasksCompleted'] == len(tasks) > 0, 'incomplete or empty verification'
    assert summary['tasksUnfinished'] == summary['tasksCached'] == 0, 'fresh completed verification required'
    assert {'Valid', 'Invalid'} <= {e['outcome'] for e in tasks}, 'positive proof and failing false assertion required'
    return len(tasks)


def records(document):
    result = {}
    for declaration in document['verificationResults']:
        for vc in declaration['vcResults']:
            key = (declaration['name'], vc['vcNum'])
            assert key not in result, 'duplicate VC identity'
            result[key] = (vc['outcome'], vc['resourceCount'], vc['assertions'])
    assert result, 'empty verification log'
    assert {'Valid', 'Invalid'} <= {v[0] for v in result.values()}, 'positive and negative verdicts required'
    assert any(v[1] > 0 and v[2] for v in result.values()), 'real solver work required'
    return result


def compare(reference, observed):
    assert reference.keys() == observed.keys(), 'VC membership changed'
    assert reference == observed, 'per-VC verdict, resource count or assertion records changed'


def main(prefixes):
    assert len(prefixes) == 4, 'four comparison runs required'
    reference = None
    count = None
    for prefix, mode in zip(prefixes, ['Interactive', 'Batch', 'Interactive', 'Batch']):
        events = [json.loads(line) for line in Path(prefix + '.jsonl').read_text().splitlines()]
        count = check_profile(events, mode)
        observed = records(json.loads(Path(prefix + '.json').read_text()))
        assert len(observed) == count, 'profile and solver log coverage differ'
        if reference is None:
            reference = observed
        else:
            compare(reference, observed)
    print('GC modes: default Interactive, opt-in Batch, explicit false Interactive, project Batch')
    print('Identical verdicts, resources and assertions: %d VCs, %d runs' % (count, len(prefixes)))


if __name__ == '__main__':
    main(sys.argv[1:])
