"""Hold an OS advisory lock until the owning job exits or the post action stops us."""
import fcntl
import os
from pathlib import Path
import subprocess
import sys
import time


def identity(pid):
    r = subprocess.run(['ps', '-p', str(pid), '-o', 'lstart=,comm='], capture_output=True, text=True)
    return r.stdout.strip() if r.returncode == 0 else ''


def hold(lock, ready, pid, expected):
    with open(lock, 'a') as stream:
        fcntl.flock(stream, fcntl.LOCK_EX)
        if identity(pid) != expected:
            return
        Path(ready).touch()
        while identity(pid) == expected:
            time.sleep(1)


if __name__ == '__main__':
    hold(sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4])
