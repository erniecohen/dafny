#!/usr/bin/env python3
"""Require exercised bounded GC and exact fresh and pooled verification identity."""
import importlib.util
import json
from pathlib import Path
import sys

spec = importlib.util.spec_from_file_location('batch_gc', Path(__file__).with_name('batch-gc.py'))
batch_gc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch_gc)


def check_profile(events, bounded, mode='Batch'):
    count = batch_gc.check_profile(events, mode)
    policies = [e for e in events if e.get('ev') == 'verification-gc']
    if bounded:
        manifest = next(e for e in events if e.get('ev') == 'manifest')
        assert manifest['serverGC'], 'server collector was not activated'
        assert len(policies) == 1, 'one bounded runtime policy required'
        policy = policies[0]
        assert 1 <= policy['heapCount'] <= policy['maxHeapCount'] <= 2, 'collector heap count was not bounded'
        assert policy['heapHardLimitBytes'] == 1024 * 1024 * 1024, 'managed heap limit was not applied'
    else:
        assert not policies, 'bounded policy was enabled without opting in'
    return count


def main(prefixes):
    assert len(prefixes) == 7, 'seven comparison runs required'
    records = []
    counts = []
    for index, prefix in enumerate(prefixes):
        events = [json.loads(line) for line in Path(prefix + '.jsonl').read_text().splitlines()]
        count = check_profile(events, index in (2, 4, 6), 'Interactive' if index == 0 else 'Batch')
        observed = batch_gc.records(json.loads(Path(prefix + '.json').read_text()))
        assert len(observed) == count, 'profile and solver log coverage differ'
        records.append(observed)
        counts.append(count)
    for observed in records[1:5]:
        batch_gc.compare(records[0], observed)
    batch_gc.compare(records[5], records[6])
    print('Bounded server GC: three activated runs, four unchanged runs')
    print('Identical verdicts, resources and assertions: %d VCs, 5 fresh-prover runs; %d VCs, 2 pooled-prover runs'
          % (counts[0], counts[5]))


if __name__ == '__main__':
    main(sys.argv[1:])
