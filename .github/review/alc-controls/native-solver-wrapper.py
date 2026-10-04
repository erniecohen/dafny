#!__PYTHON_EXECUTABLE__ -I
"""Source-only transparent Linux solver wrapper. No proof CLI mode enables it yet."""
import ctypes
import fcntl
import hashlib
import json
import os
from pathlib import Path
import select
import signal
import stat
import sys
import threading
import time
import uuid


MAX_IMAGE_BYTES = 256 * 1024 * 1024
REQUIRED_IMAGE_SEALS = fcntl.F_SEAL_WRITE | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_SEAL
# MFD_EXEC explicitly requires executable memfds (Linux 6.3+); never fall back
# to executing the mutable source inode or an unsealed temporary pathname.
MFD_EXEC = 0x0010


def identity(pid):
    text = Path('/proc/' + str(pid) + '/stat').read_text()
    fields = text[text.rindex(')') + 2:].split()
    return {'pid': pid, 'startTime': int(fields[19]), 'parent': int(fields[1]),
            'group': int(fields[2]), 'session': int(fields[3])}


def save(path, data):
    temporary = path.with_suffix('.pending')
    with temporary.open('x') as output:
        json.dump(data, output)
        output.flush()
        os.fsync(output.fileno())
    temporary.rename(path)


def forward_bytes(target, data, record_written, write=os.write):
    """The production write loop; a component control supplies a bounded writer."""
    view = memoryview(data)
    calls = partial = interrupted = 0
    while view:
        try:
            count = write(target, view)
            calls += 1
        except InterruptedError:
            interrupted += 1
            continue
        if count <= 0 or count > len(view):
            raise RuntimeError('A forwarding write made invalid progress')
        partial += count < len(view)
        record_written(view[:count])
        view = view[count:]
    return {'writeCalls': calls, 'partialWrites': partial, 'interruptedWrites': interrupted}


def capture_solver(real, expected_digest):
    source = os.open(real, os.O_RDONLY | os.O_CLOEXEC | os.O_NONBLOCK)
    image = None
    try:
        metadata = os.fstat(source)
        if not stat.S_ISREG(metadata.st_mode) or not 0 < metadata.st_size <= MAX_IMAGE_BYTES:
            raise RuntimeError('Solver must be a bounded regular native ELF image')
        image = os.memfd_create('alc-pinned-solver', os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING | MFD_EXEC)
        count = 0
        prefix = b''
        while True:
            try:
                data = os.read(source, 65536)
            except InterruptedError:
                continue
            if not data:
                break
            count += len(data)
            if count > MAX_IMAGE_BYTES:
                raise RuntimeError('Solver image safety cap exceeded')
            if len(prefix) < 4:
                prefix = (prefix + data)[:4]
            view = memoryview(data)
            while view:
                try:
                    written = os.write(image, view)
                except InterruptedError:
                    continue
                if written <= 0:
                    raise RuntimeError('A solver image write made no progress')
                view = view[written:]
        if prefix != b'\x7fELF' or count == 0:
            raise RuntimeError('The pinned solver must be a native ELF executable')
        os.fchmod(image, 0o500)
        fcntl.fcntl(image, fcntl.F_ADD_SEALS, REQUIRED_IMAGE_SEALS)
        seals = fcntl.fcntl(image, fcntl.F_GET_SEALS)
        if seals & REQUIRED_IMAGE_SEALS != REQUIRED_IMAGE_SEALS:
            raise RuntimeError('Executable solver image sealing failed')
        # Hash the final sealed image, not the potentially changing source file.
        os.lseek(image, 0, os.SEEK_SET)
        digest = hashlib.sha256()
        while True:
            data = os.read(image, 65536)
            if not data:
                break
            digest.update(data)
        os.lseek(image, 0, os.SEEK_SET)
        if digest.hexdigest() != expected_digest:
            raise RuntimeError('Captured solver image does not match the pin')
        receipt = {'sha256': digest.hexdigest(), 'bytes': count, 'seals': seals,
                   'requiredSeals': REQUIRED_IMAGE_SEALS, 'nativeElf': True, 'executableMemfd': True}
        return image, receipt
    except BaseException:
        if image is not None:
            os.close(image)
        raise
    finally:
        os.close(source)


