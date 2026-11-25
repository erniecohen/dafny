"""Hold an OS advisory lock until the owning job exits or the post action stops us.

A job is admitted only when one of the numbered slots is free and, when the
optional thresholds are given, memory is healthy: the system-wide free
percentage is at least MIN_FREE and the kernel's pressure level is no higher
than MAX_PRESSURE. Memory is checked only at admission; an admitted job is never
stopped. An empty threshold drops that condition.
"""
import fcntl
import os
from pathlib import Path
import re
import subprocess
import sys
import time

# kern.memorystatus_vm_pressure_level: 1 normal, 2 warn, 4 critical.
PRESSURE = {'normal': 1, 'warn': 2, 'critical': 4}
MEMORY_POLL_SECONDS = 10


def identity(pid):
    r = subprocess.run(['ps', '-p', str(pid), '-o', 'lstart=,comm='], capture_output=True, text=True)
    return r.stdout.strip() if r.returncode == 0 else ''


def free_percent():
    r = subprocess.run(['memory_pressure', '-Q'], capture_output=True, text=True)
    m = re.search(r'free percentage:\s*(\d+)%', r.stdout)
    return int(m.group(1)) if r.returncode == 0 and m else None


def pressure_level():
    r = subprocess.run(['sysctl', '-n', 'kern.memorystatus_vm_pressure_level'], capture_output=True, text=True)
    return int(r.stdout.strip()) if r.returncode == 0 and r.stdout.strip().isdigit() else None


def thresholds(min_free='', max_pressure=''):
    floor = None
    if min_free != '':
        if not re.fullmatch(r'[0-9]+', min_free) or int(min_free) > 100:
            raise ValueError('Minimum free memory must be an integer percentage from 0 to 100')
        floor = int(min_free)
    if max_pressure != '' and max_pressure not in PRESSURE:
        raise ValueError(f'Maximum memory pressure must be one of {", ".join(PRESSURE)}')
    return floor, PRESSURE.get(max_pressure) if max_pressure else None


def memory_healthy(floor, ceiling):
    # An unreadable probe counts as unhealthy, so a job never starts on a reading it could not take.
    if floor is not None:
        free = free_percent()
        if free is None or free < floor:
            return False
    if ceiling is not None:
        level = pressure_level()
        if level is None or level > ceiling:
            return False
    return True


def hold(lock, ready, pid, expected, slots=2, min_free='', max_pressure=''):
    if slots < 1:
        raise ValueError('Admission slots must be positive')
    floor, ceiling = thresholds(min_free, max_pressure)
    # Every runner uses the same numbered locks. Keep exactly one descriptor open
    # while admitted; the OS releases that slot even after hard cancellation.
    while identity(pid) == expected:
        wait = 1
        for number in range(slots):
            stream = open(f'{lock}.{number}', 'a')
            try:
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                stream.close()
                continue
            # Take the slot first, then check memory, so waiters do not all pass the
            # check at once and pile in. Unhealthy memory releases the slot.
            if not memory_healthy(floor, ceiling):
                stream.close()
                wait = MEMORY_POLL_SECONDS
                break
            with stream:
                if identity(pid) != expected:
                    return
                Path(ready).touch()
                while identity(pid) == expected:
                    time.sleep(1)
            return
        # Check owner identity while queued too, so a cancelled waiter exits.
        for _ in range(wait):
            if identity(pid) != expected:
                return
            time.sleep(1)


if __name__ == '__main__':
    args = sys.argv[1:] + [''] * 7
    hold(args[0], args[1], int(args[2]), args[3], int(args[4]) if args[4] else 2, args[5], args[6])
