"""Fixed component controls, launched only by the pinned native ELF fixture."""
import errno
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile


def main():
    if len(sys.argv) != 4:
        raise ValueError('The fixed seal component requires template, solver and digest')
    template, solver, digest = map(str, sys.argv[1:])
    spec = importlib.util.spec_from_file_location('reviewed_native_capture', template)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    with tempfile.TemporaryDirectory(prefix='sealed-control-', dir=os.getcwd()) as directory:
        source = Path(directory) / 'solver-copy'
        if not 0 < Path(solver).stat().st_size <= module.MAX_IMAGE_BYTES:
            raise ValueError('Unbounded solver input')
        shutil.copyfile(solver, source)
        image, receipt = module.capture_solver(str(source), digest)
        try:
            denied = []
            for name, operation in [
                ('write', lambda: os.write(image, b'x')),
                ('grow', lambda: os.ftruncate(image, receipt['bytes'] + 1)),
                ('shrink', lambda: os.ftruncate(image, receipt['bytes'] - 1)),
                ('add-seal', lambda: fcntl.fcntl(image, fcntl.F_ADD_SEALS, 0x10))]:
                try:
                    operation()
                    raise AssertionError('A sealed operation succeeded: ' + name)
                except OSError as error:
                    if error.errno != errno.EPERM:
                        raise
                    denied.append({'operation': name, 'errno': error.errno})
            with source.open('r+b') as mutable:
                first = mutable.read(1)
                mutable.seek(0)
                mutable.write(bytes([first[0] ^ 1]))
                mutable.flush()
                os.fsync(mutable.fileno())
            with source.open('rb') as mutated:
                changed = hashlib.file_digest(mutated, 'sha256').hexdigest() != digest
            os.lseek(image, 0, os.SEEK_SET)
            sealed_hash = hashlib.sha256()
            while True:
                block = os.read(image, 65536)
                if not block:
                    break
                sealed_hash.update(block)
            seals = fcntl.fcntl(image, fcntl.F_GET_SEALS)
            if not changed or sealed_hash.hexdigest() != digest or seals & module.REQUIRED_IMAGE_SEALS != module.REQUIRED_IMAGE_SEALS:
                raise AssertionError('Captured bytes did not remain pinned after source mutation')
        finally:
            os.close(image)
    data = bytes((index * 13 + 7) % 256 for index in range(257))
    sent = bytearray()
    observed = bytearray()
    interrupted = False
    def bounded_writer(_target, fragment):
        nonlocal interrupted
        if not interrupted:
            interrupted = True
            raise InterruptedError()
        count = min(7, len(fragment))
        sent.extend(fragment[:count])
        return count
    facts = module.forward_bytes(-1, data, observed.extend, bounded_writer)
    if bytes(sent) != data or bytes(observed) != data or facts['partialWrites'] == 0 or facts['interruptedWrites'] != 1:
        raise AssertionError('The production write-loop component failed')
    print(json.dumps({'schemaVersion': 1, 'capturedSha256': digest, 'sourceMutationChangedDigest': changed,
                      'requiredSeals': module.REQUIRED_IMAGE_SEALS, 'seals': seals, 'denied': denied,
                      'writeLoopComponent': {'kind': 'deterministic-callback-not-kernel-partial-write',
                                             'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), **facts}}))


if __name__ == '__main__':
    main()
