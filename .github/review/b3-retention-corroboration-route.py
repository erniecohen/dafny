"""Fixed finite read-only routing. Invalid flags never invoke the packet."""
import sys
sys.dont_write_bytecode = True
import hashlib
import json
import os
import stat
import time
from pathlib import Path

SOURCE = Path('.github/review/retention-corroboration')
OUTPUT = Path('out/b3-retention-corroboration-routing')
SOURCE_SHA = 'd1b0ee23e2c516cf35e836929c1b3034470110127b0d5df4631a847db98a4b49'
PACKET_SHA = '86791a5cb1f03d00998ec240de3fbc7cd5776596d3a1090f05da45a9943aa6dd'
PACKET_BYTES = 93233
WIRE_SCHEMA_SHA = 'a4c69b49db14b219e22186f9472a46210b1f7bd80b513d0b781b7457f31bb303'
PHASE_DEADLINE = globals().get('_ROUTE_WORK_DEADLINE', time.monotonic() + 600)
if type(PHASE_DEADLINE) not in {int, float} or PHASE_DEADLINE > time.monotonic() + 600:
    raise ValueError('Routing cannot widen the fixed work deadline.')
READ_COUNTS = {'controlReads': 0, 'controlBytes': 0}
OUTPUT_COUNTS = {'publicTextBytesReserved': 0, 'JSONReportBytesReserved': 0, 'allArtifactBytesReserved': 0}
EXPECTED = {'B3_FOCUS_GATE': 'retention-corroboration', 'B3_COMPILE_ONLY': 'true',
            'B3_FULL_GATE': 'false', 'B3_BINARIES': 'false',
            'B3_RESOLVER_PROBE': 'false', 'B3_ADDITIONAL_AXIOMS_PROBE': 'false'}


def check_deadline():
    if time.monotonic() >= PHASE_DEADLINE:
        raise ValueError('Fixed routing deadline exceeded.')


def require(value, code):
    if not value:
        raise ValueError(code)


def deadline(until):
    require(time.monotonic() < until, 'Fixed routing publication deadline exceeded.')


def fresh_output_directory():
    parent = directory_descriptor(Path('.'))
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
    try:
        for i, component in enumerate(OUTPUT.parts):
            check_deadline()
            try:
                os.mkdir(component, mode=0o700, dir_fd=parent)
            except FileExistsError:
                require(i != len(OUTPUT.parts) - 1, 'Fresh routing output required.')
            before = os.stat(component, dir_fd=parent, follow_symlinks=False)
            require(stat.S_ISDIR(before.st_mode), 'Routing output parent is not a directory.')
            child = os.open(component, flags, dir_fd=parent)
            try:
                identity = lambda s: (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)
                require(identity(before) == identity(os.fstat(child)) == identity(os.stat(component,
                        dir_fd=parent, follow_symlinks=False)), 'Routing output directory changed.')
            except BaseException:
                os.close(child)
                raise
            os.close(parent)
            parent = child
        return parent
    except BaseException:
        os.close(parent)
        raise


def directory_descriptor(path):
    path = Path(path).absolute()
    if any(n in {'.', '..'} for n in path.parts[1:]):
        raise ValueError('Canonical fixed routing directory required.')
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
    descriptor = os.open('/', flags)
    identity = lambda s: (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)
    try:
        for part in path.parts[1:]:
            check_deadline()
            before = os.stat(part, dir_fd=descriptor, follow_symlinks=False)
            if not stat.S_ISDIR(before.st_mode):
                raise ValueError('No-follow routing ancestor must be a directory.')
            child = os.open(part, flags, dir_fd=descriptor)
            try:
                if identity(before) != identity(os.fstat(child)) or identity(before) != identity(os.stat(part, dir_fd=descriptor, follow_symlinks=False)):
                    raise ValueError('Routing ancestor changed during descriptor traversal.')
            except BaseException:
                os.close(child)
                raise
            os.close(descriptor)
            descriptor = child
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def read(path, maximum=32 * 1024 ** 2):
    path = Path(path)
    parent = directory_descriptor(path.parent)
    descriptor = None
    chunks = []
    length = 0
    try:
        before = os.stat(path.name, dir_fd=parent, follow_symlinks=False)
        maximum = min(maximum, 64 * 1024 ** 2 - READ_COUNTS['controlBytes'])
        if not stat.S_ISREG(before.st_mode) or before.st_size > maximum or READ_COUNTS['controlReads'] >= 512:
            raise ValueError('Require a bounded regular source file within routing control budget.')
        descriptor = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=parent)
        identity = lambda s: (s.st_dev, s.st_ino, s.st_mode, s.st_size, s.st_mtime_ns, s.st_ctime_ns)
        if identity(before) != identity(os.fstat(descriptor)):
            raise ValueError('Source changed before routing content read.')
        READ_COUNTS['controlReads'] += 1
        while length < before.st_size:
            check_deadline()
            block = os.read(descriptor, min(1048576, before.st_size - length, maximum - length))
            if not block:
                break
            READ_COUNTS['controlBytes'] += len(block)
            length += len(block)
            chunks.append(block)
        if length != before.st_size or identity(before) != identity(os.fstat(descriptor)) or identity(before) != identity(os.stat(path.name, dir_fd=parent, follow_symlinks=False)):
            raise ValueError('Source changed during routing capture.')
    finally:
        if descriptor is not None:
            os.close(descriptor)
        os.close(parent)
    body = b''.join(chunks)
    return body, {'bytes': len(body), 'sha256': hashlib.sha256(body).hexdigest()}


