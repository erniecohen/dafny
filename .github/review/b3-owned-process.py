"""Exclusive bounded Linux process stages with subreaper and validated pidfds.

No PID-number signal or process-group kill. A cleanup signal always poisons the
stage. Each accepted control stage has exit zero, no observed descendant, no
signal, exited owned pidfds, reaped direct children and an empty direct-child set.
"""
import ctypes
import os
from pathlib import Path
import select
import signal
import subprocess
import time

MAX_REGISTERED = 512
MAX_CHILD_IDS = 4096
MAX_THREADS = 2048
MAX_TEXT = 65536
cleanup_active = False
current_fault = None


def record_cancellation(message):
    if current_fault is not None:
        current_fault(message)
    return cleanup_active


def bounded_text(path):
    with open(path, 'rb', buffering=0) as source:
        value = source.read(MAX_TEXT + 1)
    if len(value) > MAX_TEXT:
        raise RuntimeError('Owned proc state exceeded its bound')
    return value.decode('ascii')


def identity(pid):
    value = bounded_text('/proc/' + str(pid) + '/stat')
    end = value.rfind(')')
    if end < 0 or not value[:end + 1].startswith(str(pid) + ' ('):
        raise RuntimeError('Malformed process identity')
    fields = value[end + 2:].split()
    if len(fields) < 20:
        raise RuntimeError('Incomplete process identity')
    return {'pid': pid, 'parent': int(fields[1]), 'group': int(fields[2]),
            'session': int(fields[3]), 'startTime': int(fields[19])}


def child_ids(pid):
    # Threads may disappear during enumeration. The caller repeats an authoritative
    # coordinator scan after traversing owned descendants, covering reparenting.
    result = set()
    tasks = list(Path('/proc/' + str(pid) + '/task').iterdir())
    if len(tasks) > MAX_THREADS:
        raise RuntimeError('Owned process thread count exceeded its bound')
    for task in tasks:
        try:
            value = bounded_text(task / 'children')
        except (FileNotFoundError, ProcessLookupError):
            continue
        for item in value.split():
            result.add(int(item))
            if len(result) > MAX_CHILD_IDS:
                raise RuntimeError('Owned direct-child list exceeded its bound')
    return sorted(result)


def exited(fd):
    poller = select.poll()
    poller.register(fd, select.POLLIN)
    return bool(poller.poll(0))


def enable_subreaper():
    if not hasattr(os, 'pidfd_open') or not hasattr(signal, 'pidfd_send_signal'):
        raise RuntimeError('Linux pidfd APIs are required')
    # This one-threaded coordinator owns its signal disposition and all wait calls.
    # Keep zombies available until this scope captures/reaps their exact identity.
    signal.signal(signal.SIGCHLD, signal.SIG_DFL)
    if signal.getsignal(signal.SIGCHLD) != signal.SIG_DFL:
        raise RuntimeError('Default SIGCHLD disposition is required')
    libc = ctypes.CDLL(None, use_errno=True)
    libc.prctl.argtypes = [ctypes.c_int, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_ulong]
    libc.prctl.restype = ctypes.c_int
    if libc.prctl(36, 1, 0, 0, 0) != 0:  # PR_SET_CHILD_SUBREAPER
        raise OSError(ctypes.get_errno(), 'Subreaper setup failed')
    state = ctypes.c_int()
    if libc.prctl(37, ctypes.addressof(state), 0, 0, 0) != 0 or state.value != 1:
        raise RuntimeError('Subreaper state was not established')
    if child_ids(os.getpid()):
        raise RuntimeError('Exclusive coordinator already owns children')
    return {'enabled': True, 'coordinator': identity(os.getpid()), 'initialDirectChildren': []}


def pin_owned(pid, parent_pin, adopted=False):
    before = identity(pid)
    if before['parent'] != parent_pin['pid'] or before['startTime'] < parent_pin['startTime']:
        raise RuntimeError('Process does not have the validated owned parent')
    if identity(parent_pin['pid'])['startTime'] != parent_pin['startTime']:
        raise RuntimeError('Owned parent identity changed')
    fd = os.pidfd_open(pid, 0)
    try:
        after = identity(pid)
        if before != after:
            raise RuntimeError('Process identity changed during pidfd capture')
        return {'identity': after, 'pidfd': fd, 'ownership': 'exclusive-subreaper-adopted-direct-child' if adopted else 'validated-owned-parent',
                'exitObserved': False, 'reaped': False}
    except Exception:
        os.close(fd)
        raise


