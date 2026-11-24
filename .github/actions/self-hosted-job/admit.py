"""Reserve headroom, concurrent growth and optional declared memory until the owning job exits.

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

PROBE_TIMEOUT_SECONDS = 5  # Owner identity probe; memory probes use the required policy.
STOPPED = 0
POLICY_KEYS = ('reserve_mb', 'default_peak_mb', 'peak_min_samples', 'peak_history_days',
               'probe_timeout_s', 'slot_poll_s', 'memory_backoff_s')


def identity(pid):
    r = subprocess.run(['ps', '-p', str(pid), '-o', 'lstart=,comm='], capture_output=True, text=True,
                       timeout=PROBE_TIMEOUT_SECONDS)
    return r.stdout.strip() if r.returncode == 0 else ''


def positive_number(value, name):
    if type(value) not in (int, float) or not math.isfinite(value) or value <= 0:
        raise ValueError(f'{name} must be positive and finite')
    return value


def validate_policy(policy):
    if not isinstance(policy, dict):
        raise ValueError('Admission policy must be an object')
    for name in POLICY_KEYS:
        value = policy.get(name)
        if name == 'memory_backoff_s':
            if not isinstance(value, list) or not value:
                raise ValueError('memory_backoff_s must be a nonempty JSON array')
            for delay in value:
                positive_number(delay, name)
        elif name == 'peak_min_samples':
            positive_integer(value, name)
        else:
            positive_number(value, name)
    return policy


def probe(command, timeout):
    try:
        r = subprocess.run(command, capture_output=True, text=True, timeout=timeout)
        return r.stdout.strip() if r.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None


def free_percent(timeout):
    text = probe(['memory_pressure', '-Q'], timeout)
    match = re.search(r'System-wide memory free percentage:\s*(\d+)%', text or '')
    return int(match[1]) if match and 0 <= int(match[1]) <= 100 else None


def pressure_level(timeout):
    text = probe(['sysctl', '-n', 'kern.memorystatus_vm_pressure_level'], timeout)
    return int(text) if text in ('1', '2', '4') else None


def physical_mb(timeout):
    text = probe(['sysctl', '-n', 'hw.memsize'], timeout)
    return int(text) / 1048576 if text and text.isdigit() and int(text) > 0 else None


def memory_healthy(policy, expected_mb, outstanding_mb):
    timeout = policy['probe_timeout_s']
    free, total, level = free_percent(timeout), physical_mb(timeout), pressure_level(timeout)
    available = free * total / 100 if free is not None and total is not None else None
    # Free memory already excludes current RSS. Reserve only future growth, otherwise
    # jobs admitted before allocating their peak can spend the same free memory twice:
    # available_mb - expected_mb - sum(max(0, expected_j - RSS_j)) >= reserve_mb.
    # Unknown RSS charges the full peak. The declared budget remains a separate cap.
    healthy = (available is not None and level in (1, 2)
               and available - expected_mb - outstanding_mb >= policy['reserve_mb'])
    detail = (f'available_mb={available} expected_mb={expected_mb} outstanding_mb={outstanding_mb} '
              f'reserve_mb={policy["reserve_mb"]} pressure={level}')
    return healthy, detail


def history_records(path):
    """The runner supplies a file path; daily rotations sit alongside that file."""
    if not path:
        return []
    path = Path(path)
    records = []
    try:
        rotations = sorted(file for file in path.parent.glob(path.name + '.*')
                           if re.fullmatch(re.escape(path.name) + r'\.\d{8}', file.name))
    except OSError:
        rotations = []
    # Match the sampler: rotations first, current file last for deduplication.
    for file in rotations + [path]:
        try:
            with file.open() as stream:
                for line in stream:
                    try:
                        record = json.loads(line)
                        if isinstance(record, dict):
                            records.append(record)
                    except ValueError:
                        continue
        except (OSError, UnicodeError):
            continue
    return records


def expected_peak(project, kind, policy, caller=None, now=None):
    if caller is not None:
        return {'mb': positive_number(caller, 'expected_peak_mb'), 'source': 'caller', 'n': 0}
    now = time.time() if now is None else now
    since = now - policy['peak_history_days'] * 86400
    ended = {}
    for row in history_records(os.environ.get('ADMISSION_RSS_HISTORY')):
        t, samples, peak = row.get('t'), row.get('samples'), row.get('peak_mb')
        if (row.get('event') == 'end' and isinstance(row.get('key'), str)
                and type(t) in (int, float) and since <= t <= now
                and type(samples) in (int, float) and samples > 0
                and type(peak) in (int, float) and math.isfinite(peak) and peak > 0):
            ended[row['key']] = row
    groups = []
    if project and kind:
        groups.append(('project/kind p95', [r['peak_mb'] for r in ended.values()
                                          if r.get('project') == project and r.get('kind') == kind]))
    if kind:
        groups.append(('kind p95', [r['peak_mb'] for r in ended.values() if r.get('kind') == kind]))
    elif project:
        groups.append(('project p95', [r['peak_mb'] for r in ended.values() if r.get('project') == project]))
    for source, values in groups:
        if len(values) >= policy['peak_min_samples']:
            values.sort()
            return {'mb': values[round(0.95 * (len(values) - 1))], 'source': source, 'n': len(values)}
    return {'mb': policy['default_peak_mb'], 'source': 'default', 'n': 0}


def process_tree_mb(pid, timeout):
    """Measure the recorded owner tree, accepting older descendant roots too."""
    text = probe(['ps', '-axo', 'pid=,ppid=,rss=,comm='], timeout)
    if text is None:
        return None
    rows = {}
    try:
        for line in text.splitlines():
            process, parent, rss, command = line.split(None, 3)
            process, parent, rss = int(process), int(parent), int(rss)
            if process <= 0 or parent < 0 or rss < 0:
                return None
            rows[process] = (parent, rss, command)
    except ValueError:
        return None
    if pid not in rows:
        return None
    root, ancestor, seen = pid, pid, set()
    while ancestor in rows and ancestor not in seen:
        seen.add(ancestor)
        if Path(rows[ancestor][2]).name == 'Runner.Worker':
            root = ancestor
            break
        ancestor = rows[ancestor][0]
    descendants = {root}
    while True:
        added = {p for p, row in rows.items() if row[0] in descendants} - descendants
        if not added:
            break
        descendants.update(added)
    return sum(rows[p][1] for p in descendants) / 1024


class Backoff:
    def __init__(self, policy):
        self.policy, self.index = policy, 0

    def delay(self, reason):
        if reason.split(':', 1)[0] in ('slots', 'mutex', 'unaccounted'):
            self.index = 0
            return self.policy['slot_poll_s']
        delays = self.policy['memory_backoff_s']
        delay = delays[self.index]
        self.index = min(self.index + 1, len(delays) - 1)
        return delay


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
    return slots, budget, entry['name'], amount


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


def publish(stream, project, kind, amount, expected_mb, owner_pid):
    data = {'pid': owner_pid, 'project': project, 'kind': kind or 'unknown',
            'declared_mb': amount, 'expected_mb': expected_mb, 'started': time.time()}
    path = sidecar_path(stream)
    temporary = path.with_name(f'{path.name}.{os.getpid()}.tmp')
    try:
        temporary.write_text(json.dumps(data, sort_keys=True) + '\n')
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def release(stream):
    # Keep unlink-before-unlock: deleting after unlock could erase a successor's
    # sidecar. Scanners treat the brief unaccounted interval as a slot wait.
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


def pool_usage(directory, candidate, timeout):
    total, outstanding, unaccounted = 0, 0, []
    for path in sorted(directory.iterdir()):
        if path == Path(candidate) or not (re.fullmatch(r'active\.lock\.[0-9]+', path.name)
                                          or path.name == 'agent-reserve.lock'):
            continue
        reserve = path.name == 'agent-reserve.lock'
        if not reserve:
            probe = try_lock(path)
            if probe is not None:
                probe.close()
                continue  # Free numbered locks' stale metadata never reserves memory.
        # Never acquire the agent reserve, even to test it. Read its metadata
        # without locking; a live sidecar conservatively counts until removed.
        try:
            data = json.loads(Path(str(path) + '.json').read_text())
            if not isinstance(data, dict):
                raise ValueError('Metadata must be an object')
            positive_integer(data.get('pid'), 'pid')
            amount = positive_integer(data.get('declared_mb'), 'declared_mb')
            positive_number(data.get('expected_mb', amount), 'expected_mb')
            if (not isinstance(data.get('project'), str) or not data['project']
                    or not isinstance(data.get('kind'), str) or not data['kind']
                    or type(data.get('started')) not in (int, float)
                    or not math.isfinite(data['started']) or data['started'] <= 0):
                raise ValueError('Metadata must describe a live holder')
            if not pid_alive(data['pid']):
                if reserve:
                    continue  # Dead reserve metadata has no live reservation.
                raise ValueError('Metadata must describe a live holder')
        except FileNotFoundError:
            if not reserve:
                unaccounted.append(str(path))
            continue
        except (OSError, ValueError, TypeError, OverflowError):
            unaccounted.append(str(path))
            continue
        total += amount
        expected = positive_number(data.get('expected_mb', amount), 'expected_mb')
        # Growth uses exactly the expected peak checked for this job at admission.
        # Registered declarations continue to count separately against the budget;
        # older sidecars without expected_mb use their declaration consistently.
        peak = expected
        actual = process_tree_mb(data['pid'], timeout)
        outstanding += peak if actual is None else max(0, peak - actual)
    return total, outstanding, unaccounted


def try_admit(lock, slots, repository, kind, policy, caller=None, report_peak=print, *, owner_pid):
    validate_policy(policy)
    positive_integer(slots, 'Admission slots')
    positive_integer(owner_pid, 'Owner PID')
    # Configuration and paths are deliberately read on every attempt, before locking.
    settings = configuration(repository, kind)
    if 'ADMISSION_LOCK_DIR' in os.environ and not os.environ['ADMISSION_LOCK_DIR']:
        raise ValueError('ADMISSION_LOCK_DIR must not be empty')
    base = Path(os.environ['ADMISSION_LOCK_DIR']) / 'active.lock' if 'ADMISSION_LOCK_DIR' in os.environ else Path(lock)
    base.parent.mkdir(parents=True, exist_ok=True)
    project = os.environ.get('ADMISSION_PROJECT') or (settings[2] if settings else repository)
    peak = expected_peak(project, kind, policy, caller)
    report_peak(f'Admission peak: project={project} kind={kind} expected_mb={peak["mb"]} '
          f'source={peak["source"]} n={peak["n"]}', flush=True)
    amount = math.ceil(peak['mb'])
    if settings is not None:
        cap, budget, _, amount = settings
        slots = min(slots, cap)
    mutex = try_lock(base.parent / 'budget.lock')
    if mutex is None:
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
        total, outstanding, unaccounted = pool_usage(base.parent, stream.name, policy['probe_timeout_s'])
        if unaccounted:
            return None, 'unaccounted: ' + ', '.join(Path(path).name for path in unaccounted)
        if settings is not None and total + amount > budget:
            return None, 'budget'
        healthy, detail = memory_healthy(policy, peak['mb'], outstanding)
        if not healthy:
            return None, 'memory: ' + detail
        publish(stream, project, kind, amount, peak['mb'], owner_pid)
        admitted = True
        return stream, None
    finally:
        # Both locks leave the transaction on every refusal, including probe/publish errors.
        if stream is not None and not admitted:
            stream.close()
        if mutex is not None:
            mutex.close()


def hold(lock, ready, pid, expected, slots, policy, kind='', repository='', caller=None):
    positive_integer(slots, 'Admission slots')
    validate_policy(policy)
    if caller is not None:
        positive_number(caller, 'expected_peak_mb')
    if not expected:
        return
    previous = None
    previous_peak = None

    def report_peak(message, **kwargs):
        nonlocal previous_peak
        if message != previous_peak:
            print(message, **kwargs)
            previous_peak = message

    backoff = Backoff(policy)
    while not STOPPED and identity(pid) == expected:
        stream, reason = try_admit(lock, slots, repository, kind, policy, caller, report_peak, owner_pid=pid)
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
        if reason != previous:
            print(f'Waiting for admission: {reason}', flush=True)
            previous = reason
        deadline = time.monotonic() + backoff.delay(reason)
        # No lock is retained; cancellation stays active even during long backoffs.
        while True:
            if STOPPED or identity(pid) != expected:
                return
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                break
            time.sleep(min(1, remaining))


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
        hold(args[0], args[1], int(args[2]), args[3], int(args[4]),
             json.loads(args[5]), args[6], args[7], json.loads(args[8]) if args[8] else None)
        if STOPPED:
            sys.exit(128 + STOPPED)
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        detail = str(error) if isinstance(error, ValueError) else 'Cannot access host-level admission files or commands'
        print(f'Admission failed: {detail}', file=sys.stderr, flush=True)
        sys.exit(2)
