#!/usr/bin/env python3
"""Recording lifetime controls, exact task exchanges and persistent report identity."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import zlib


def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def contents(directory):
    """All small-tier records and large-tier payloads; only module timing is variable.

    Blob addresses of module reports necessarily vary with RunTimeTicks. Compare
    every other field, plus the table/key membership, rather than those addresses.
    Access timestamps and partition holds are eviction metadata, not payloads.
    """
    root = Path(directory) / 'v1'
    result = {}
    for table in ('results', 'module-results', 'solver-identities', 'image-digests'):
        for path in sorted((root / table).glob('*/*/data')):
            entry = json.loads(path.read_text())
            data = (root / 'blobs/sha256' / entry['Blob'][:2] / entry['Blob'] / 'data').read_bytes()
            payload = zlib.decompress(data[10:]) if data.startswith(b'\0dcz') else data
            assert len(payload) == entry['Length'] and hashlib.sha256(payload).hexdigest() == entry['Blob']
            assert entry['Schema'] == 1 and entry['Table'] == table and entry['Key'] == path.parent.name
            if table == 'module-results':
                record = json.loads(payload)
                for task in record['Tasks']:
                    task['RunTimeTicks'] = 0
                payload = json.dumps(record, sort_keys=True, separators=(',', ':')).encode()
            result[(table, entry['Key'])] = payload
    for table in ('resolutions', 'translations', 'preparations'):
        for path in sorted((root / 'artifacts' / table).glob('*/*')):
            if path.name.startswith('.'):
                continue
            # Format includes a checksum, table and key; exact bytes compare all of them.
            data = path.read_bytes()
            assert data[:4] == b'DART' and hashlib.sha256(data[:-32]).digest() == data[-32:]
            result[('artifacts/' + table, path.name)] = data
    assert result, 'empty cache comparison'
    return result


def main():
    exe, solver, source, prefix, destination = sys.argv[1:]
    destination = Path(destination).resolve()
    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)
    oracle = load('batch-gc')
    bpl = load('translation-workers')
    reference = oracle.records(json.loads(Path(prefix + '.json').read_text()))
    digest = hashlib.sha256(Path(solver).read_bytes()).hexdigest()
    env = {k: v for k, v in os.environ.items() if k not in ('DAFNY_CACHE_DIR', 'DAFNY_CACHE_PARTITION')}
    cold = {}

    def run(name, cache, release, audit=False, capture=False):
        out = destination / name
        args = [shutil.which('dotnet'), exe, 'verify', source, '--cores', '2', '--type-system-refresh',
                '--solver-path', solver, '--verification-time-limit', '60', '--show-snippets:false',
                '--boogie', '/deterministicLiteralHashes:1', '--boogie', '/restartProver',
                '--pipeline-profile', str(out) + '.jsonl', '--log-format', 'json;LogFileName=' + str(out) + '.json']
        if cache:
            args += ['--verification-cache', str(cache), '--verification-cache-solver-digest', digest, '--persistent-translation']
        if release is not None:
            args += ['--release-cache-recordings:' + str(release).lower()]
        if audit:
            args += ['--persistent-translation-audit']
        if capture:
            args += ['--bprint', str(out) + '.bpl']
        process = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env, timeout=110)
        out.with_suffix('.out').write_bytes(process.stdout + process.stderr)
        assert process.returncode == 4, (name, process.returncode, process.stdout, process.stderr)
        events = [json.loads(line) for line in out.with_suffix('.jsonl').read_text().splitlines()]
        summary = next(e for e in events if e['ev'] == 'summary')
        assert summary['tasksCompleted'] == summary['tasksSubmitted'] == len(reference) > 0
        assert summary['tasksUnfinished'] == 0
        observed = oracle.records(json.loads(out.with_suffix('.json').read_text()))
        oracle.compare(reference, observed)
        counters = next((e for e in events if e['ev'] == 'result-cache'), None)
        if cache:
            assert counters is not None
            if release:
                assert counters['releasedRecordings'] > 0, 'option did not release any completed recordings'
                assert counters['releasedReporters'] > 0, 'option did not release any completed split reporters'
            else:
                assert counters['releasedRecordings'] == 0, 'unrequested release'
                assert counters['releasedReporters'] == 0, 'unrequested split reporter release'
        else:
            assert counters is None
        if name == 'warm':
            assert summary['tasksCached'] > 0 and counters['solverStarts'] > 0, 'valid replay and fresh false assertion required'
        else:
            assert summary['tasksCached'] == 0
        if capture:
            assert bpl.modules(prefix) == bpl.modules(str(out)) and len(bpl.modules(prefix)) >= 2, 'Boogie text changed'
        return observed

    for name, release in [('off', None), ('on', True), ('explicit-off', False)]:
        cache = destination / (name + '-cache')
        run(name, cache, release)
        cold[name] = contents(cache)
    assert cold['off'] == cold['on'] == cold['explicit-off'], 'cache keys or contents changed'
    assert sum(k[0] == 'results' for k in cold['on']) >= 2
    assert sum(k[0] == 'module-results' for k in cold['on']) >= 2
    run('warm', destination / 'on-cache', True)
    run('audit', destination / 'on-cache', True, audit=True)
    assert cold['on'] == contents(destination / 'on-cache'), 'replay or audit changed stored evidence'
    run('capture', None, True, capture=True)
    # Equality-oracle negative controls: changed and empty observations are rejected.
    for changed in ({}, {**reference, next(iter(reference)): ('Invalid', -1, [])}):
        try:
            oracle.compare(reference, changed)
        except AssertionError:
            pass
        else:
            raise AssertionError('identity oracle accepted missing or changed evidence')
    print('Recording release: activated cold, warm and audit; default and explicit false unchanged')
    print('Identical obligations, verdicts, resources, assertion reports and cache payloads (except module timing)')
    print('Positive proofs, fresh failing false assertion, nonempty replay and equality-oracle negative controls passed')


if __name__ == '__main__':
    main()
