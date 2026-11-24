"""Reserve a job slot and optional declared memory until the owning job exits.

Budget mode uses host-level configuration supplied by the runner environment.
Every attempt reads that configuration anew; admitted jobs keep their reservation.
"""
import fcntl
import json
import math
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import time

# kern.memorystatus_vm_pressure_level: 1 normal, 2 warn, 4 critical.
PRESSURE = {'normal': 1, 'warn': 2, 'critical': 4}
MEMORY_POLL_SECONDS = 10
PROBE_TIMEOUT_SECONDS = 5
STOPPED = 0


def identity(pid):
    r = subprocess.run(['ps', '-p', str(pid), '-o', 'lstart=,comm='], capture_output=True, text=True,
                       timeout=PROBE_TIMEOUT_SECONDS)
    return r.stdout.strip() if r.returncode == 0 else ''


def free_percent():
    try:
        r = subprocess.run(['memory_pressure', '-Q'], capture_output=True, text=True,
                           timeout=PROBE_TIMEOUT_SECONDS)
    except (OSError, subprocess.TimeoutExpired):
        return None
    m = re.search(r'free percentage:\s*(\d+)%', r.stdout)
    return int(m.group(1)) if r.returncode == 0 and m and int(m.group(1)) <= 100 else None


def pressure_level():
    try:
        r = subprocess.run(['sysctl', '-n', 'kern.memorystatus_vm_pressure_level'],
                           capture_output=True, text=True, timeout=PROBE_TIMEOUT_SECONDS)
    except (OSError, subprocess.TimeoutExpired):
        return None
    value = r.stdout.strip()
    return int(value) if r.returncode == 0 and value.isdigit() and int(value) in PRESSURE.values() else None


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
    if floor is not None:
        free = free_percent()
        if free is None or free < floor:
            return False
    if ceiling is not None:
        level = pressure_level()
        if level is None or level > ceiling:
            return False
    return True


def positive_integer(value, name):
    if type(value) is not int or value <= 0:
        raise ValueError(f'{name} must be a positive integer')
    return value


def declared_mb(entry, kind):
    peaks = entry.get('peak_mb')
    if not isinstance(peaks, dict):
        return positive_integer(peaks, 'peak_mb')
    values = {key: positive_integer(value, f'peak_mb.{key}')
              for key, value in peaks.items() if not key.startswith('_')}
    if not values:
        raise ValueError('peak_mb names no job kind')
    return values[kind] if kind in values else max(values.values())


def configuration(repository, kind):
    """None only when neither budget configuration variable is present."""
    names = ('ADMISSION_RUNNERS_JSON', 'ADMISSION_PROJECTS_DIR')
    if not any(name in os.environ for name in names):
        return None
    if not all(os.environ.get(name) for name in names):
        raise ValueError('ADMISSION_RUNNERS_JSON and ADMISSION_PROJECTS_DIR must both name host-level configuration')
    data = json.loads(Path(os.environ[names[0]]).read_text())
    if not isinstance(data, dict):
        raise ValueError('Admission configuration must be a JSON object')
    budget = positive_integer(data.get('studio_ci_budget_mb'), 'studio_ci_budget_mb')
    slots = positive_integer(data.get('admission_slots'), 'admission_slots')
    memory = data.get('admission_memory', {})
    if not isinstance(memory, dict):
        raise ValueError('admission_memory must be an object')
    floor = memory.get('min_free_percent', 40)
    ceiling = memory.get('max_pressure', 'normal')
    if type(floor) is not int or not 0 <= floor <= 100:
        raise ValueError('min_free_percent must be an integer from 0 to 100')
    if not isinstance(ceiling, str) or ceiling not in PRESSURE:
        raise ValueError('max_pressure must be normal, warn or critical')
    directory = Path(os.environ[names[1]])
    if not directory.is_dir():
        raise ValueError('ADMISSION_PROJECTS_DIR must name a directory')
    matches = []
    for path in sorted(directory.glob('*.json')):
        entry = json.loads(path.read_text())
        if (not isinstance(entry, dict) or not isinstance(entry.get('name'), str) or not entry['name']
                or not isinstance(entry.get('repos', []), list)
                or any(not isinstance(repo, str) or not repo for repo in entry.get('repos', []))):
            raise ValueError(f'Invalid project registration: {path.name}')
        if repository and repository in entry.get('repos', []):
            matches.append(entry)
    if len(matches) != 1:
        raise ValueError('Repository must match exactly one project registration')
    entry = matches[0]
    amount = declared_mb(entry, kind)
    if amount > budget:
        raise ValueError(f'Request of {amount} MB exceeds entire admission budget of {budget} MB')
    return slots, floor, PRESSURE[ceiling], budget, entry['name'], amount


def try_lock(path):
    stream = open(path, 'a')
    try:
        os.set_inheritable(stream.fileno(), False)
        fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        return stream
    except BlockingIOError:
        stream.close()
        return None
    except BaseException:
        stream.close()
        raise


def sidecar_path(stream):
    return Path(str(stream.name) + '.json')


def publish(stream, project, kind, amount):
    data = {'pid': os.getpid(), 'project': project, 'kind': kind or 'unknown',
            'declared_mb': amount, 'started': time.time()}
    path = sidecar_path(stream)
    temporary = path.with_name(f'{path.name}.{os.getpid()}.tmp')
    try:
        temporary.write_text(json.dumps(data, sort_keys=True) + '\n')
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def release(stream):
    try:
        sidecar_path(stream).unlink(missing_ok=True)
    finally:
        stream.close()