def run_owned(command, log, env, timeout, reject_descendants=False):
    global cleanup_active, current_fault
    owner = identity(os.getpid())
    if child_ids(owner['pid']):
        raise RuntimeError('Exclusive process stage starts with children')
    started = time.monotonic()
    result = {'command': list(command), 'exitCode': None, 'passed': False, 'poisoned': False,
              'safetyTimeoutSeconds': timeout, 'rejectObservedDescendants': reject_descendants,
              'ownedProcesses': [], 'signals': [], 'signalCount': 0, 'remainingDirectChildren': [],
              'failures': [], 'subreaperAdoptionRequired': False}
    owned = {}
    retired = []
    root_captured = False
    process = None
    root_pid = None
    failures = result['failures']

    def fault(message):
        result['poisoned'] = True
        if len(failures) < 64:
            failures.append(str(message)[:4096])
        elif failures[-1] != 'failure-ledger-bound':
            failures[-1] = 'failure-ledger-bound'

    def register(pid, parent_pin, adopted=False):
        nonlocal root_captured
        old = owned.get(pid)
        if old is not None:
            current = identity(pid)
            if current['startTime'] == old['identity']['startTime']:
                return old
            if not exited(old['pidfd']):
                raise RuntimeError('A live registered process number changed identity')
            # Numeric reuse never inherits an old pidfd or root classification.
            old['exitObserved'] = True
            retired.append({key: value for key, value in old.items() if key != 'pidfd'})
            os.close(old['pidfd'])
            del owned[pid]
        entry = pin_owned(pid, parent_pin, adopted)
        if len(owned) + len(retired) >= MAX_REGISTERED:
            os.close(entry['pidfd'])
            raise RuntimeError('Owned process metadata registry exceeded its bound')
        entry['isRoot'] = pid == root_pid and not root_captured
        if entry['isRoot']:
            root_captured = True
        owned[pid] = entry
        if adopted:
            result['subreaperAdoptionRequired'] = True
        if reject_descendants and not entry['isRoot']:
            fault('A fixed nonproof host created an observed descendant')
        return entry

    def scan():
        # The coordinator is an exclusive subreaper and spawns only this Popen
        # during the scope. Newly adopted direct children therefore belong to it.
        for pid in child_ids(owner['pid']):
            try:
                register(pid, owner, adopted=pid != root_pid)
            except FileNotFoundError:
                fault('Direct child disappeared before ownership could be captured')
        for entry in list(owned.values()):
            if exited(entry['pidfd']):
                entry['exitObserved'] = True
                continue
            actual = identity(entry['identity']['pid'])
            if actual['startTime'] != entry['identity']['startTime']:
                raise RuntimeError('Live owned process identity changed')
            try:
                children = child_ids(actual['pid'])
            except FileNotFoundError:
                if not exited(entry['pidfd']):
                    raise
                continue
            for pid in children:
                try:
                    register(pid, actual)
                except FileNotFoundError:
                    fault('Descendant disappeared before ownership could be captured')
        for pid in child_ids(owner['pid']):
            register(pid, owner, adopted=pid != root_pid)

    def reap():
        # Popen owns/reaps its root. Other identities can become direct children
        # through this subreaper; waitpid does not signal or change an unowned PID.
        for pid, entry in list(owned.items()):
            if entry["isRoot"]:
                continue
            if exited(entry['pidfd']):
                entry['exitObserved'] = True
                try:
                    waited, _ = os.waitpid(pid, os.WNOHANG)
                    if waited == pid:
                        entry['reaped'] = True
                except ChildProcessError:
                    # The captured owned parent may have reaped it naturally.
                    if pid not in child_ids(owner['pid']):
                        entry['reaped'] = True

    def signal_owned(entry):
        if exited(entry['pidfd']):
            entry['exitObserved'] = True
            return
        # Captured pidfd kernel ownership survives reparenting and PID reuse.
        # Numeric identity is diagnostic only; it is never used for a signal.
        fault('Owned pidfd cleanup signal required')
        try:
            signal.pidfd_send_signal(entry['pidfd'], signal.SIGKILL, None, 0)
            result['signalCount'] += 1
            if len(result['signals']) < MAX_REGISTERED:
                result['signals'].append({'identity': entry['identity'], 'signal': 'SIGKILL', 'via': 'validated-owned-pidfd'})
        except ProcessLookupError:
            if not exited(entry['pidfd']):
                raise

    def emergency_direct_drain():
        # Registry bounds/errors do not suppress cleanup. Capture each actual direct
        # child through the exclusive coordinator identity, signal its pidfd, close it.
        # These extra facts can be truncated only on an already poisoned stage.
        for pid in child_ids(owner['pid']):
            old = owned.get(pid)
            if old is not None and identity(pid)['startTime'] == old['identity']['startTime']:
                signal_owned(old); continue
            entry = None
            try:
                entry = pin_owned(pid, owner, adopted=True)
                result['subreaperAdoptionRequired'] = True
                signal_owned(entry)
            except FileNotFoundError:
                fault('Direct cleanup child disappeared before pinning')
            finally:
                if entry is not None:
                    if exited(entry['pidfd']):
                        try: os.waitpid(pid, os.WNOHANG)
                        except ChildProcessError: pass
                    os.close(entry['pidfd'])

    current_fault = fault
    try:
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, env=env,
                                   start_new_session=True, close_fds=True)
        root_pid = process.pid
        root = register(root_pid, owner)
        if root['identity']['group'] != root_pid or root['identity']['session'] != root_pid:
            raise RuntimeError('The owned root did not create its isolated session')
        while process.poll() is None:
            scan()
            if failures:
                break
            if time.monotonic() - started >= timeout:
                fault('Process stage exceeded its safety deadline'); break
            time.sleep(0.02)
        if process.poll() is not None:
            result['exitCode'] = process.returncode
            if process.returncode != 0:
                fault('Process stage returned a nonzero exit code')
        # Permit naturally exiting build/download descendants a short bounded grace.
        grace = time.monotonic() + 2
        while not failures and time.monotonic() < grace:
            scan(); reap()
            if all(exited(entry['pidfd']) for entry in owned.values()) and not child_ids(owner['pid']):
                break
            time.sleep(0.02)
        if process.poll() is None or any(not exited(e['pidfd']) for e in owned.values()) or child_ids(owner['pid']):
            fault('Owned processes survived normal stage completion')
    except BaseException as error:
        fault(type(error).__name__ + ': ' + str(error))
    finally:
        cleanup_active = True
        if failures:
            # Stop known roots/descendants before taking additional ownership scans,
            # so an inspection failure cannot leave the process running and forking.
            for entry in list(owned.values()):
                try: signal_owned(entry)
                except Exception as error: fault('signal: ' + type(error).__name__ + ': ' + str(error))
            deadline = time.monotonic() + 10
            while time.monotonic() < deadline:
                try: scan()
                except Exception as error: fault('drain-scan: ' + type(error).__name__ + ': ' + str(error))
                for entry in list(owned.values()):
                    try: signal_owned(entry)
                    except Exception as error: fault('drain-signal: ' + type(error).__name__ + ': ' + str(error))
                try: emergency_direct_drain()
                except Exception as error: fault('direct-drain: ' + type(error).__name__ + ': ' + str(error))
                if process is not None:
                    process.poll()
                try: reap()
                except Exception as error: fault('drain-reap: ' + type(error).__name__ + ': ' + str(error))
                try:
                    if not child_ids(owner['pid']) and all(exited(e['pidfd']) for e in owned.values()):
                        break
                except Exception as error: fault('drain-empty: ' + type(error).__name__ + ': ' + str(error))
                time.sleep(0.02)
        if process is not None:
            try:
                result['exitCode'] = process.wait(timeout=1)
                for entry in owned.values():
                    if entry['isRoot']: entry['reaped'] = True
                for entry in retired:
                    if entry['isRoot']: entry['reaped'] = True
            except Exception as error: fault('root-wait: ' + type(error).__name__ + ': ' + str(error))
        try: reap()
        except Exception as error: fault('final-reap: ' + type(error).__name__ + ': ' + str(error))
        try:
            result['remainingDirectChildren'] = child_ids(owner['pid'])
            if result['remainingDirectChildren']: fault('Direct children remain after owned cleanup')
        except Exception as error: fault('final-child-inspection: ' + type(error).__name__ + ': ' + str(error))
        result["ownedProcesses"].extend(retired)
        for entry in owned.values():
            entry['exitObserved'] = exited(entry['pidfd'])
            if not entry['exitObserved']: fault('An owned pidfd still represents a live process')
            result['ownedProcesses'].append({key: value for key, value in entry.items() if key != 'pidfd'})
            os.close(entry['pidfd'])
        result['passed'] = (result['exitCode'] == 0 and not failures and result['signalCount'] == 0 and
                            result['remainingDirectChildren'] == [] and all(e['exitObserved'] for e in result['ownedProcesses']))
        result['safetyElapsedSeconds'] = time.monotonic() - started
        current_fault = None
        cleanup_active = False
    return result
