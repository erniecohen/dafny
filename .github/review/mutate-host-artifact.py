#!/usr/bin/env python3
"""Run serial mutations in disposable copies, leaving the action untouched."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

ACTION = Path(__file__).resolve().parents[1] / 'actions/host-artifact'
TEST = Path(__file__).with_name('test_host_artifact.py')
MUTATIONS = [
    ('no publication', 'host-artifact.py',
     "os.replace(temporary, destination / (name + '.tar'))", 'pass', 'test_put_get_file'),
    ('non-atomic pack', 'host-artifact.py',
     "['tar', '-cf', temporary,", "['tar', '-cf', str(destination / (name + '.tar')),",
     'test_put_publishes_only_after_pack_finishes'),
    ('temporary leak', 'host-artifact.py', 'os.unlink(temporary)', 'pass',
     'test_failed_pack_preserves_archive_and_cleans_temporary'),
    ('wrong directory root', 'host-artifact.py',
     "(source, '.') if source.is_dir()", '(source.parent, source.name) if source.is_dir()',
     'test_directory_layout_modes_and_hidden_files'),
    ('current attempt only', 'host-artifact.py',
     'range(attempt, 0, -1)', 'range(attempt, attempt - 1, -1)',
     'test_get_searches_earlier_attempts_per_name'),
    ('oldest attempt first', 'host-artifact.py', 'range(attempt, 0, -1)', 'range(1, attempt + 1)',
     'test_get_uses_newest_available_attempt'),
    ('future attempt allowed', 'host-artifact.py', 'range(attempt, 0, -1)', 'range(attempt + 1, 0, -1)',
     'test_get_does_not_read_future_attempt'),
    ('missing name succeeds', 'host-artifact.py',
     'raise FileNotFoundError(f\'Host artifact "{name}" is missing in this run and all earlier attempts\')',
     'return', 'test_missing_name_fails_without_fallback'),
    ('run isolation removed', 'host-artifact.py', 'run = repository / run_id', "run = repository / '100'",
     'test_isolates_repository_run_and_name'),
    ('no pruning', 'host-artifact.py', 'prune(repository, run)', 'pass',
     'test_pruning_respects_age_current_run_and_repository'),
    ('reversed age check', 'host-artifact.py', 'run.stat().st_mtime < cutoff', 'run.stat().st_mtime > cutoff',
     'test_pruning_respects_age_current_run_and_repository'),
    ('retention is one day', 'host-artifact.py', 'RETENTION_SECONDS = 3 *', 'RETENTION_SECONDS = 1 *',
     'test_pruning_respects_age_current_run_and_repository'),
    ('no run refresh', 'host-artifact.py', 'os.utime(run, None)', 'pass',
     'test_pruning_respects_age_current_run_and_repository'),
    ('no reader/pruner lock', 'host-artifact.py', 'fcntl.flock(lock, fcntl.LOCK_EX)', 'pass',
     'test_pruning_waits_for_entire_get'),
    ('no fallback selection', 'host-artifact.py', "backend = 'host' if root else 'github'", "backend = 'host'",
     'test_unset_or_empty_fallback'),
    ('fallback upload disabled', 'action.yml', "inputs.mode == 'put'", "inputs.mode == 'get'",
     'test_unset_or_empty_fallback'),
    ('fallback download disabled', 'action.yml', "inputs.mode == 'get'", "inputs.mode == 'put'",
     'test_unset_or_empty_fallback'),
]


def main():
    for label, filename, before, after, test in MUTATIONS:
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / 'action'
            shutil.copytree(ACTION, candidate)
            path = candidate / filename
            source = path.read_text()
            if source.count(before) != 1:
                raise RuntimeError(f'{label}: expected one mutation site')
            path.write_text(source.replace(before, after, 1))
            result = subprocess.run([sys.executable, str(TEST), f'HostArtifactTests.{test}'],
                                    env=dict(os.environ, HOST_ARTIFACT_TEST_ACTION=str(candidate)),
                                    capture_output=True, text=True, timeout=25)
            if result.returncode == 0:
                raise RuntimeError(f'Mutation survived: {label}')
            if 'FAIL:' not in result.stderr:
                raise RuntimeError(f'Mutation did not reach an assertion: {label}\n{result.stderr}')
            print(f'Caught: {label}')
    print(f'{len(MUTATIONS)} of {len(MUTATIONS)} mutations caught')


if __name__ == '__main__':
    main()
