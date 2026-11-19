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


def hold(lock, ready, pid, expected, slots=2):
    if slots < 1:
        raise ValueError('Admission slots must be positive')
    # Every runner uses the same numbered locks. Keep exactly one descriptor open
    # while admitted; the OS releases that slot even after hard cancellation.
    while identity(pid) == expected:
        for number in range(slots):
            stream = open(f'{lock}.{number}', 'a')
            try:
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                stream.close()
                continue
            with stream:
                if identity(pid) != expected:
                    return
                Path(ready).touch()
                while identity(pid) == expected:
                    time.sleep(1)
            return
        # Check owner identity while queued too, so a cancelled waiter exits.
        time.sleep(1)


if __name__ == '__main__':
    hold(sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4],
         int(sys.argv[5]) if len(sys.argv) > 5 else 2)