def document(body):
    def pairs(items):
        value = {}
        for k, v in items:
            if k in value:
                raise ValueError('Duplicate routing JSON property.')
            value[k] = v
        return value
    return json.loads(body, object_pairs_hook=pairs)


def reserve_output(count, report):
    require(type(count) is int and count >= 0
            and OUTPUT_COUNTS['allArtifactBytesReserved'] + count <= (64 * 1024 ** 2), 'artifact-aggregate-byte-cap')
    if report:
        require(OUTPUT_COUNTS['JSONReportBytesReserved'] + count <= (32 * 1024 ** 2), 'report-aggregate-byte-cap')
        OUTPUT_COUNTS['JSONReportBytesReserved'] += count
    else:
        OUTPUT_COUNTS['publicTextBytesReserved'] += count
    OUTPUT_COUNTS['allArtifactBytesReserved'] += count

def write_all(descriptor, body, until):
    view = memoryview(body)
    offset = 0
    while offset < len(view):
        deadline(until)
        written = os.write(descriptor, view[offset:offset + 65536])
        require(written > 0, 'output-write-zero')
        offset += written

def json_tokens(value, until, depth=0, active=None):
    deadline(until)
    require(depth <= 64, 'output-JSON-depth-cap')
    if type(value) is str:
        yield b'"'
        for offset in range(0, len(value), 4096):
            deadline(until)
            # At most 4096 input characters; escaping cannot produce more
            # than 49152 ASCII bytes plus the two temporary quote bytes.
            token = json.dumps(value[offset:offset + 4096], ensure_ascii=True)[1:-1].encode('ascii')
            require(len(token) <= 49152, 'output-JSON-string-token-cap')
            yield token
        yield b'"'
    elif value is None:
        yield b'null'
    elif type(value) is bool:
        yield b'true' if value else b'false'
    elif type(value) is int:
        require(value.bit_length() <= 14300, 'output-JSON-integer-cap')
        yield str(value).encode('ascii')
    elif type(value) is float:
        require(value == value and value not in {float('inf'), float('-inf')}, 'output-JSON-finite-number')
        yield repr(value).encode('ascii')
    else:
        require(type(value) in {dict, list} and len(value) <= 65536, 'output-JSON-container-type-or-count')
        active = set() if active is None else active
        require(id(value) not in active, 'output-JSON-cycle')
        active.add(id(value))
        try:
            if type(value) is dict:
                for key in value:
                    deadline(until)
                    require(type(key) is str, 'output-JSON-key-type')
                yield b'{'
                for i, key in enumerate(sorted(value)):
                    deadline(until)
                    if i:
                        yield b','
                    yield from json_tokens(key, until, depth + 1, active)
                    yield b':'
                    yield from json_tokens(value[key], until, depth + 1, active)
                yield b'}'
            else:
                yield b'['
                for i, item in enumerate(value):
                    deadline(until)
                    if i:
                        yield b','
                    yield from json_tokens(item, until, depth + 1, active)
                yield b']'
        finally:
            active.remove(id(value))

