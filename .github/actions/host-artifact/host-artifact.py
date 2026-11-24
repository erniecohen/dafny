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

RETENTION_SECONDS = 3 * 24 * 60 * 60


def component(value):
    if not re.fullmatch(r'[A-Za-z0-9_][A-Za-z0-9_.-]*', value):
        raise ValueError('Artifact key components must contain only letters, digits, _, . and -')
    return value


def prune(repository, current_run):
    cutoff = time.time() - RETENTION_SECONDS
    for run in repository.iterdir():
        if (run.name.isdigit() and run != current_run and run.is_dir()
                and not run.is_symlink() and run.stat().st_mtime < cutoff):
            shutil.rmtree(run)


def transfer(mode, name, path, root):
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
    # Never remove the lock file: all jobs must lock the same inode. Both the
    # archive read/extraction and pruning hold this lock for their entire operation.
    with (repository / '.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        if mode == 'put':
            source = Path(path).absolute()
            if not source.exists():
                raise FileNotFoundError('Artifact input file or directory is missing')
            destination = run / str(attempt)
            destination.mkdir(parents=True, exist_ok=True)
            fd, temporary = tempfile.mkstemp(prefix='.put-', dir=destination)
            os.close(fd)
            try:
                parent, entry = (source, '.') if source.is_dir() else (source.parent, source.name)
                # tar preserves executable modes and nested files; a directory
                # contributes its contents, matching upload-artifact's root layout.
                environment = dict(os.environ, COPYFILE_DISABLE='1')
                subprocess.run(['tar', '-cf', temporary, '-C', str(parent), entry],
                               check=True, env=environment)
                os.replace(temporary, destination / (name + '.tar'))
            finally:
                if os.path.exists(temporary):
                    os.unlink(temporary)
            os.utime(run, None)
            prune(repository, run)
            print('host-artifact: stored on host')
        else:
            for previous in range(attempt, 0, -1):
                archive = run / str(previous) / (name + '.tar')
                if archive.is_file():
                    destination = Path(path)
                    destination.mkdir(parents=True, exist_ok=True)
                    subprocess.run(['tar', '-xf', str(archive.absolute()), '-C', str(destination)],
                                   check=True)
                    print(f'host-artifact: restored from host (attempt {previous})')
                    return
            raise FileNotFoundError(f'Host artifact "{name}" is missing in this run and all earlier attempts')


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
