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
import sys
import threading
import time
import uuid


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


def main():
    root = Path(__file__).resolve().parent
    config = json.loads((root / 'config.json').read_text())
    token = config['token']
    if not sys.platform.startswith('linux'):
        raise RuntimeError('Linux ownership is required')
    libc = ctypes.CDLL(None, use_errno=True)
    if libc.prctl(1, signal.SIGKILL, 0, 0, 0) != 0:
        raise OSError(ctypes.get_errno(), 'wrapper parent-death signaling failed')
    if os.getppid() != config['host']['pid'] or identity(os.getppid())['startTime'] != config['host']['startTime']:
        raise RuntimeError('Wrong launching host identity')
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
    executable = open(real, 'rb')
    digest = hashlib.file_digest(executable, 'sha256').hexdigest()
    if digest != config['solverSha256']:
        raise RuntimeError('Solver image changed')
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
            os.dup2(input_read, 0)
            os.dup2(output_write, 1)
            os.dup2(error_write, 2)
            for fd in [input_read, input_write, output_read, output_write, error_read, error_write, gate_write]:
                os.close(fd)
            if os.read(gate_read, 1) != b'g':
                os._exit(125)
            os.close(gate_read)
            # Execute the exact file descriptor whose bytes were hashed, retaining argv
            # and environment. Linux native ELF exec closes the CLOEXEC descriptor.
            os.execve(executable.fileno(), [real, *sys.argv[1:]], os.environ)
        except BaseException:
            os._exit(127)
    for fd in [input_read, output_write, error_write, gate_read]:
        os.close(fd)
    executable.close()
    save(folder / 'ready.json', {'token': token, 'wrapper': identity(wrapper_pid), 'solver': identity(pid),
                                'solverSha256': digest, 'arguments': sys.argv[1:]})
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
        end = 'unknown'
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
                    view = memoryview(data)
                    while view:
                        try:
                            count = os.write(target, view)
                        except InterruptedError:
                            continue
                        if count <= 0:
                            raise RuntimeError('A forwarding write made no progress')
                        written_hash.update(view[:count])
                        written_bytes += count
                        view = view[count:]
                os.fsync(log.fileno())
        except BaseException as error:
            with lock:
                errors.append({'stream': name, 'type': type(error).__name__, 'errno': getattr(error, 'errno', None), 'message': str(error)})
        finally:
            with lock:
                streams[name] = {'readBytes': read_bytes, 'writtenBytes': written_bytes, 'end': end,
                                 'readSha256': read_hash.hexdigest(), 'writtenSha256': written_hash.hexdigest()}

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
    save(folder / 'complete.json', {'token': token, 'solverExitCode': code, 'streams': streams, 'errors': errors})
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