def open_host_pidfd(host):
    if identity(host['pid'])['startTime'] != host['startTime']:
        raise RuntimeError('Host identity changed before pidfd capture')
    descriptor = os.pidfd_open(host['pid'], 0)
    try:
        if identity(host['pid'])['startTime'] != host['startTime']:
            raise RuntimeError('Host identity changed during pidfd capture')
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def watch_host(descriptor, stopped, root):
    poller = select.poll()
    poller.register(descriptor, select.POLLIN)
    try:
        while not stopped.is_set():
            for _, events in poller.poll(50):
                if events & (select.POLLIN | select.POLLHUP):
                    # Signal only this wrapper; its stable main thread owns the
                    # solver child's PDEATHSIG contract. Never signal the host.
                    os.kill(os.getpid(), signal.SIGKILL)
                if events & (select.POLLERR | select.POLLNVAL):
                    raise RuntimeError('Host process-lifetime watcher failed')
    except BaseException as error:
        try:
            save(root / ('wrapper-failure-' + str(os.getpid()) + '-' + uuid.uuid4().hex + '.json'),
                 {'pid': os.getpid(), 'type': type(error).__name__, 'message': str(error)})
        finally:
            os._exit(125)


def main():
    root = Path(__file__).resolve().parent
    config = json.loads((root / 'config.json').read_text())
    token = config['token']
    if not sys.platform.startswith('linux'):
        raise RuntimeError('Linux ownership is required')
    libc = ctypes.CDLL(None, use_errno=True)
    if os.getppid() != config['host']['pid'] or identity(os.getppid())['startTime'] != config['host']['startTime']:
        raise RuntimeError('Wrong launching host identity')
    # PR_SET_PDEATHSIG follows the creating .NET thread, which may retire while
    # the host stays alive. A verified pidfd observes process lifetime instead.
    host_pidfd = open_host_pidfd(config['host'])
    # Serialize closing admission with joining the exclusive inherited cgroup.
    with (root / 'admission.lock').open('r+') as admission:
        fcntl.flock(admission, fcntl.LOCK_EX)
        if (root / 'closed').exists():
            raise RuntimeError('Launch admission is closed')
        os.setsid()
        (Path(config['leaf']) / 'cgroup.procs').write_text(str(os.getpid()) + '\n')
        folder = root / 'launches' / uuid.uuid4().hex
        folder.mkdir(mode=0o700)
        fcntl.flock(admission, fcntl.LOCK_UN)
    real = config['realSolver']
    executable, image_receipt = capture_solver(real, config['solverSha256'])
    if os.execve not in os.supports_fd:
        raise RuntimeError('Descriptor-bound solver exec is unavailable')
    input_read, input_write = os.pipe()
    output_read, output_write = os.pipe()
    error_read, error_write = os.pipe()
    gate_read, gate_write = os.pipe()
    wrapper_pid = os.getpid()
    pid = os.fork()
    if pid == 0:
        try:
            if libc.prctl(1, signal.SIGKILL, 0, 0, 0) != 0 or os.getppid() != wrapper_pid:
                os._exit(125)
            os.close(host_pidfd)
            os.dup2(input_read, 0)
            os.dup2(output_write, 1)
            os.dup2(error_write, 2)
            for fd in [input_read, input_write, output_read, output_write, error_read, error_write, gate_write]:
                os.close(fd)
            if os.read(gate_read, 1) != b'g':
                os._exit(125)
            os.close(gate_read)
            if fcntl.fcntl(executable, fcntl.F_GET_SEALS) & REQUIRED_IMAGE_SEALS != REQUIRED_IMAGE_SEALS:
                os._exit(125)
            os.lseek(executable, 0, os.SEEK_SET)
            # Exec the sealed captured bytes, retaining argv[0], arguments and
            # environment. Native ELF exec closes the CLOEXEC descriptor.
            os.execve(executable, [real, *sys.argv[1:]], os.environ)
        except BaseException:
            os._exit(127)
    for fd in [input_read, output_write, error_write, gate_read]:
        os.close(fd)
    os.close(executable)
    host_watch_stopped = threading.Event()
    host_watch = threading.Thread(target=watch_host, args=(host_pidfd, host_watch_stopped, root), daemon=True)
    host_watch.start()  # Start after fork, avoiding a fork of a multithreaded Python process.
    save(folder / 'ready.json', {'token': token, 'wrapper': identity(wrapper_pid), 'solver': identity(pid),
                                'solverSha256': image_receipt['sha256'], 'solverImage': image_receipt, 'arguments': sys.argv[1:]})
    deadline = time.monotonic() + 30
    while not (folder / 'admitted').exists():
        if time.monotonic() >= deadline:
            os.close(gate_write)
            raise TimeoutError('PID ownership handshake did not complete')
        time.sleep(0.01)
    if (folder / 'admitted').read_text() != token:
        raise RuntimeError('Wrong admission token')
    os.write(gate_write, b'g')
    os.close(gate_write)
    done = threading.Event()
    streams = {}
    errors = []
    lock = threading.Lock()

    def relay(source, target, name):
        read_hash, written_hash = hashlib.sha256(), hashlib.sha256()
        read_bytes = written_bytes = 0
        writes = partial_writes = interrupted_writes = 0
        end = 'unknown'
        def written(fragment):
            nonlocal written_bytes
            written_hash.update(fragment)
            written_bytes += len(fragment)
        try:
            with (folder / (name + '.bin')).open('xb', buffering=0) as log:
                while True:
                    if name == 'stdin' and not select.select([source], [], [], 0.05)[0]:
                        if done.is_set():
                            end = 'solver-exited-after-cli-completion-or-natural-exit'
                            break
                        continue
                    try:
                        data = os.read(source, 65536)
                    except InterruptedError:
                        continue
                    if not data:
                        end = 'eof'
                        if name == 'stdin':
                            os.close(target)
                        break
                    if read_bytes + len(data) > 64 * 1024 * 1024:
                        raise RuntimeError('Stream safety cap exceeded')
                    logged = memoryview(data)
                    while logged:
                        try:
                            count = os.write(log.fileno(), logged)
                        except InterruptedError:
                            continue
                        if count <= 0:
                            raise RuntimeError('A capture write made no progress')
                        logged = logged[count:]
                    read_hash.update(data)
                    read_bytes += len(data)
                    statistics = forward_bytes(target, data, written)
                    writes += statistics['writeCalls']
                    partial_writes += statistics['partialWrites']
                    interrupted_writes += statistics['interruptedWrites']
                os.fsync(log.fileno())
        except BaseException as error:
            with lock:
                errors.append({'stream': name, 'type': type(error).__name__, 'errno': getattr(error, 'errno', None), 'message': str(error)})
        finally:
            with lock:
                streams[name] = {'readBytes': read_bytes, 'writtenBytes': written_bytes, 'end': end,
                                 'readSha256': read_hash.hexdigest(), 'writtenSha256': written_hash.hexdigest(),
                                 'writeCalls': writes, 'partialWrites': partial_writes, 'interruptedWrites': interrupted_writes,
                                 'writeCountersScope': 'completed-forwarding-blocks'}

    threads = [threading.Thread(target=relay, args=(0, input_write, 'stdin')),
               threading.Thread(target=relay, args=(output_read, 1, 'stdout')),
               threading.Thread(target=relay, args=(error_read, 2, 'stderr'))]
    for thread in threads:
        thread.start()
    _, status = os.waitpid(pid, 0)
    done.set()
    for thread in threads:
        thread.join()
    code = os.waitstatus_to_exitcode(status)
    save(folder / 'complete.json', {'token': token, 'solverImage': image_receipt, 'solverExitCode': code,
                                    'streams': streams, 'errors': errors})
    host_watch_stopped.set()
    host_watch.join(timeout=1)
    if host_watch.is_alive():
        raise RuntimeError('Host process-lifetime watcher did not stop')
    os.close(host_pidfd)
    return code if code >= 0 else 128 - code


if __name__ == '__main__':
    try:
        code = main()
    except BaseException as error:
        # Wrapper faults belong in its private ledger, never inserted into solver streams.
        root = Path(__file__).resolve().parent
        failure = root / ('wrapper-failure-' + str(os.getpid()) + '-' + uuid.uuid4().hex + '.json')
        try:
            save(failure, {'pid': os.getpid(), 'type': type(error).__name__, 'message': str(error)})
        except BaseException:
            pass
        os._exit(125)
    else:
        os._exit(code)