def publish_route(parent, value, until):
    available = min(32 * 1024 ** 2 - OUTPUT_COUNTS['JSONReportBytesReserved'],
                    64 * 1024 ** 2 - OUTPUT_COUNTS['allArtifactBytesReserved'])
    count = 1
    require(count <= available, 'Routing report aggregate exhausted.')
    for token in json_tokens(value, until):
        deadline(until)
        count += len(token)
        require(count <= available, 'Routing report aggregate exhausted.')
    descriptor = None
    length = 0
    try:
        descriptor = os.open('routing.json.pending', os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
                             | os.O_NONBLOCK | os.O_CLOEXEC, mode=0o600, dir_fd=parent)
        for token in json_tokens(value, until):
            deadline(until)
            reserve_output(len(token), report=True)
            write_all(descriptor, token, until)
            length += len(token)
        reserve_output(1, report=True)
        write_all(descriptor, b'\n', until)
        length += 1
        require(length == count, 'Routing report preflight/emission mismatch.')
        os.fsync(descriptor)
        deadline(until)
        os.rename('routing.json.pending', 'routing.json', src_dir_fd=parent, dst_dir_fd=parent)
    finally:
        if descriptor is not None:
            os.close(descriptor)


def main():
    global PHASE_DEADLINE, OUTPUT_COUNTS
    try:
        output = fresh_output_directory()
    except BaseException:
        print('NOT GREEN: fresh routing output required')
        return 0
    route = {'schemaVersion': 2, 'scope': 'finite-read-only-retention-corroboration-routing', 'passed': False,
             'flagsAccepted': False, 'packetInvoked': False, 'sourcePinsAccepted': False,
             'SDKExecuted': False, 'collectorExecuted': False, 'analyzerExecuted': False,
             'targetExecuted': False, 'newArchiveDownloaded': False, 'reviewedRunnerClosure': False,
             'fullDiagnosticEnabled': False, 'sourceManifestSha256': SOURCE_SHA,
             'packetBodySha256': PACKET_SHA, 'wireSchemaSha256': WIRE_SCHEMA_SHA,
             'failureCode': None, 'failureType': None}
    code = None
    namespace = None
    try:
        if sys.argv[1:] or sys.flags.isolated != 1 or sys.flags.no_site != 1 or sys.flags.dont_write_bytecode != 1:
            raise ValueError('Fixed isolated argument-free routing required.')
        flags = {name: os.environ.get(name) for name in EXPECTED}
        # Fixed values only: a foreign environment value is never detached.
        route['flags'] = {name: value if value == EXPECTED[name] else 'nonmatching-value' for name, value in flags.items()}
        if flags != EXPECTED:
            raise ValueError('Finite corroboration requires compile_only=true, full=false and all other flags=false.')
        route['flagsAccepted'] = True
        raw, source_pin = read(SOURCE / 'source-manifest.json')
        if source_pin['sha256'] != SOURCE_SHA:
            raise ValueError('Fixed corroboration source manifest changed.')
        manifest = document(raw)
        if set(manifest) != {'schemaVersion', 'scope', 'files', 'original44Sha256', 'originalScannerSha256', 'productBase'}:
            raise ValueError('Fixed corroboration manifest schema changed.')
        declared = set()
        for pin in manifest['files']:
            name = pin['path']
            if name in declared or name.startswith('/') or '\\' in name or any(x in {'', '.', '..'} for x in name.split('/')):
                raise ValueError('Fixed source relative inventory required.')
            declared.add(name)
            raw, now = read(SOURCE / name)
            if now != {k: pin[k] for k in ['bytes', 'sha256']}:
                raise ValueError('Declared corroboration source changed.')
        raw, outer_pin = read('.github/review/b3-retention-corroboration-manifest.json')
        outer = document(raw)
        if (set(outer) != {'schemaVersion', 'scope', 'sourceManifestSha256', 'compileOnlyRequired',
                'fullGatePermitted', 'newArchivesPermitted', 'SDKToolTargetOrProofExecutionPermitted', 'files'}
                or type(outer['schemaVersion']) is not int or outer['schemaVersion'] != 1
                or outer['scope'] != 'fixed-finite-read-only-retention-corroboration-routing'
                or outer['compileOnlyRequired'] is not True or outer['fullGatePermitted'] is not False
                or outer['newArchivesPermitted'] is not False or outer['SDKToolTargetOrProofExecutionPermitted'] is not False):
            raise ValueError('Fixed outer routing schema or false execution boundary changed.')
        if outer['sourceManifestSha256'] != SOURCE_SHA or [p['path'] for p in outer['files']] != [
                '.github/review/retention-corroboration/source-manifest.json', '.github/review/b3-retention-corroboration-route.py',
                '.github/review/base', '.github/workflows/review.yml']:
            raise ValueError('Fixed outer routing inventory changed.')
        for pin in outer['files']:
            raw, now = read(pin['path'])
            if now != {k: pin[k] for k in ['bytes', 'sha256']}:
                raise ValueError('Declared corroboration routing changed.')
        raw, packet_pin = read(SOURCE / 'packet.py')
        if packet_pin != {'bytes': PACKET_BYTES, 'sha256': PACKET_SHA}:
            raise ValueError('Captured packet body changed before compilation.')
        code = compile(raw, str(SOURCE / 'packet.py'), 'exec', dont_inherit=True)
        route['sourcePinsAccepted'] = True
        route['outerManifestSha256'] = outer_pin['sha256']
        route['packetInvoked'] = True
        # Execute exactly captured hash-bound Python source, not import machinery,
        # pycache, the historical scanner or any inspected image/tool body.
        namespace = {'__name__': '__main__', '__file__': str(SOURCE / 'packet.py'),
                     '_ROUTE_WORK_DEADLINE': PHASE_DEADLINE}
        exec(code, namespace)
        code = None
        PHASE_DEADLINE = namespace['_CORROBORATION_PUBLICATION_DEADLINE']
        # Independently recheck the exact captured control inventory after the
        # packet returns; a successful summary cannot waive source mutation.
        raw, after_source = read(SOURCE / 'source-manifest.json')
        if after_source != source_pin:
            raise ValueError('Corroboration manifest changed across packet invocation.')
        for root, pins in [(SOURCE, manifest['files']), (Path('.'), outer['files'])]:
            for pin in pins:
                raw, now = read(root / pin['path'])
                if now != {k: pin[k] for k in ['bytes', 'sha256']}:
                    raise ValueError('Pinned corroboration source or routing changed after packet invocation.')
        raw, after_outer = read('.github/review/b3-retention-corroboration-manifest.json')
        if after_outer != outer_pin:
            raise ValueError('Corroboration outer manifest changed across packet invocation.')
        summary_path = Path('out/b3-retention-corroboration/summary.json')
        raw, route['summaryPin'] = read(summary_path)
        summary = document(raw)
        if (type(summary.get('schemaVersion')) is not int or summary['schemaVersion'] != 2
                or summary.get('wireSchemaSha256') != WIRE_SCHEMA_SHA
                or summary.get('frozenConceptualRequiredNamesEnforcedUnchanged') is not False
                or type(summary.get('wireSchemaCaptured')) is not bool
                or summary.get('passed') is True and summary['wireSchemaCaptured'] is not True
                or type(summary.get('captureCompleted')) is not bool or type(summary.get('passed')) is not bool
                or summary['captureCompleted'] != summary['passed']
                or any(summary.get(k) is not False for k in ['SDKExecuted', 'collectorExecuted', 'analyzerExecuted',
                    'targetExecuted', 'fullDiagnosticEnabled', 'reviewedRunnerClosure', 'newArchiveDownloaded',
                    'originalScannerInvokedOrImported', 'nativeProofOrCostParityAccepted'])):
            raise ValueError('Packet false-boundary/schema claims changed.')
        route['passed'] = summary['captureCompleted']
    except BaseException as error:
        # Exception messages may contain unreviewed input paths or text.
        route['failureCode'] = 'fixed-routing-or-entry-failure'
        route['failureType'] = type(error).__name__
    finally:
        code = None
        try:
            if namespace is not None and 'OUTPUT_COUNTS' in namespace:
                counts = namespace['OUTPUT_COUNTS']
                require(type(counts) is dict and set(counts) == set(OUTPUT_COUNTS)
                        and all(type(n) is int and n >= 0 for n in counts.values())
                        and counts['allArtifactBytesReserved'] == counts['publicTextBytesReserved'] + counts['JSONReportBytesReserved']
                        and counts['allArtifactBytesReserved'] <= 64 * 1024 ** 2
                        and counts['JSONReportBytesReserved'] <= 32 * 1024 ** 2, 'Packet output reservations invalid.')
                OUTPUT_COUNTS = counts
            publication = (namespace.get('_CORROBORATION_PUBLICATION_DEADLINE') if namespace is not None else None)
            if publication is None:
                publication = time.monotonic() + 5
            require(type(publication) in {int, float} and publication <= time.monotonic() + 5,
                    'Routing cannot widen the fixed publication window.')
            route['outputReservationsBeforeRoutingReport'] = dict(OUTPUT_COUNTS)
            route['routingControlReads'] = dict(READ_COUNTS)
            publish_route(output, route, publication)
        except BaseException:
            route['passed'] = False
            print('NOT GREEN: bounded routing publication failed')
        finally:
            os.close(output)
        print('Finite corroboration routing: ' + ('CAPTURED ONLY' if route['passed'] else 'NOT GREEN'))
    return 0


if __name__ == '__main__':
    main()
