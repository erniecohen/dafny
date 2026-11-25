#!/usr/bin/env python3
"""Transport one file/directory using only Python's standard library and tar."""
import fcntl
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time

DEFAULT_RETENTION_DAYS = 3


def retention_days(value):
    if value == '':
        return DEFAULT_RETENTION_DAYS
    if not value.isdigit() or int(value) < 1:
        raise ValueError('retention-days must be a positive decimal integer or empty')
    return int(value)


def recorded_retention(run):
    record = run / '.retention-days'
    return retention_days(record.read_text().strip()) if record.exists() else DEFAULT_RETENTION_DAYS


def component(value):
    if not re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.-]*', value):
        raise ValueError('Artifact key components must contain only letters, digits, _, . and -')
    return value


def prune(repository, current_run):
    for run in repository.iterdir():
        if (run.name.isdigit() and run != current_run and run.is_dir()
                and not run.is_symlink()):
            cutoff = time.time() - recorded_retention(run) * 24 * 60 * 60
            if run.stat().st_mtime < cutoff:
                shutil.rmtree(run)


def transfer(mode, name, path, root):
    requested_retention = retention_days(os.environ.get('ARTIFACT_RETENTION_DAYS', ''))
    include_hidden = os.environ.get('ARTIFACT_INCLUDE_HIDDEN_FILES', 'false') == 'true'
    parts = os.environ['GITHUB_REPOSITORY'].split('/')
    if len(parts) != 2:
        raise ValueError('GITHUB_REPOSITORY must be owner/repository')
    repository = Path(root).joinpath(*(component(part) for part in parts))
    run_id = os.environ['GITHUB_RUN_ID']
    attempt_text = os.environ['GITHUB_RUN_ATTEMPT']
    if (not run_id.isdigit() or int(run_id) < 1 or not attempt_text.isdigit()
            or int(attempt_text) < 1):
        raise ValueError('Run ID and attempt must be positive decimal integers')
    attempt = int(attempt_text)
    run = repository / run_id
    repository.mkdir(parents=True, exist_ok=True)
    # Never remove the lock file: all jobs must lock the same inode. Readers
    # share it throughout extraction; publication and pruning exclude readers.
    with (repository / '.lock').open('a') as lock:
        if mode == 'put':
            source = Path(path).absolute()
            if not source.exists():
                raise FileNotFoundError('Artifact input file or directory is missing')
            # Pack on the same filesystem, without blocking readers or publishers.
            fd, temporary = tempfile.mkstemp(prefix='.put-', dir=repository)
            os.close(fd)
            try:
                parent, entries = ((source, [p.name for p in source.iterdir()]) if source.is_dir()
                                   else (source.parent, [source.name]))
                # tar preserves executable modes and nested files; a directory
                # contributes its contents, matching upload-artifact's root layout.
                environment = dict(os.environ, COPYFILE_DISABLE='1')
                # List the directory's children rather than '.' so excluding
                # hidden basenames cannot exclude the archive's root itself.
                excludes = [] if include_hidden else ['--exclude=.*']
                subprocess.run(['tar', '-cf', temporary, '-C', str(parent), *excludes,
                                '--', *entries] if entries else
                               ['tar', '-cf', temporary, '-T', os.devnull],
                               check=True, env=environment)
                os.chmod(temporary, 0o644)
                fcntl.flock(lock, fcntl.LOCK_EX)
                existed = run.exists()
                destination = run / str(attempt)
                destination.mkdir(parents=True, exist_ok=True)
                maximum = max(recorded_retention(run) if existed else requested_retention,
                              requested_retention)
                (run / '.retention-days').write_text(str(maximum) + '\n')
                os.utime(run, None)
                os.replace(temporary, destination / (name + '.tar'))
                try:
                    prune(repository, run)
                except (OSError, ValueError) as error:
                    print(f'::warning::host-artifact: prune failed after publication: {error}')
            finally:
                if os.path.exists(temporary):
                    os.unlink(temporary)
            print('host-artifact: stored on host')
        else:
            fcntl.flock(lock, fcntl.LOCK_SH)
            for previous in range(attempt, 0, -1):
                archive = run / str(previous) / (name + '.tar')
                if archive.is_file():
                    destination = Path(path)
                    destination.mkdir(parents=True, exist_ok=True)
                    subprocess.run(['tar', '-xf', str(archive.absolute()), '-C', str(destination)],
                                   check=True)
                    print(f'host-artifact: restored from host (attempt {previous})')
                    return
            raise FileNotFoundError(f'Host artifact "{name}" is missing in this run and all earlier attempts; '
                                    'a mixed pool may be configured: set HOST_ARTIFACT_DIR on every runner in the same apply')


def main():
    mode = os.environ['ARTIFACT_MODE']
    if mode not in ('put', 'get'):
        raise ValueError('Artifact mode must be put or get')
    name = component(os.environ['ARTIFACT_NAME'])
    path = os.environ.get('ARTIFACT_PATH', '.')
    root = os.environ.get('HOST_ARTIFACT_DIR', '')
    if root and not Path(root).is_absolute():
        raise ValueError('HOST_ARTIFACT_DIR must be an absolute directory shared by the runners')
    backend = 'host' if root else 'github'
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        output.write(f'backend={backend}\n')
    if not root:
        print(f'host-artifact: HOST_ARTIFACT_DIR is unset; using GitHub artifacts ({mode})')
        return
    print(f'host-artifact: using host transport ({mode})')
    transfer(mode, name, path, root)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f'::error::host-artifact: {error}', file=sys.stderr)
        sys.exit(1)
