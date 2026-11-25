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
     "['tar', '-cf', temporary, '-C',", "['tar', '-cf', str(run / str(attempt) / (name + '.tar')), '-C',",
     'test_put_publishes_only_after_pack_finishes'),
    ('temporary leak', 'host-artifact.py', 'os.unlink(temporary)', 'pass',
     'test_failed_pack_preserves_archive_and_cleans_temporary'),
    ('wrong directory root', 'host-artifact.py',
     '(source, [p.name for p in source.iterdir()]) if source.is_dir()',
     '(source.parent, [source.name]) if source.is_dir()',
     'test_directory_layout_modes_and_hidden_files'),
    ('current attempt only', 'host-artifact.py',
     'range(attempt, 0, -1)', 'range(attempt, attempt - 1, -1)',
     'test_get_searches_earlier_attempts_per_name'),
    ('oldest attempt first', 'host-artifact.py', 'range(attempt, 0, -1)', 'range(1, attempt + 1)',
     'test_get_uses_newest_available_attempt'),
    ('future attempt allowed', 'host-artifact.py', 'range(attempt, 0, -1)', 'range(attempt + 1, 0, -1)',
     'test_get_does_not_read_future_attempt'),
    ('missing name succeeds', 'host-artifact.py',
     'raise FileNotFoundError(f\'Host artifact "{name}" is missing in this run and all earlier attempts; \'\n                                    \'a mixed pool may be configured: set HOST_ARTIFACT_DIR on every runner in the same apply\')',
     'return', 'test_missing_name_fails_without_fallback'),
    ('run isolation removed', 'host-artifact.py', 'run = repository / run_id', "run = repository / '100'",
     'test_isolates_repository_run_and_name'),
    ('no pruning', 'host-artifact.py', 'prune(repository, run)', 'pass',
     'test_pruning_respects_age_current_run_and_repository'),
    ('reversed age check', 'host-artifact.py', 'run.stat().st_mtime < cutoff', 'run.stat().st_mtime > cutoff',
     'test_pruning_respects_age_current_run_and_repository'),
    ('retention is one day', 'host-artifact.py', 'DEFAULT_RETENTION_DAYS = 3', 'DEFAULT_RETENTION_DAYS = 1',
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
    ('reader takes exclusive lock', 'host-artifact.py', 'fcntl.flock(lock, fcntl.LOCK_SH)',
     'fcntl.flock(lock, fcntl.LOCK_EX)', 'test_get_can_share_a_read_lock'),
    ('unreadable archive', 'host-artifact.py', 'os.chmod(temporary, 0o644)', 'pass',
     'test_archive_is_published_readable_with_restrictive_umask'),
    ('retention maximum lost', 'host-artifact.py',
     'max(recorded_retention(run) if existed else requested_retention,\n                              requested_retention)',
     'requested_retention', 'test_run_retention_records_maximum_across_attempts'),
    ('recorded retention ignored', 'host-artifact.py', 'recorded_retention(run) * 24',
     'DEFAULT_RETENTION_DAYS * 24', 'test_run_retention_records_maximum_across_attempts'),
    ('prune failure fails put', 'host-artifact.py',
     'print(f\'::warning::host-artifact: prune failed after publication: {error}\')',
     'raise', 'test_prune_failure_warns_after_successful_publish'),
    ('hidden default changed', 'action.yml', "default: 'false'", "default: 'true'",
     'test_unset_or_empty_fallback'),
    ('fallback retention hardcoded', 'action.yml', 'retention-days: ${{ inputs.retention-days }}',
     'retention-days: 3', 'test_unset_or_empty_fallback'),
    ('hidden files always packed', 'host-artifact.py',
     "excludes = [] if include_hidden else ['--exclude=.*']", 'excludes = []',
     'test_hidden_files_are_excluded_by_default'),
    ('mixed pool hint missing', 'host-artifact.py', 'a mixed pool may be configured',
     'configuration differs', 'test_mixed_pool_error_and_rollout_note'),
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