def pid_alive(pid):
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def pool_usage(directory, candidate):
    total, unaccounted = 0, []
    for path in sorted(directory.iterdir()):
        if path == Path(candidate) or not (re.fullmatch(r'active\.lock\.[0-9]+', path.name)
                                          or path.name == 'agent-reserve.lock'):
            continue
        probe = try_lock(path)
        if probe is not None:
            probe.close()
            continue  # Free locks' stale metadata never reserves memory.
        try:
            data = json.loads(Path(str(path) + '.json').read_text())
            if not isinstance(data, dict):
                raise ValueError('Metadata must be an object')
            positive_integer(data.get('pid'), 'pid')
            amount = positive_integer(data.get('declared_mb'), 'declared_mb')
            if (not isinstance(data.get('project'), str) or not data['project']
                    or not isinstance(data.get('kind'), str) or not data['kind']
                    or type(data.get('started')) not in (int, float)
                    or not math.isfinite(data['started']) or data['started'] <= 0
                    or not pid_alive(data['pid'])):
                raise ValueError('Metadata must describe a live holder')
        except (OSError, ValueError, TypeError, OverflowError):
            unaccounted.append(str(path))
            continue
        total += amount
    return total, unaccounted


def try_admit(lock, slots, repository, kind, floor, ceiling):
    # Configuration and paths are deliberately read on every attempt, before locking.
    settings = configuration(repository, kind)
    if 'ADMISSION_LOCK_DIR' in os.environ and not os.environ['ADMISSION_LOCK_DIR']:
        raise ValueError('ADMISSION_LOCK_DIR must not be empty')
    base = Path(os.environ['ADMISSION_LOCK_DIR']) / 'active.lock' if 'ADMISSION_LOCK_DIR' in os.environ else Path(lock)
    base.parent.mkdir(parents=True, exist_ok=True)
    if settings is not None:
        cap, floor, ceiling, budget, project, amount = settings
        slots = min(slots, cap)
    mutex = try_lock(base.parent / 'budget.lock') if settings is not None else None
    if settings is not None and mutex is None:
        return None, 'mutex'
    stream = None
    admitted = False
    try:
        for index in range(slots):
            stream = try_lock(f'{base}.{index}')
            if stream is not None:
                break
        if stream is None:
            return None, 'slots'
        if settings is not None:
            total, unaccounted = pool_usage(base.parent, stream.name)
            if unaccounted:
                return None, 'unaccounted: ' + ', '.join(Path(path).name for path in unaccounted)
            if total + amount > budget:
                return None, 'budget'
        if not memory_healthy(floor, ceiling):
            return None, 'memory'
        if settings is not None:
            publish(stream, project, kind, amount)
        admitted = True
        return stream, None
    finally:
        # Both locks leave the transaction on every refusal, including probe/publish errors.
        if stream is not None and not admitted:
            stream.close()
        if mutex is not None:
            mutex.close()


def hold(lock, ready, pid, expected, slots=2, min_free='', max_pressure='', kind='', repository=''):
    positive_integer(slots, 'Admission slots')
    floor, ceiling = thresholds(min_free, max_pressure)
    if not expected:
        return
    reported = set()
    backoff = 5
    while not STOPPED and identity(pid) == expected:
        if not any(name in os.environ for name in ('ADMISSION_RUNNERS_JSON', 'ADMISSION_PROJECTS_DIR')):
            if 'legacy' not in reported:
                print('Host-level admission configuration unset; using slots-only admission', flush=True)
                reported.add('legacy')
        stream, reason = try_admit(lock, slots, repository, kind, floor, ceiling)
        if stream is not None:
            try:
                if STOPPED or identity(pid) != expected:
                    return
                Path(ready).touch()
                while not STOPPED and identity(pid) == expected:
                    time.sleep(1)
            finally:
                release(stream)
            return
        category = reason.split(':', 1)[0]
        if category not in reported:
            print(f'Waiting for admission: {reason}', flush=True)
            reported.add(category)
        wait = 1
        if category in ('budget', 'memory', 'unaccounted'):
            wait = backoff if category != 'memory' or any(
                name in os.environ for name in ('ADMISSION_RUNNERS_JSON', 'ADMISSION_PROJECTS_DIR')) else MEMORY_POLL_SECONDS
            backoff = min(30, backoff * 2) if backoff < 30 else 60
        # Check owner identity throughout every wait. No lock is retained while sleeping.
        for _ in range(wait):
            if STOPPED or identity(pid) != expected:
                return
            time.sleep(1)


def stop(signum, frame):
    # Defer exit until the transaction finishes, so a signal cannot interrupt publication
    # or the transfer of the holder descriptor to hold()'s cleanup block.
    global STOPPED
    STOPPED = signum


if __name__ == '__main__':
    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    args = sys.argv[1:] + [''] * 9
    try:
        hold(args[0], args[1], int(args[2]), args[3], int(args[4]) if args[4] else 2,
             args[5], args[6], args[7], args[8])
        if STOPPED:
            sys.exit(128 + STOPPED)
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        detail = str(error) if isinstance(error, ValueError) else 'Cannot access host-level admission files or commands'
        print(f'Admission failed: {detail}', file=sys.stderr, flush=True)
        sys.exit(2)
