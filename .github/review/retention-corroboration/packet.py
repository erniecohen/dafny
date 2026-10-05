"""Fixed finite read-only metadata capture. Never execute an inspected image."""
import sys
sys.dont_write_bytecode = True
import hashlib
import json
import os
import posixpath
import re
import stat
import struct
import time
import xml.etree.ElementTree as ET
from pathlib import Path

SOURCE = Path('.github/review/retention-corroboration')
OUTPUT = Path('out/b3-retention-corroboration')
OLD = Path('.github/review/alc-controls')
SCANNER = Path('.github/review/retention-inventory/inventory.py')
OLD_SHA = 'b2e0e676f8c279e25358330ac3a91e740a12ddce319fd9aaedc5f1bd508dbc90'
SCANNER_SHA = '01b1522c9405f95f82c6da1f56ff7185d5b83c2b80c7732b8a52accd83664d1b'
SELECTOR_SHA = '3f68c8c766dc6742b3144a83909c2f6a97c36b8382e49b97ccbd184fa6c5383a'
WIRE_SCHEMA_SHA = 'a4c69b49db14b219e22186f9472a46210b1f7bd80b513d0b781b7457f31bb303'
START = time.monotonic()
WORK = globals().get('_ROUTE_WORK_DEADLINE', START + 600)
if type(WORK) not in {int, float} or WORK > START + 600:
    raise ValueError('Internal routing deadline cannot widen the 600-second work budget.')
TEXT_MAX = 4 * 1024 ** 2
TEXT_TOTAL = 64 * 1024 ** 2
FILE_MAX = 512 * 1024 ** 2
FILE_TOTAL = 8 * 1024 ** 3
REPORT_MAX = 32 * 1024 ** 2
COUNTS = {'inputTextReads': 0, 'inputTextBytes': 0, 'statusTextBytes': 0, 'selectedStatusParagraphs': 0,
          'attemptedInputTextReads': 0, 'attemptedInputTextBytes': 0, 'attemptedStatusBytes': 0,
          'hashFileBytes': 0, 'controlReads': 0, 'controlBytes': 0}
ROWS = []
CHECKS = []
ABSENCE_CHECKS = []
FAULTS = []
FAULT_OVERFLOW = False
CURRENT = None
ALLOWED = set()
DOC = None
WIRE = None
NATIVE_IMAGES = set()
OUTPUT_COUNTS = {'publicTextBytesReserved': 0, 'JSONReportBytesReserved': 0, 'allArtifactBytesReserved': 0}
OUTPUT_DESCRIPTOR = None


class Fault(Exception):
    def __init__(self, code, facts=None):
        super().__init__(code)
        self.facts = facts


def require(value, code):
    if not value:
        raise Fault(code)


def deadline(until=WORK):
    require(time.monotonic() < until, 'deadline-exceeded')


def sha(body):
    return hashlib.sha256(body).hexdigest()


def strict_json(body, until=WORK):
    require(len(body) <= REPORT_MAX, 'JSON-byte-cap')
    depth = members = 0
    quoted = escaped = False
    for i, c in enumerate(body.decode('utf-8')):
        if i % 4096 == 0:
            deadline(until)
        if quoted:
            if escaped:
                escaped = False
            elif c == '\\':
                escaped = True
            elif c == '"':
                quoted = False
        elif c == '"':
            quoted = True
        elif c in '[{':
            depth += 1
            require(depth <= 64, 'JSON-depth-cap')
        elif c in ']}':
            depth -= 1
        elif c in ',:':
            members += 1
            require(members <= 65536, 'JSON-member-cap')
    def pairs(items):
        d = {}
        for k, v in items:
            require(k not in d, 'duplicate-JSON-key')
            d[k] = v
        return d
    def constant(value):
        raise Fault('nonfinite-JSON-number')
    return json.loads(body, object_pairs_hook=pairs, parse_constant=constant)


def identity(s):
    return {'device': s.st_dev, 'inode': s.st_ino, 'mode': s.st_mode,
            'size': s.st_size, 'mtime_ns': s.st_mtime_ns, 'ctime_ns': s.st_ctime_ns}


def forbidden_path(name):
    path = posixpath.normpath(name)
    roots = ['/root', '/home', '/etc/ssl/private', '/etc/ssh', '/etc/sudoers.d',
             '/etc/apt/auth.conf.d', '/run/secrets', '/var/lib/cloud']
    files = ['/etc/shadow', '/etc/gshadow', '/etc/apt/auth.conf', '/etc/sudoers', '/etc/passwd', '/etc/group']
    base = path.rsplit('/', 1)[-1].lower()
    return (path in files or any(path == r or path.startswith(r + '/') for r in roots)
            or path.startswith('/proc/') and path.endswith('/environ')
            or base in {'id_rsa', 'id_ed25519'}
            or base.endswith(('.key', '.p12', '.pfx', '.kdbx')))


def directory_descriptor(name, until=WORK):
    require(type(name) is str and name.startswith('/') and name == posixpath.normpath(name)
            and len(name) <= 4096 and '\x00' not in name, 'descriptor-directory-path')
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
    descriptor = os.open('/', flags)
    try:
        for component in name.split('/')[1:]:
            if not component:
                continue
            deadline(until)
            expected = os.stat(component, dir_fd=descriptor, follow_symlinks=False)
            require(stat.S_ISDIR(expected.st_mode), 'descriptor-ancestor-not-directory')
            child = os.open(component, flags, dir_fd=descriptor)
            try:
                require(identity(expected) == identity(os.fstat(child))
                        == identity(os.stat(component, dir_fd=descriptor, follow_symlinks=False)),
                        'descriptor-ancestor-changed')
            except BaseException:
                os.close(child)
                raise
            os.close(descriptor)
            descriptor = child
        require(stat.S_ISDIR(os.fstat(descriptor).st_mode), 'descriptor-directory-type')
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def fresh_output_directory(relative, until):
    path = Path(relative)
    require(not path.is_absolute() and all(n not in {'', '.', '..'} for n in path.parts), 'fixed-output-relative-path')
    parent = directory_descriptor(str(Path('.').absolute()), until)
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
    try:
        for i, component in enumerate(path.parts):
            deadline(until)
            try:
                os.mkdir(component, mode=0o700, dir_fd=parent)
            except FileExistsError:
                require(i != len(path.parts) - 1, 'fresh-packet-output')
            expected = os.stat(component, dir_fd=parent, follow_symlinks=False)
            require(stat.S_ISDIR(expected.st_mode), 'output-parent-directory-type')
            child = os.open(component, flags, dir_fd=parent)
            try:
                require(identity(expected) == identity(os.fstat(child))
                        == identity(os.stat(component, dir_fd=parent, follow_symlinks=False)), 'output-directory-changed')
            except BaseException:
                os.close(child)
                raise
            os.close(parent)
            parent = child
        return parent
    except BaseException:
        os.close(parent)
        raise


def output_descriptor(public_text=False, until=WORK):
    require(OUTPUT_DESCRIPTOR is not None, 'owned-output-descriptor-required')
    parent = os.dup(OUTPUT_DESCRIPTOR)
    if not public_text:
        return parent
    try:
        deadline(until)
        try:
            os.mkdir('public-text', mode=0o700, dir_fd=parent)
        except FileExistsError:
            pass
        expected = os.stat('public-text', dir_fd=parent, follow_symlinks=False)
        require(stat.S_ISDIR(expected.st_mode), 'public-text-directory-type')
        child = os.open('public-text', os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC, dir_fd=parent)
        try:
            require(identity(expected) == identity(os.fstat(child))
                    == identity(os.stat('public-text', dir_fd=parent, follow_symlinks=False)), 'public-text-directory-changed')
        except BaseException:
            os.close(child)
            raise
        return child
    finally:
        os.close(parent)


def reserve_output(count, report):
    require(type(count) is int and count >= 0
            and OUTPUT_COUNTS['allArtifactBytesReserved'] + count <= TEXT_TOTAL, 'artifact-aggregate-byte-cap')
    if report:
        require(OUTPUT_COUNTS['JSONReportBytesReserved'] + count <= REPORT_MAX, 'report-aggregate-byte-cap')
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


def resolve(name, allowed, until=WORK, track=True):
    deadline(until)
    require(type(name) is str and name.startswith('/') and '\x00' not in name
            and len(name) <= 4096 and name == posixpath.normpath(name), 'invalid-fixed-path')
    require(name in allowed and not forbidden_path(name), 'undeclared-or-forbidden-path')
    todo = name.split('/')[1:]
    done = []
    links = []
    while todo:
        deadline(until)
        part = todo.pop(0)
        trial = '/' + '/'.join(done + [part])
        try:
            info = os.lstat(trial)
        except (FileNotFoundError, NotADirectoryError) as error:
            parent = '/' + '/'.join(done)
            parent_fd = directory_descriptor(parent or '/', until)
            try:
                parent_id = identity(os.fstat(parent_fd))
                try:
                    os.stat(part, dir_fd=parent_fd, follow_symlinks=False)
                except (FileNotFoundError, NotADirectoryError) as confirmed:
                    error = confirmed
                else:
                    raise Fault('absence-no-longer-observed')
                after_id = identity(os.fstat(parent_fd))
                require(parent_id == after_id, 'absence-parent-changed')
            finally:
                os.close(parent_fd)
            result = {'presence': 'absent-ENOENT' if isinstance(error, FileNotFoundError) else 'absent-ENOTDIR',
                    'resolvedPath': None, 'linkObservations': links,
                    'absence': {'errno': error.errno, 'parentPath': parent or '/', 'parentBefore': parent_id,
                                'parentAfter': after_id, 'parentReadableNoFollow': True}}
            if track:
                require(len(ABSENCE_CHECKS) < 512, 'selected-absence-count-cap')
                ABSENCE_CHECKS.append({'requestedPath': name, 'resolution': result})
            return result
        if stat.S_ISLNK(info.st_mode):
            require(len(links) < 8, 'alias-hop-cap')
            target = os.readlink(trial)
            # A parent step after a target component would require walking that
            # component (possibly another alias) before '..'. Lexical normpath
            # cannot establish its kernel meaning. Fail rather than claiming
            # a different file was the configured alias's actual endpoint.
            component_seen = False
            for component in target.split('/'):
                if component in {'', '.'}:
                    continue
                if component == '..':
                    require(not component_seen, 'alias-parent-after-component-not-supported')
                else:
                    component_seen = True
            replacement = posixpath.normpath(target if target.startswith('/') else posixpath.join('/' + '/'.join(done), target))
            endpoint = posixpath.normpath(posixpath.join(replacement, *todo))
            if endpoint not in allowed or forbidden_path(endpoint):
                raise Fault('unknown-alias-endpoint', {'knownLinkObservations': links,
                    'sourceLinkPath': trial, 'sourceLinkSha256': sha(os.fsencode(target)),
                    'sourceLinkIdentity': identity(info), 'unknownTargetOpened': False})
            links.append({'path': trial, 'linkSha256': sha(os.fsencode(target)),
                          'targetPath': replacement, 'identity': identity(info)})
            todo = endpoint.split('/')[1:]
            done = []
        else:
            if todo:
                require(stat.S_ISDIR(info.st_mode), 'ancestor-not-directory')
            done.append(part)
    endpoint = '/' + '/'.join(done)
    require(endpoint in allowed and not forbidden_path(endpoint), 'unknown-resolved-endpoint')
    return {'presence': 'alias-to-regular' if links else 'regular', 'resolvedPath': endpoint,
            'linkObservations': links, 'absence': None}


def read_file(name, allowed, maximum=FILE_MAX, keep=False, control=False, until=WORK, track=True,
              text_role=False, status_stream=False):
    resolution = resolve(name, allowed, until, track=track)
    require(resolution['resolvedPath'] is not None, 'required-file-absent')
    p = resolution['resolvedPath']
    maximum = min(maximum, (64 * 1024 ** 2 - COUNTS['controlBytes']) if control else (FILE_TOTAL - COUNTS['hashFileBytes']))
    if text_role or status_stream:
        maximum = min(maximum, TEXT_TOTAL - COUNTS['attemptedInputTextBytes'])
    if status_stream:
        maximum = min(maximum, 16 * 1024 ** 2 - COUNTS['attemptedStatusBytes'])
    parent_fd = directory_descriptor(posixpath.dirname(p), until)
    fd = None
    blocks = []
    digest = hashlib.sha256()
    length = 0
    try:
        before = os.stat(posixpath.basename(p), dir_fd=parent_fd, follow_symlinks=False)
        require(stat.S_ISREG(before.st_mode) and before.st_size <= maximum, 'wrong-file-type-or-size')
        fd = os.open(posixpath.basename(p), os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                     dir_fd=parent_fd)
        opened = os.fstat(fd)
        require(identity(before) == identity(opened), 'changed-before-open')
        if control:
            require(COUNTS['controlReads'] < 512, 'source-control-read-count-cap')
            COUNTS['controlReads'] += 1
        if text_role:
            require(COUNTS['attemptedInputTextReads'] < 128, 'attempted-text-role-cap')
            COUNTS['attemptedInputTextReads'] += 1
        while length < before.st_size:
            deadline(until)
            block = os.read(fd, min(1048576, before.st_size - length, maximum - length))
            if not block:
                break
            if control:
                COUNTS['controlBytes'] += len(block)
            else:
                COUNTS['hashFileBytes'] += len(block)
            if text_role or status_stream:
                COUNTS['attemptedInputTextBytes'] += len(block)
            if status_stream:
                COUNTS['attemptedStatusBytes'] += len(block)
            length += len(block)
            require(length <= maximum, 'file-byte-cap')
            digest.update(block)
            if keep:
                blocks.append(block)
        after = os.fstat(fd)
        outside = os.stat(posixpath.basename(p), dir_fd=parent_fd, follow_symlinks=False)
        require(identity(before) == identity(after) == identity(outside) and length == before.st_size,
                'changed-during-read')
    finally:
        if fd is not None:
            os.close(fd)
        os.close(parent_fd)
    require(resolve(name, allowed, until, track=False) == resolution, 'selected-alias-changed-during-read')
    pin = {'requestedPath': name, **resolution, 'readBefore': identity(before), 'readAfter': identity(outside),
           'bytes': length, 'sha256': digest.hexdigest(), 'stable': True}
    if track and not control:
        require(len(CHECKS) < 512, 'selected-read-count-cap')
        CHECKS.append(pin)
    return pin, b''.join(blocks) if keep else None


def source_read(relative, maximum=REPORT_MAX, until=WORK, text_role=False):
    require(type(relative) is str and not relative.startswith('/') and '\\' not in relative
            and all(p not in {'', '.', '..'} for p in relative.split('/')), 'source-relative-path')
    path = str(Path(relative).absolute())
    # The public checkout can be under the runner home, but selected runner data
    # never receives this narrow source-integrity exception.
    maximum = min(maximum, 64 * 1024 ** 2 - COUNTS['controlBytes'])
    if text_role:
        maximum = min(maximum, TEXT_MAX, TEXT_TOTAL - COUNTS['attemptedInputTextBytes'])
    require(COUNTS['controlReads'] < 512, 'source-control-read-count-cap')
    parent_fd = directory_descriptor(posixpath.dirname(path), until)
    fd = None
    try:
        before = os.stat(posixpath.basename(path), dir_fd=parent_fd, follow_symlinks=False)
        require(stat.S_ISREG(before.st_mode) and before.st_size <= maximum, 'source-type-or-size')
        fd = os.open(posixpath.basename(path), os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                     dir_fd=parent_fd)
        require(identity(os.fstat(fd)) == identity(before), 'source-open-race')
        COUNTS['controlReads'] += 1
        if text_role:
            require(COUNTS['attemptedInputTextReads'] < 128, 'attempted-historical-text-role-cap')
            COUNTS['attemptedInputTextReads'] += 1
        chunks = []
        size = 0
        while size < before.st_size:
            deadline(until)
            b = os.read(fd, min(1048576, before.st_size - size, maximum - size))
            if not b:
                break
            COUNTS['controlBytes'] += len(b)
            if text_role:
                COUNTS['attemptedInputTextBytes'] += len(b)
            size += len(b)
            require(size <= maximum, 'source-read-cap')
            chunks.append(b)
        require(size == before.st_size and identity(os.fstat(fd)) == identity(before)
                == identity(os.stat(posixpath.basename(path), dir_fd=parent_fd, follow_symlinks=False)), 'source-read-race')
    finally:
        if fd is not None:
            os.close(fd)
        os.close(parent_fd)
    require(COUNTS['controlReads'] <= 512 and COUNTS['controlBytes'] <= 64 * 1024 ** 2, 'source-control-read-cap')
    data = b''.join(chunks)
    return data, {'path': relative, 'bytes': len(data), 'sha256': sha(data)}


def source_pins(until=WORK):
    body, pin = source_read(str(SOURCE / 'source-manifest.json'), until=until)
    manifest = strict_json(body, until)
    require(set(manifest) == {'schemaVersion', 'scope', 'files', 'original44Sha256', 'originalScannerSha256', 'productBase'},
            'source-manifest-schema')
    require(manifest['schemaVersion'] == 1 and manifest['original44Sha256'] == OLD_SHA
            and manifest['originalScannerSha256'] == SCANNER_SHA
            and manifest['productBase'] == '8333daa60e2f2ee456068369f94c141898cde875', 'fixed-source-manifest-values')
    expected = set()
    pins = []
    for row in manifest['files']:
        require(set(row) == {'path', 'bytes', 'sha256'} and type(row['bytes']) is int, 'source-file-pin-schema')
        require(row['path'] not in expected, 'duplicate-source-file')
        expected.add(row['path'])
        _, now = source_read(str(SOURCE / row['path']), until=until)
        require({k: now[k] for k in ['bytes', 'sha256']} == {k: row[k] for k in ['bytes', 'sha256']}, 'source-file-changed')
        pins.append(now)
    require(len(expected) <= 32, 'source-file-count-cap')
    # Only fixed-depth source inventory: no broad repository or runner walk.
    exact_source_inventory(SOURCE, expected | {'source-manifest.json'}, until)
    old_raw, old_pin = source_read(str(OLD / 'source-manifest.json'), until=until)
    require(old_pin['sha256'] == OLD_SHA, 'original44-manifest-changed')
    old = strict_json(old_raw, until)
    require(len(old['files']) == 44, 'original44-denominator')
    exact_source_inventory(OLD, {r['path'] for r in old['files']} | {'source-manifest.json'}, until)
    for row in old['files']:
        _, now = source_read(str(OLD / row['path']), until=until)
        require(now['bytes'] == row['bytes'] and now['sha256'] == row['sha256'], 'original44-file-changed')
    _, scanner = source_read(str(SCANNER), until=until)
    require(scanner['sha256'] == SCANNER_SHA, 'original-scanner-changed')
    original_raw, original_pin = source_read('.github/review/retention-inventory/source-manifest.json', until=until)
    require(original_pin['sha256'] == 'ddde84160401f3cf18a728881e9d01c8975a6b85f873a1721369bded66f96c05', 'original-scanner-source-manifest-changed')
    original = strict_json(original_raw, until)
    exact_source_inventory(Path('.github/review/retention-inventory'), {r['path'] for r in original['files']} | {'source-manifest.json'}, until)
    for row in original['files']:
        _, current = source_read('.github/review/retention-inventory/' + row['path'], until=until)
        require(current['bytes'] == row['bytes'] and current['sha256'] == row['sha256'], 'original-scanner-source-changed')
    base, base_pin = source_read('.github/review/base', until=until)
    require(base == b'v4.11.0 8333daa60e2f2ee456068369f94c141898cde875\n', 'product-base-changed')
    outer_raw, outer_pin = source_read('.github/review/b3-retention-corroboration-manifest.json', until=until)
    outer = strict_json(outer_raw, until)
    require(set(outer) == {'schemaVersion', 'scope', 'sourceManifestSha256', 'compileOnlyRequired',
            'fullGatePermitted', 'newArchivesPermitted', 'SDKToolTargetOrProofExecutionPermitted', 'files'}
            and type(outer['schemaVersion']) is int and outer['schemaVersion'] == 1
            and outer['scope'] == 'fixed-finite-read-only-retention-corroboration-routing'
            and outer['compileOnlyRequired'] is True and outer['fullGatePermitted'] is False
            and outer['newArchivesPermitted'] is False and outer['SDKToolTargetOrProofExecutionPermitted'] is False,
            'outer-exact-schema-and-false-boundaries')
    require(outer['sourceManifestSha256'] == pin['sha256'] and
            [p['path'] for p in outer['files']] == [str(SOURCE / 'source-manifest.json'),
             '.github/review/b3-retention-corroboration-route.py', '.github/review/base', '.github/workflows/review.yml'],
            'outer-declared-boundary')
    for row in outer['files']:
        _, now = source_read(row['path'], until=until)
        require(now['bytes'] == row['bytes'] and now['sha256'] == row['sha256'], 'outer-pin-changed')
    return {'sourceManifest': pin, 'outerManifest': outer_pin, 'original44Manifest': old_pin,
            'originalScanner': scanner, 'productBase': base_pin, 'files': pins}


def exact_source_inventory(root, expected, until):
    require(len(expected) <= 64 and all(type(n) is str and not n.startswith('/') and '\\' not in n
            and all(p not in {'', '.', '..'} for p in n.split('/')) for n in expected), 'source-eligible-relative-names')
    directories = {''}
    for name in expected:
        parts = name.split('/')
        require(len(parts) <= 10, 'source-eligible-depth')
        directories.update('/'.join(parts[:i]) for i in range(1, len(parts)))
    require(len(directories) <= 64, 'source-eligible-directory-count')
    for name in sorted(directories):
        deadline(until)
        directory = root / name
        descriptor = directory_descriptor(str(directory.absolute()), until)
        children = set()
        try:
            before = identity(os.fstat(descriptor))
            with os.scandir(descriptor) as entries:
                for entry in entries:
                    deadline(until)
                    require(len(children) < 64 and not stat.S_ISLNK(os.stat(entry.name, dir_fd=descriptor,
                            follow_symlinks=False).st_mode), 'source-eligible-entry-cap-or-symlink')
                    children.add(name + '/' + entry.name if name else entry.name)
            require(identity(os.fstat(descriptor)) == before, 'source-eligible-directory-changed')
        finally:
            os.close(descriptor)
        declared = {n for n in expected | directories if n and posixpath.dirname(n) == name}
        # Only POSIX source paths are eligible; no unknown entry is descended into.
        require(children == declared, 'source-eligible-inventory-extra-or-missing')


def failure(selector_id, error):
    global FAULT_OVERFLOW
    code = str(error) if type(error) is Fault else 'read-or-parser-fault'
    # Never print arbitrary OSError filenames, input strings or exception args.
    if len(FAULTS) >= 512:
        FAULT_OVERFLOW = True
        return
    FAULTS.append({'selectorId': selector_id, 'code': code[:128], 'exceptionType': type(error).__name__})


def compare(name, pin):
    old = next((p['archivedPin'] for p in DOC['selectors'] if p['path'] == name and p['archivedPin'] is not None), None)
    if old is None:
        return 'not-archived-new-input'
    return 'same-bytes' if pin is not None and pin['bytes'] == old['bytes'] and pin['sha256'] == old['sha256'] else 'changed-new-input' if pin else 'absent-new-input'


def inert_text_kind(row):
    path = row['path']
    if path.endswith('.json'):
        return 'JSON'
    if path.endswith(('.xml', '.targets', '.props')):
        return 'XML'
    if '/ld.so.conf' in path:
        return 'loader-config'
    if path.endswith('nsswitch.conf'):
        return 'NSS-config'
    if path.endswith('.cnf'):
        return 'OpenSSL-config'
    return 'raw-inert-text'


def public_content(body, kind):
    markers = [b'BEGIN PRIVATE KEY', b'BEGIN RSA PRIVATE KEY', b'BEGIN EC PRIVATE KEY',
               b'BEGIN OPENSSH PRIVATE KEY', b'BEGIN DSA PRIVATE KEY', b'BEGIN ENCRYPTED PRIVATE KEY']
    require(not any(x in body for x in markers), 'private-key-content-rejected')
    text = body.decode('utf-8')
    # Segments are disjoint and inspected once. No regex with ambiguous greedy
    # repetitions runs on the whole input. False positives remain conservative.
    lower = text.lower()
    segment = 0
    quoted_segment = None
    pending_sensitive = False
    url_start = None
    url_colon = False
    for i, character in enumerate(lower):
        if i % 4096 == 0:
            deadline()
        if pending_sensitive and not character.isspace():
            raise Fault('sensitive-config-assignment-rejected')
        if character in ':=':
            key = quoted_segment if quoted_segment is not None else lower[segment:i]
            pending_sensitive = any(word in key for word in ['password', 'passwd', 'credential', 'secret', 'token', 'auth'])
            segment = i + 1
            quoted_segment = None
        elif character in '"\'':
            quoted_segment = lower[segment:i]
            segment = i + 1
        elif character in '#;,\r\n<>':
            segment = i + 1
            quoted_segment = None
        elif not character.isspace():
            quoted_segment = None
        if lower.startswith('http://', i) or lower.startswith('https://', i):
            url_start = i + (7 if lower.startswith('http://', i) else 8)
            url_colon = False
        if url_start is not None and i >= url_start:
            if character.isspace() or character == '/':
                url_start = None
            elif character == ':':
                url_colon = True
            elif character == '@' and url_colon:
                raise Fault('credential-bearing-URL-rejected')
    return text


def parse_text(text, kind):
    references = []
    if kind == 'JSON':
        parsed = strict_json(text.encode('utf-8'))
        return {'format': kind, 'value': parsed, 'unfollowedReferences': []}
    if kind == 'XML':
        require('<!DOCTYPE' not in text.upper() and '<!ENTITY' not in text.upper(), 'XML-entity-or-DOCTYPE-rejected')
        class BoundedTarget:
            def __init__(self):
                self.rows = []
                self.fragments = []
                self.stack = []
                self.fragment_count = 0

            def start(self, tag, attributes):
                deadline()
                require(len(self.stack) < 64 and len(self.rows) < 65536
                        and len(attributes) <= 1024, 'XML-depth-node-or-attribute-cap')
                if self.stack:
                    self.stack[-1][1] = True
                index = len(self.rows)
                self.rows.append({'tag': tag, 'attributes': dict(attributes), 'text': ''})
                self.fragments.append([])
                self.stack.append([index, False])
                if tag.rsplit('}', 1)[-1] in {'Import', 'UsingTask'}:
                    references.append({'tag': tag, 'attributes': dict(attributes), 'followed': False})

            def end(self, tag):
                deadline()
                require(self.stack and self.rows[self.stack[-1][0]]['tag'] == tag, 'XML-end-stack')
                self.stack.pop()

            def data(self, value):
                deadline()
                if self.stack and not self.stack[-1][1]:
                    require(self.fragment_count < 65536, 'XML-text-fragment-cap')
                    self.fragment_count += 1
                    self.fragments[self.stack[-1][0]].append(value)

            def close(self):
                require(not self.stack, 'XML-incomplete-stack')
                for row, fragments in zip(self.rows, self.fragments):
                    deadline()
                    row['text'] = ''.join(fragments)
                return self.rows

        parser = ET.XMLParser(target=BoundedTarget())
        for offset in range(0, len(text), 4096):
            deadline()
            parser.feed(text[offset:offset + 4096])
        rows = parser.close()
        return {'format': kind, 'value': rows, 'unfollowedReferences': references}
    lines = bounded_lines(text, 'text-line-cap')
    if kind in {'loader-config', 'OpenSSL-config', 'NSS-config'}:
        for line in lines:
            deadline()
            stripped = line.strip()
            if stripped.startswith(('#', ';')) or not stripped:
                continue
            if stripped.startswith(('include ', '.include ')) or kind == 'OpenSSL-config' and re.search(r'\b(module|dynamic_path|engine_id|default_keyfile)\s*=', stripped):
                # No reference string enters the receipt or public text if it
                # could point to an unreviewed/private file.
                references.append({'kind': 'config-reference', 'lineSha256': sha(line.encode()), 'followed': False})
        require(not references, 'unreviewed-config-reference-text-withheld')
    return {'format': kind, 'value': None, 'unfollowedReferences': references}


def bounded_lines(text, code):
    # Admit before retaining each line; splitlines() would allocate the complete
    # collection before the line cap could reject a tiny-record input.
    lines = []
    start = 0
    for i, c in enumerate(text):
        if i % 4096 == 0:
            deadline()
        if c in '\n\r\v\f\x1c\x1d\x1e\x85\u2028\u2029':
            if c == '\n' and i > 0 and text[i - 1] == '\r':
                start = i + 1
                continue
            require(len(lines) < 65536, code)
            lines.append(text[start:i])
            start = i + 1
    if start < len(text):
        require(len(lines) < 65536, code)
        lines.append(text[start:])
    return lines


def selected_text(row, pin, body):
    require(COUNTS['inputTextReads'] < 128 and COUNTS['inputTextBytes'] + len(body) <= TEXT_TOTAL, 'selected-text-budget')
    COUNTS['inputTextReads'] += 1
    COUNTS['inputTextBytes'] += len(body)
    require(COUNTS['inputTextReads'] <= 128 and len(body) <= TEXT_MAX
            and COUNTS['inputTextBytes'] <= TEXT_TOTAL, 'selected-text-budget')
    disposition = 'complete-public-text'
    parsed = None
    error_code = None
    try:
        text = public_content(body, inert_text_kind(row))
        parsed = parse_text(text, inert_text_kind(row))
    except Exception as error:
        disposition = 'hash-only-rejected-content'
        error_code = str(error) if type(error) is Fault else 'text-decoding-or-parser-fault'
        failure(row['selectorId'], error)
    artifact = None
    if disposition == 'complete-public-text':
        require(re.fullmatch(r'[a-z0-9-]+', row['selectorId']) is not None, 'public-text-fixed-name')
        reserve_output(len(body), report=False)
        parent = output_descriptor(public_text=True)
        descriptor = None
        name = row['selectorId'] + '.txt'
        try:
            descriptor = os.open(name, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                                 mode=0o600, dir_fd=parent)
            write_all(descriptor, body, WORK)
        finally:
            if descriptor is not None:
                os.close(descriptor)
            os.close(parent)
        artifact = {'path': 'public-text/' + name, 'bytes': len(body), 'sha256': sha(body)}
    return {'contentDisposition': disposition, 'UTF8Complete': disposition == 'complete-public-text',
            'textBytes': len(body), 'textSha256': sha(body), 'parserKind': inert_text_kind(row),
            'parsedRecords': parsed, 'publicTextArtifact': artifact, 'forbiddenContentFault': error_code,
            'unfollowedReferenceCount': len(parsed['unfollowedReferences']) if parsed is not None else None}


def bounded_unpack(fmt, body, offset, bound=None):
    n = struct.calcsize(fmt)
    require(type(offset) is int and offset >= 0 and offset + n <= (len(body) if bound is None else bound), 'binary-range')
    return struct.unpack_from(fmt, body, offset)


def elf(body):
    require(body[:4] == b'\x7fELF' and len(body) >= 64, 'ELF-header')
    require(body[4:7] == bytes([2, 1, 1]), 'ELF64-little-endian-current-only')
    et, machine, version = bounded_unpack('<HHI', body, 16)
    phoff = bounded_unpack('<Q', body, 32)[0]
    ehsize, phentsize, phnum = bounded_unpack('<HHH', body, 52)
    require(machine == 62 and version == 1 and ehsize == 64, 'ELF-x64-header-facts')
    result = {'e_type': et, 'e_machine': machine, 'e_version': version, 'e_ehsize': ehsize,
              'programHeaderOffset': phoff, 'programHeaderEntrySize': phentsize, 'programHeaderCount': phnum,
              'PT_INTERP': None, 'DT_NEEDED': [], 'DT_SONAME': None, 'DT_RPATH': None, 'DT_RUNPATH': None,
              'staticMetadataComplete': False, 'noExportsEnumerated': True}
    if et == 1:
        require(phoff == phentsize == phnum == 0, 'ET_REL-program-headers')
        result.update({'outsideLoadGraph': True, 'staticMetadataComplete': True})
        return result
    require(et in {2, 3} and phentsize == 56 and 0 < phnum <= 256 and phoff + phnum * 56 <= len(body), 'ELF-loadable-headers')
    segments = []
    dynamic = None
    for i in range(phnum):
        deadline()
        typ, flags, off, va, pa, fs, ms, align = bounded_unpack('<IIQQQQQQ', body, phoff + i * 56)
        require(off + fs <= len(body) and fs <= ms, 'ELF-segment-bounds')
        if typ == 1:
            segments.append((va, fs, off))
        elif typ == 2:
            require(dynamic is None and fs % 16 == 0 and fs // 16 <= 8192, 'ELF-dynamic-count')
            dynamic = (off, fs)
        elif typ == 3:
            require(result['PT_INTERP'] is None and 1 <= fs <= 4096, 'ELF-interpreter-size')
            s = body[off:off + fs]
            require(s[-1:] == b'\0' and b'\0' not in s[:-1], 'ELF-interpreter-string')
            result['PT_INTERP'] = s[:-1].decode('utf-8')
    require(dynamic is not None, 'ELF-no-dynamic-metadata')
    entries = []
    terminated = False
    for off in range(dynamic[0], dynamic[0] + dynamic[1], 16):
        deadline()
        tag, value = bounded_unpack('<qQ', body, off)
        if tag == 0:
            terminated = True
            break
        entries.append((tag, value))
    require(terminated, 'ELF-dynamic-unterminated')
    strings = [v for t, v in entries if t == 5]
    sizes = [v for t, v in entries if t == 10]
    require(len(strings) == len(sizes) == 1 and sizes[0] <= TEXT_MAX, 'ELF-string-table')
    candidates = [off + strings[0] - va for va, size, off in segments if va <= strings[0] and strings[0] + sizes[0] <= va + size]
    require(len(candidates) == 1 and candidates[0] + sizes[0] <= len(body), 'ELF-string-table-map')
    table = body[candidates[0]:candidates[0] + sizes[0]]
    for tag, value in entries:
        if tag not in {1, 14, 15, 29}:
            continue
        require(value < len(table), 'ELF-string-offset')
        end = table.find(b'\0', value)
        require(end >= value and end - value <= 4096, 'ELF-string-termination')
        name = table[value:end].decode('utf-8')
        if tag == 1:
            require(len(result['DT_NEEDED']) < 256, 'ELF-needed-cap')
            result['DT_NEEDED'].append(name)
        else:
            key = {14: 'DT_SONAME', 15: 'DT_RPATH', 29: 'DT_RUNPATH'}[tag]
            require(result[key] is None, 'ELF-duplicate-lookup-tag')
            result[key] = name
    result['staticMetadataComplete'] = True
    return result


def pe(body):
    require(body[:2] == b'MZ', 'PE-DOS-header')
    start = bounded_unpack('<I', body, 60)[0]
    require(body[start:start + 4] == b'PE\0\0', 'PE-signature')
    machine, sections, stamp, symbols, symbolcount, optional_size, flags = bounded_unpack('<HHIIIHH', body, start + 4)
    require(1 <= sections <= 96, 'PE-section-count')
    opt = start + 24
    magic = bounded_unpack('<H', body, opt)[0]
    require(magic in {0x10b, 0x20b}, 'PE-optional-format')
    directories = opt + (96 if magic == 0x10b else 112)
    count = bounded_unpack('<I', body, directories - 4)[0]
    require(count > 14 and directories + 15 * 8 <= opt + optional_size, 'PE-CLI-directory')
    section_rows = []
    for i in range(sections):
        offset = opt + optional_size + 40 * i
        virtual_size, va, raw_size, raw = bounded_unpack('<IIII', body, offset + 8)
        require(raw + raw_size <= len(body), 'PE-section-range')
        section_rows.append((va, raw_size, raw))
    def rva(value, length):
        candidates = [raw + value - va for va, size, raw in section_rows if va <= value and value + length <= va + size]
        require(len(candidates) == 1 and candidates[0] + length <= len(body), 'PE-RVA-map')
        return candidates[0]
    cli_rva, cli_size = bounded_unpack('<II', body, directories + 14 * 8)
    require(cli_size >= 72, 'PE-CLI-header')
    cli = rva(cli_rva, 72)
    metadata_rva, metadata_size = bounded_unpack('<II', body, cli + 8)
    require(metadata_size <= 16 * 1024 ** 2, 'PE-metadata-cap')
    meta = rva(metadata_rva, metadata_size)
    end = meta + metadata_size
    require(body[meta:meta + 4] == b'BSJB', 'CLI-metadata-signature')
    version_length = bounded_unpack('<I', body, meta + 12, end)[0]
    require(version_length <= 256, 'CLI-version-string-cap')
    cursor = meta + 16 + ((version_length + 3) & ~3)
    reserved, stream_count = bounded_unpack('<HH', body, cursor, end)
    require(stream_count <= 16, 'CLI-stream-count')
    cursor += 4
    streams = {}
    for _ in range(stream_count):
        off, size = bounded_unpack('<II', body, cursor, end)
        cursor += 8
        stop = body.find(b'\0', cursor, min(cursor + 32, end))
        require(stop >= cursor and meta + off + size <= end, 'CLI-stream-range')
        name = body[cursor:stop].decode('ascii')
        require(name not in streams, 'CLI-duplicate-stream')
        streams[name] = (meta + off, size)
        cursor += ((stop - cursor + 1 + 3) & ~3)
    require('#Strings' in streams and '#Blob' in streams and '#GUID' in streams
            and '#~' in streams and '#-' not in streams, 'CLI-compressed-streams-only')
    table, table_size = streams['#~']
    table_end = table + table_size
    reserved, major, minor, heaps, reserved2, valid, sorted_mask = bounded_unpack('<IBBBBQQ', body, table, table_end)
    require(valid >> 45 == 0 and major == 2 and heaps & ~7 == 0, 'CLI-table-version-or-mask')
    cursor = table + 24
    counts = [0] * 45
    for i in range(45):
        if valid >> i & 1:
            counts[i] = bounded_unpack('<I', body, cursor, table_end)[0]
            require(counts[i] <= 1048576, 'CLI-table-row-cap')
            cursor += 4
    require(counts[32] == 1 and counts[35] <= 256 and counts[0] == 1, 'CLI-assembly-denominator')
    require(all(counts[i] == 0 for i in [3, 5, 7, 19, 22]), 'CLI-pointer-tables-not-supported')
    def index(t):
        return 2 if counts[t] < 65536 else 4
    def coded(tables, bits):
        return 2 if max(counts[t] for t in tables) < (1 << (16 - bits)) else 4
    string = 4 if heaps & 1 else 2
    guid = 4 if heaps & 2 else 2
    blob = 4 if heaps & 4 else 2
    td = coded([2, 1, 27], 2)
    mr = coded([6, 10], 1)
    impl = coded([38, 35, 39], 2)
    has_ca_tables = [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43]
    layouts = [
        [2, string, guid, guid, guid], [coded([0, 26, 35, 1], 2), string, string],
        [4, string, string, td, index(4), index(6)], [index(4)], [2, string, blob], [index(6)],
        [4, 2, 2, string, blob, index(8)], [index(8)], [2, 2, string], [index(2), td],
        [coded([2, 1, 26, 6, 27], 3), string, blob], [2, coded([4, 8, 23], 2), blob],
        [coded(has_ca_tables, 5), coded([6, 10], 3), blob], [coded([4, 8], 1), blob],
        [2, coded([2, 6, 32], 2), blob], [2, 4, index(2)], [4, index(4)], [blob],
        [index(2), index(20)], [index(20)], [2, string, td], [index(2), index(23)], [index(23)],
        [2, string, blob], [2, index(6), coded([20, 23], 1)], [index(2), mr, mr],
        [string], [blob], [2, coded([4, 6], 1), string, index(26)], [4, index(4)], [4, 4], [4],
        [4, 2, 2, 2, 2, 4, blob, string, string], [4], [4, 4, 4],
        [2, 2, 2, 2, 4, blob, string, string, blob], [4, index(35)], [4, 4, 4, index(35)],
        [4, string, blob], [4, 4, string, string, impl], [4, 4, string, impl],
        [index(2), index(2)], [2, 2, coded([2, 6], 1), string], [mr, blob], [index(42), td]]
    offsets = []
    for i, layout in enumerate(layouts):
        offsets.append(cursor)
        cursor += counts[i] * sum(layout)
        require(cursor <= table_end, 'CLI-table-range')
    def row(t, n):
        require(1 <= n <= counts[t], 'CLI-row-index')
        pos = offsets[t] + (n - 1) * sum(layouts[t])
        values = []
        for size in layouts[t]:
            values.append(bounded_unpack('<H' if size == 2 else '<I', body, pos, table_end)[0])
            pos += size
        return values
    def text(n):
        start, size = streams['#Strings']
        require(n < size, 'CLI-string-index')
        stop = body.find(b'\0', start + n, min(start + size, start + n + 4097))
        require(stop >= start + n, 'CLI-string-cap')
        return body[start + n:stop].decode('utf-8')
    def compressed(data, pos):
        require(pos < len(data), 'CLI-compressed-integer')
        a = data[pos]
        if a < 128:
            return a, pos + 1
        if a & 0xc0 == 0x80:
            require(pos + 2 <= len(data), 'CLI-compressed-range')
            return ((a & 63) << 8) | data[pos + 1], pos + 2
        require(a & 0xe0 == 0xc0 and pos + 4 <= len(data), 'CLI-compressed-range')
        return ((a & 31) << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3], pos + 4
    def blob_bytes(n):
        start, size = streams['#Blob']
        heap = memoryview(body)[start:start + size]
        length, position = compressed(heap, n)
        require(length <= TEXT_MAX and position + length <= len(heap), 'CLI-blob-cap-or-range')
        return heap[position:position + length]
    def assembly(t, n):
        v = row(t, n)
        versions = v[1:5] if t == 32 else v[:4]
        flags = v[5] if t == 32 else v[4]
        key = blob_bytes(v[6] if t == 32 else v[5])
        require(len(key) <= 1024, 'CLI-public-key-cap')
        token = hashlib.sha1(key).digest()[-8:][::-1].hex() if flags & 1 and key else key.hex()
        return {'name': text(v[7] if t == 32 else v[6]), 'version': '.'.join(map(str, versions)),
                'culture': text(v[8] if t == 32 else v[7]), 'flags': flags,
                'publicKeyOrTokenHex': key.hex(), 'publicKeyTokenHex': token}
    module = row(0, 1)
    guid_start, guid_size = streams['#GUID']
    require(1 <= module[2] and module[2] * 16 <= guid_size, 'CLI-MVID-index')
    mvid = body[guid_start + (module[2] - 1) * 16:guid_start + module[2] * 16].hex()
    facts = []
    admitted_attributes = set()
    eligible = {'System.Reflection.AssemblyInformationalVersionAttribute',
                'System.Reflection.AssemblyFileVersionAttribute', 'System.Runtime.Versioning.TargetFrameworkAttribute'}
    for n in range(1, counts[12] + 1):
        deadline()
        parent, ctor, value = row(12, n)
        if parent & 31 != 14 or parent >> 5 != 1 or ctor & 7 != 3:
            continue
        member = row(10, ctor >> 3)
        if member[0] & 7 != 1 or text(member[1]) != '.ctor':
            continue
        typeref = row(1, member[0] >> 3)
        attribute = text(typeref[2]) + '.' + text(typeref[1])
        if attribute not in eligible:
            continue
        require(len(admitted_attributes) < 3 and attribute not in admitted_attributes,
                'CLI-attribute-denominator')
        admitted_attributes.add(attribute)
        require(blob_bytes(member[2]) == b'\x20\x01\x01\x0e', 'CLI-known-single-string-constructor-signature')
        encoded = blob_bytes(value)
        require(encoded[:2] == b'\x01\x00' and len(encoded) > 2 and encoded[2] != 255, 'CLI-attribute-string-form')
        length, position = compressed(encoded, 2)
        require(length <= 4096 and position + length + 2 <= len(encoded), 'CLI-attribute-string-range')
        named = bounded_unpack('<H', encoded, position + length)[0]
        require(named <= 128, 'CLI-attribute-named-cap')
        facts.append({'attribute': attribute, 'constructorString': encoded[position:position + length].tobytes().decode('utf-8'),
                      'namedArgumentCount': named, 'remainingBlobSha256': sha(encoded[position + length:]),
                      'completeNamedArgumentsDecoded': named == 0 and position + length + 2 == len(encoded)})
    require(len(facts) <= 3 and len({x['attribute'] for x in facts}) == len(facts), 'CLI-attribute-denominator')
    return {'assemblyIdentity': assembly(32, 1), 'AssemblyRefRows': [assembly(35, n) for n in range(1, counts[35] + 1)],
            'MVIDHex': mvid, 'customAttributeStringFacts': facts, 'metadataBytes': metadata_size,
            'metadataTableRows': counts, 'customAttributeConstructorsInvoked': False, 'assemblyLoaded': False}


def observation(row):
    resolution = resolve(row['path'], ALLOWED)
    if resolution['resolvedPath'] is None:
        record = {'selectorId': row['selectorId'], 'group': row['group'], 'kind': row['kind'],
                  'file': {'requestedPath': row['path'], **resolution, 'bytes': None, 'sha256': None,
                           'readBefore': None, 'readAfter': None, 'stable': False},
                  'archivedComparison': compare(row['path'], None), 'text': None, 'binary': None, 'captureFault': None}
        if row['presence'] == 'required-present':
            failure(row['selectorId'], Fault('required-selector-absent'))
        return record
    text = row['kind'] == 'public-detached-text'
    binary = row['kind'] in {'bounded-static-PE-identity-and-AssemblyRef', 'bounded-alias-and-ELF-dynamic-metadata',
                            'hash-and-bounded-ELF-metadata', 'typed-file-or-alias-record'}
    # ld.so.cache is raw metadata: never execute its loader. Cache bytes are
    # retained only by hash here; candidate cache-selection source remains pending.
    if row['path'].endswith('ld.so.cache'):
        binary = False
    if text:
        require(COUNTS['inputTextReads'] < 128, 'selected-text-role-cap')
    pin, data = read_file(row['path'], ALLOWED, TEXT_MAX if text else FILE_MAX, keep=text or binary, text_role=text)
    record = {'selectorId': row['selectorId'], 'group': row['group'], 'kind': row['kind'], 'file': pin,
              'archivedComparison': compare(row['path'], pin), 'text': None, 'binary': None, 'captureFault': None}
    if text:
        record['text'] = selected_text(row, pin, data)
    elif binary:
        try:
            if row['kind'] == 'bounded-static-PE-identity-and-AssemblyRef':
                record['binary'] = pe(data)
            elif data[:4] == b'\x7fELF':
                NATIVE_IMAGES.add(pin['resolvedPath'])
                require(len(NATIVE_IMAGES) <= 128, 'resolved-native-image-cap')
                record['binary'] = elf(data)
            else:
                # The alternate sysconfig spelling is an inert alias/hash role.
                require(row['path'].endswith('.py'), 'required-ELF-signature')
        except Exception as error:
            failure(row['selectorId'], error)
            record['captureFault'] = {'code': str(error)[:128] if type(error) is Fault else 'binary-parser-fault',
                                      'exceptionType': type(error).__name__}
    return record


def packages():
    status_name = DOC['packageStatus']['path']
    pin, body = read_file(status_name, ALLOWED, 16 * 1024 ** 2, keep=True, status_stream=True)
    # The status stream has its separate 16 MiB cap and consumes no 128-role
    # slot. Its captured bytes still charge the shared 64 MiB budget before
    # decoding or parsing any paragraph.
    COUNTS['statusTextBytes'] += len(body)
    COUNTS['inputTextBytes'] += len(body)
    require(COUNTS['statusTextBytes'] <= 16 * 1024 ** 2
            and COUNTS['inputTextBytes'] <= TEXT_TOTAL, 'status-text-budget')
    text = body.decode('utf-8')
    selected = []
    fields_allowed = set(DOC['packageStatus']['paragraphFields'])
    identities = set()
    paragraph_start = 0
    while paragraph_start < len(text):
        deadline()
        paragraph_end = text.find('\n\n', paragraph_start)
        if paragraph_end < 0:
            paragraph_end = len(text)
        next_paragraph = paragraph_end + 2
        if paragraph_end == paragraph_start:
            paragraph_start = next_paragraph
            continue
        fragments = {}
        field_names = set()
        key = None
        line_start = paragraph_start
        line_count = 0
        while line_start < paragraph_end:
            deadline()
            require(line_count < 65536, 'status-paragraph-line-cap')
            line_count += 1
            line_end = text.find('\n', line_start, paragraph_end)
            if line_end < 0:
                line_end = paragraph_end
            require(line_end - line_start <= 65536, 'status-line-size-cap')
            line = text[line_start:line_end]
            line_start = line_end + 1
            if line.startswith((' ', '\t')):
                require(key is not None, 'status-continuation')
                if key in fields_allowed:
                    fragments[key].append(line)
            else:
                require(':' in line, 'status-field-form')
                colon = line.find(':')
                require(0 < colon <= 256 and len(field_names) < 256, 'status-field-count-or-name-cap')
                key = line[:colon]
                require(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9-]*', key) is not None, 'status-field-name')
                require(key not in field_names, 'status-duplicate-field')
                field_names.add(key)
                if key in fields_allowed:
                    fragments[key] = [line[colon + 1:].lstrip()]
        fields = {}
        for field, parts in fragments.items():
            deadline()
            fields[field] = '\n'.join(parts)
        name = fields.get('Package')
        start = paragraph_start
        paragraph_start = next_paragraph
        if name not in DOC['packageStatus']['packageNames']:
            continue
        architecture = fields.get('Architecture')
        if architecture not in {'amd64', 'all'}:
            continue
        ident = (name, architecture)
        require(ident not in identities, 'duplicate-selected-status-identity')
        identities.add(ident)
        COUNTS['selectedStatusParagraphs'] += 1
        require(COUNTS['selectedStatusParagraphs'] <= 512, 'status-selected-paragraph-cap')
        public_content(json.dumps({k: v for k, v in fields.items() if k in fields_allowed}).encode('utf-8'), 'JSON')
        selected.append({'exactName': name, 'architecture': architecture,
                         'paragraphSha256': sha(text[start:paragraph_end].encode('utf-8')),
                         'selectedFields': {k: v for k, v in fields.items() if k in fields_allowed},
                         'matchingInfoFiles': [], 'declaredOwnershipOnly': True})
    by_name = {row['exactName']: row for row in selected}
    require(len(by_name) == len(selected), 'ambiguous-selected-package-name')
    results = []
    for name in DOC['packageStatus']['packageNames']:
        deadline()
        row = by_name.get(name)
        result = {'packageName': name, 'statusRecord': row, 'infoFiles': [], 'missingPackage': row is None}
        for suffix in ['.list', '.md5sums']:
            options = ['/var/lib/dpkg/info/' + name + x + suffix for x in ['', ':amd64']]
            present = []
            absent = []
            for option in options:
                resolution = resolve(option, ALLOWED)
                if resolution['resolvedPath'] is None:
                    absent.append({'path': option, **resolution})
                else:
                    present.append(option)
            require(len(present) <= 1, 'ambiguous-package-info-alternatives')
            if not present:
                result['infoFiles'].append({'suffix': suffix, 'absence': absent, 'file': None, 'selectedEntries': []})
                continue
            require(row is not None, 'info-file-without-selected-package')
            require(COUNTS['inputTextReads'] < 128, 'package-text-role-cap')
            info, contents = read_file(present[0], ALLOWED, TEXT_MAX, keep=True, text_role=True)
            COUNTS['inputTextReads'] += 1
            COUNTS['inputTextBytes'] += len(contents)
            require(COUNTS['inputTextReads'] <= 128 and COUNTS['inputTextBytes'] <= TEXT_TOTAL, 'package-text-budget')
            lines = bounded_lines(contents.decode('utf-8'), 'package-info-line-cap')
            entries = []
            withheld = []
            for line in lines:
                deadline()
                if suffix == '.md5sums':
                    match = re.fullmatch(r'([0-9a-fA-F]{32})\s+(.+)', line)
                    require(match is not None, 'package-md5-record-form')
                    claimed = '/' + match[2].lstrip('/')
                    digest = match[1].lower()
                else:
                    claimed = line
                    digest = None
                if forbidden_path(claimed):
                    withheld.append(sha(line.encode()))
                elif claimed in ALLOWED:
                    entries.append({'path': claimed, 'declaredMD5': digest,
                                    'note': 'package-declaration-not-authenticity-or-source-correspondence'})
            result['infoFiles'].append({'suffix': suffix, 'absence': absent, 'file': info,
                'selectedEntries': entries, 'withheldEntryCount': len(withheld),
                'withheldEntriesSha256': sha('\n'.join(withheld).encode()), 'arbitraryOwnedFilesOpened': False})
        results.append(result)
    return {'statusFile': pin, 'packages': results, 'allOtherParagraphsDetached': False}


def directories(until=WORK, track=True):
    result = []
    for row in DOC['shallowDirectorySelectors']:
        deadline(until)
        p = Path(row['path'])
        resolution = resolve(str(p), ALLOWED, until, track=track)
        if resolution['resolvedPath'] is None:
            result.append({'selectorPath': str(p), 'resolution': resolution, 'entries': None})
            continue
        path = resolution['resolvedPath']
        descriptor = directory_descriptor(path, until)
        names = []
        try:
            before = os.fstat(descriptor)
            with os.scandir(descriptor) as entries:
                for entry in entries:
                    deadline(until)
                    require(len(names) < row['maximumImmediateEntries'], 'shallow-directory-entry-cap')
                    names.append(entry.name)
            after = os.fstat(descriptor)
        finally:
            os.close(descriptor)
        require(resolve(str(p), ALLOWED, until, track=False) == resolution, 'shallow-alias-changed')
        result.append({'selectorPath': str(p), 'resolution': resolution,
                       'lstatBefore': identity(before), 'lstatAfter': identity(after),
                       'eligiblePresence': {name: name in names for name in row['eligibleNames']},
                       'totalImmediateCount': len(names), 'boundedNamesSha256': sha('\0'.join(sorted(names)).encode()),
                       'unknownImmediateCount': len(set(names) - set(row['eligibleNames'])),
                       'changed': identity(before) != identity(after), 'recursive': False})
    return result


def archive_texts():
    origin, _ = source_read(str(SOURCE / 'historical-text-origins.json'))
    origins = strict_json(origin)
    require(origins['sourceZipSha256'] == DOC['archiveReference']['zipSha256'] and len(origins['files']) == 5,
            'historical-text-origin-boundary')
    results = []
    for i, (selector, row) in enumerate(zip(DOC['archiveTextSelectors'], origins['files'])):
        require((selector['archive'], selector['member']) == (row['archive'], row['member']), 'historical-text-member-selection')
        require(COUNTS['inputTextReads'] < 128, 'historical-text-role-cap')
        body, pin = source_read(str(SOURCE / row['path']), maximum=TEXT_MAX, text_role=True)
        require(pin['bytes'] == row['bytes'] and pin['sha256'] == row['sha256'], 'historical-member-bytes')
        parsed = selected_text({'selectorId': 'historical-text-' + str(i + 1), 'path': row['member']}, pin, body)
        results.append({'origin': row, 'file': pin, 'text': parsed, 'reusedHistoricalData': True,
                        'freshArchiveDownloaded': False, 'currentRunnerProvenance': False})
    return results


def validate_contract():
    global WIRE
    raw, pin = source_read(str(SOURCE / 'SELECTORS.json'))
    require(pin['sha256'] == SELECTOR_SHA, 'selector-bytes-changed')
    doc = strict_json(raw)
    primary_raw, primary_pin = source_read(str(SOURCE / 'PRIMARY-SOURCE-PINS.json'))
    require(primary_pin['sha256'] == doc['sourceIndexManifestSha256'], 'primary-source-index-pin')
    primary = strict_json(primary_raw)
    require(type(primary['files']) is list and len(primary['files']) == 86,
            'primary-source-index-denominator')
    # Public IDs are one-based ordinals of this exact frozen files array,
    # never filesystem paths from the separate private review-location aid.
    source_ids = {'P' + str(i).zfill(3) for i in range(1, 87)}
    require(all(type(row['sourceOwners']) is list
                and all(owner in source_ids for owner in row['sourceOwners'])
                for row in doc['selectors']), 'primary-source-owner-ordinal')
    require(doc['schemaVersion'] == 1 and doc['scope'].startswith('PROPOSED-fixed-finite-'), 'selector-contract-version')
    require(type(doc['sourceImplementationAuthorized']) is bool and doc['sourceImplementationAuthorized'] is False
            and doc['runtimeExecutionAuthorized'] is False, 'no-caller-approval-fields')
    selectors = doc['selectors']
    require(len(selectors) == 265 and len({r['selectorId'] for r in selectors}) == 265
            and len({r['path'] for r in selectors}) == 263, 'selector-fixed-denominator')
    require(len(doc['archivedPythonModuleAssociations']) == 101 and len(doc['packageStatus']['packageNames']) == 32
            and len(doc['archiveTextSelectors']) == 5 and len(doc['shallowDirectorySelectors']) == 6, 'finite-data-counts')
    require(doc['bounds']['computedMaximumTextReads'] == 128 and doc['bounds']['textReadsMaximum'] == 128,
            'text-role-count')
    wire_raw, wire_pin = source_read(str(SOURCE / 'OUTPUT-SCHEMA.json'))
    require(wire_pin['sha256'] == WIRE_SCHEMA_SHA, 'wire-schema-source-pin')
    WIRE = strict_json(wire_raw)
    require(WIRE['schemaVersion'] == 2 and WIRE['selectorDocumentSha256'] == SELECTOR_SHA
            and WIRE['frozenConceptualRequiredNamesEnforcedUnchanged'] is False
            and WIRE['inputAndScopeWidened'] is False
            and WIRE['unknownFieldsAllowedOutsideDeclaredOptionalKeys'] is False, 'wire-schema-version-and-boundaries')
    require(set(WIRE['conceptualProjection']) == set(doc['schemas']), 'wire-projection-family-set')
    for family, conceptual in doc['schemas'].items():
        deadline()
        require(set(WIRE['conceptualProjection'][family]) == set(conceptual['required']), 'wire-projection-field-set')
    return doc


def python_host():
    modules = []
    for name, module in sorted(sys.modules.items()):
        path = getattr(module, '__file__', None)
        if isinstance(path, str):
            modules.append({'name': name, 'declaredFile': path, 'loadedAsInterpreterModule': True})
    require(len(modules) <= 512, 'interpreter-module-count')
    return {'version': sys.version, 'executable': sys.executable, 'isolated': sys.flags.isolated == 1,
            'noSite': sys.flags.no_site == 1, 'dontWriteBytecode': sys.dont_write_bytecode is True,
            'effectiveUID': os.geteuid(), 'modules': modules,
            'ordinaryInterpreterStartupAndDeclaredImportsExecuted': True,
            'interpreterSourceToBinaryClosureQualified': False,
            'allObservedModuleFilesRehashedInThisPacket': False}


def recheck_rows(until):
    for pin in list(CHECKS):
        deadline(until)
        try:
            current, _ = read_file(pin['requestedPath'], ALLOWED, until=until, track=False)
            require(current['bytes'] == pin['bytes'] and current['sha256'] == pin['sha256']
                    and current['resolvedPath'] == pin['resolvedPath']
                    and current['linkObservations'] == pin['linkObservations'], 'selected-final-byte-change')
        except Exception as error:
            failure('final-selected-file-recheck', error)
    for pin in list(ABSENCE_CHECKS):
        deadline(until)
        try:
            require(resolve(pin['requestedPath'], ALLOWED, until, track=False) == pin['resolution'],
                    'selected-final-absence-change')
        except Exception as error:
            failure('final-selected-absence-recheck', error)


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


def publish(name, value, until):
    require(name in {'corroboration.json', 'summary.json'}, 'fixed-report-name')
    available = min(REPORT_MAX - OUTPUT_COUNTS['JSONReportBytesReserved'],
                    TEXT_TOTAL - OUTPUT_COUNTS['allArtifactBytesReserved'])
    count = 1  # final newline
    require(count <= available, 'report-preflight-byte-cap')
    for token in json_tokens(value, until):
        deadline(until)
        count += len(token)
        require(count <= available, 'report-preflight-byte-cap')
    parent = output_descriptor(until=until)
    descriptor = None
    length = 0
    digest = hashlib.sha256()
    try:
        descriptor = os.open(name + '.pending', os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC,
                             mode=0o600, dir_fd=parent)
        for token in json_tokens(value, until):
            deadline(until)
            reserve_output(len(token), report=True)
            write_all(descriptor, token, until)
            length += len(token)
            digest.update(token)
        reserve_output(1, report=True)
        write_all(descriptor, b'\n', until)
        length += 1
        digest.update(b'\n')
        require(length == count, 'report-preflight-emission-disagrees')
        os.fsync(descriptor)
        deadline(until)
        os.rename(name + '.pending', name, src_dir_fd=parent, dst_dir_fd=parent)
    finally:
        if descriptor is not None:
            os.close(descriptor)
        os.close(parent)
    return {'path': name, 'bytes': length, 'sha256': digest.hexdigest()}


def wire_record(family, value, variant, until):
    deadline(until)
    require(WIRE is not None, 'wire-schema-not-captured')
    record = WIRE['records'][family]['variants'][variant]
    if record['type'] == 'null':
        require(value is None, 'wire-null-variant')
        return
    require(record['type'] == 'dict' and type(value) is dict
            and set(record['required']) <= set(value) <= set(record['allowed']), 'wire-record-exact-key-contract')
    primitive = {'dict': dict, 'list': list, 'str': str, 'bool': bool, 'int': int, 'null': type(None)}
    for name, tag in record['fieldTypes'].items():
        deadline(until)
        if name not in value:
            continue
        nullable = tag.startswith('nullable-')
        base = tag[9:] if nullable else tag
        require(base in primitive and (nullable and value[name] is None or type(value[name]) is primitive[base]),
                'wire-record-primitive-type')


def validate_evidence(evidence, summary, until):
    deadline(until)
    if WIRE is not None:
        wire_record('PacketEvidence', evidence, 'record', until)
        wire_record('PacketSummary', summary, 'record', until)
    else:
        require(summary['passed'] is False and not evidence['observations']
                and evidence['packages'] is None and evidence['directories'] is None
                and evidence['historicalText'] is None, 'uncaptured-wire-schema-cannot-accept-data')
    require(set(evidence) == {'schemaVersion', 'scope', 'observations', 'packages', 'directories', 'historicalText'}, 'evidence-exact-schema')
    require(type(evidence['schemaVersion']) is int and evidence['schemaVersion'] == 2, 'evidence-version-type')
    required_summary = {'schemaVersion', 'scope', 'captureCompleted', 'passed', 'selectorDocumentSha256',
        'sourcePinsBefore', 'sourcePinsAfter', 'counts', 'exactSelectorCountExpected', 'exactSelectorCountObserved',
        'primaryFault', 'terminalWorkFault', 'finalizationFaults', 'observationFaults', 'faultLedgerOverflow', 'archiveReference',
        'nineOriginalMissingEdgesRemainUnresolved', 'fourOriginalSourceContractsRemainPending',
        'completeDynamicLoadClosure', 'reviewedRunnerClosure', 'fullDiagnosticEnabled', 'SDKExecuted',
        'collectorExecuted', 'analyzerExecuted', 'targetExecuted', 'nativeProofOrCostParityAccepted',
        'originalScannerInvokedOrImported', 'newArchiveDownloaded', 'interpreter', 'historicalDataReused',
        'changedRunnerFactsAreNewInputs', 'stabilityScope', 'wireSchemaSha256',
        'frozenConceptualRequiredNamesEnforcedUnchanged', 'outputReservationsBeforeSummary', 'wireSchemaCaptured'}
    require(set(summary) == required_summary, 'summary-exact-schema')
    require(type(summary['schemaVersion']) is int and summary['schemaVersion'] == 2
            and summary['wireSchemaSha256'] == WIRE_SCHEMA_SHA, 'summary-wire-version-pin')
    false_names = ['completeDynamicLoadClosure', 'reviewedRunnerClosure', 'fullDiagnosticEnabled', 'SDKExecuted',
        'collectorExecuted', 'analyzerExecuted', 'targetExecuted', 'nativeProofOrCostParityAccepted',
        'originalScannerInvokedOrImported', 'newArchiveDownloaded', 'frozenConceptualRequiredNamesEnforcedUnchanged']
    require(all(summary[n] is False for n in false_names), 'no-execution-or-closure-claims')
    require(all(type(summary[n]) is bool for n in ['captureCompleted', 'passed', 'faultLedgerOverflow',
        'nineOriginalMissingEdgesRemainUnresolved', 'fourOriginalSourceContractsRemainPending',
        'historicalDataReused', 'changedRunnerFactsAreNewInputs']), 'critical-boolean-types')
    require(summary['captureCompleted'] == summary['passed'], 'capture-status-disagrees')
    require(type(summary['wireSchemaCaptured']) is bool and summary['wireSchemaCaptured'] == (WIRE is not None),
            'wire-schema-capture-fact')
    require(summary['nineOriginalMissingEdgesRemainUnresolved'] is True
            and summary['fourOriginalSourceContractsRemainPending'] is True
            and summary['changedRunnerFactsAreNewInputs'] is True, 'pending-contracts-remain-pending')
    require(summary['exactSelectorCountExpected'] == 265
            and summary['exactSelectorCountObserved'] == len(evidence['observations']), 'reported-selector-denominator')
    require(set(summary['counts']) == {'inputTextReads', 'inputTextBytes', 'statusTextBytes', 'selectedStatusParagraphs',
        'attemptedInputTextReads', 'attemptedInputTextBytes', 'attemptedStatusBytes',
        'hashFileBytes', 'controlReads', 'controlBytes'} and all(type(n) is int and n >= 0 for n in summary['counts'].values()), 'count-exact-types')
    known = {r['selectorId']: r for r in DOC['selectors']} if DOC else {}
    seen = set()
    for row in evidence['observations']:
        deadline(until)
        wire_record('Observation', row, 'record', until)
        require(set(row) == {'selectorId', 'group', 'kind', 'file', 'archivedComparison', 'text', 'binary', 'captureFault'}, 'observation-exact-schema')
        ident = row['selectorId']
        require(ident in known and ident not in seen and row['group'] == known[ident]['group']
                and row['kind'] == known[ident]['kind'], 'observation-selector-binding')
        seen.add(ident)
        pin = row['file']
        wire_record('FileObservation', pin, 'record' if pin is not None else 'unavailable', until)
        if pin is not None:
            require(set(pin) == {'requestedPath', 'presence', 'resolvedPath', 'linkObservations', 'absence',
                'readBefore', 'readAfter', 'bytes', 'sha256', 'stable'} and pin['requestedPath'] == known[ident]['path'], 'file-observation-exact-schema')
            require(pin['presence'] in {'regular', 'alias-to-regular', 'absent-ENOENT', 'absent-ENOTDIR'} and type(pin['stable']) is bool, 'file-observation-types')
            if pin['sha256'] is not None:
                require(type(pin['bytes']) is int and 0 <= pin['bytes'] <= FILE_MAX
                        and re.fullmatch(r'[0-9a-f]{64}', pin['sha256']) is not None and pin['resolvedPath'] in ALLOWED, 'file-observation-pin')
            else:
                require(pin['presence'].startswith('absent-') and pin['bytes'] is None and pin['absence'] is not None, 'absence-observation-facts')
                wire_record('Absence', pin['absence'], 'record', until)
                require(pin['absence']['parentReadableNoFollow'] is True, 'absence-readable-parent')
        text = row['text']
        wire_record('TextObservation', text, 'record' if text is not None else 'unavailable', until)
        if text is not None:
            require(set(text) == {'contentDisposition', 'UTF8Complete', 'textBytes', 'textSha256', 'parserKind',
                'parsedRecords', 'publicTextArtifact', 'forbiddenContentFault', 'unfollowedReferenceCount'}, 'text-observation-exact-schema')
            require(type(text['UTF8Complete']) is bool and type(text['textBytes']) is int
                    and text['textBytes'] <= TEXT_MAX and text['textSha256'] == pin['sha256'], 'text-byte-binding')
            if text['contentDisposition'] == 'complete-public-text':
                require(text['UTF8Complete'] is True and text['forbiddenContentFault'] is None
                        and text['publicTextArtifact']['sha256'] == pin['sha256'], 'public-text-binding')
            else:
                require(text['contentDisposition'] == 'hash-only-rejected-content' and text['publicTextArtifact'] is None, 'rejected-text-no-public-body')
        binary = row['binary']
        if binary is not None:
            require(pin is not None and pin['sha256'] is not None and text is None, 'binary-owner-byte-pin')
            if row['kind'] == 'bounded-static-PE-identity-and-AssemblyRef':
                wire_record('PEObservation', binary, 'record', until)
                require(set(binary) == {'assemblyIdentity', 'AssemblyRefRows', 'MVIDHex', 'customAttributeStringFacts',
                    'metadataBytes', 'metadataTableRows', 'customAttributeConstructorsInvoked', 'assemblyLoaded'}, 'PE-exact-schema')
                require(binary['assemblyLoaded'] is False and binary['customAttributeConstructorsInvoked'] is False
                        and type(binary['metadataBytes']) is int and 0 <= binary['metadataBytes'] <= 16 * 1024 ** 2
                        and len(binary['AssemblyRefRows']) <= 256 and len(binary['metadataTableRows']) == 45
                        and all(type(n) is int and 0 <= n <= 1048576 for n in binary['metadataTableRows'])
                        and len(binary['customAttributeStringFacts']) <= 3, 'PE-bounds-and-no-body-execution')
                require(re.fullmatch(r'[0-9a-f]{32}', binary['MVIDHex']) is not None, 'PE-MVID-hex')
                for assembly in [binary['assemblyIdentity'], *binary['AssemblyRefRows']]:
                    deadline(until)
                    require(set(assembly) == {'name', 'version', 'culture', 'flags', 'publicKeyOrTokenHex', 'publicKeyTokenHex'}
                            and type(assembly['flags']) is int and 0 <= assembly['flags'] <= 4294967295
                            and all(type(assembly[n]) is str for n in ['name', 'version', 'culture', 'publicKeyOrTokenHex', 'publicKeyTokenHex']),
                            'PE-assembly-identity-schema')
                for fact in binary['customAttributeStringFacts']:
                    deadline(until)
                    require(set(fact) == {'attribute', 'constructorString', 'namedArgumentCount', 'remainingBlobSha256',
                        'completeNamedArgumentsDecoded'} and type(fact['completeNamedArgumentsDecoded']) is bool
                        and type(fact['constructorString']) is str and type(fact['namedArgumentCount']) is int
                        and 0 <= fact['namedArgumentCount'] <= 128, 'PE-detached-attribute-schema')
            else:
                wire_record('ELFObservation', binary, 'relocatable' if binary['e_type'] == 1 else 'loadable', until)
                base_keys = {'e_type', 'e_machine', 'e_version', 'e_ehsize', 'programHeaderOffset', 'programHeaderEntrySize',
                    'programHeaderCount', 'PT_INTERP', 'DT_NEEDED', 'DT_SONAME', 'DT_RPATH', 'DT_RUNPATH',
                    'staticMetadataComplete', 'noExportsEnumerated'}
                require(set(binary) == base_keys | ({'outsideLoadGraph'} if binary['e_type'] == 1 else set()), 'ELF-exact-schema')
                require(binary['e_type'] in {1, 2, 3} and binary['e_machine'] == 62 and binary['e_version'] == 1
                        and binary['e_ehsize'] == 64 and binary['staticMetadataComplete'] is True
                        and binary['noExportsEnumerated'] is True and len(binary['DT_NEEDED']) <= 256
                        and all(type(n) is str and len(n.encode('utf-8')) <= 4096 for n in binary['DT_NEEDED']), 'ELF-detached-facts')
                require(binary['e_type'] != 1 or binary['outsideLoadGraph'] is True, 'ELF-relocatable-outside-load-graph')
    # Failed captures may still carry complete earlier groups. Their record
    # variants get the same executable structural checks before publication;
    # incompleteness never bypasses the declared wire shape.
    if evidence['packages'] is not None:
        deadline(until)
        package = evidence['packages']
        require(set(package) == {'statusFile', 'packages', 'allOtherParagraphsDetached'}
                and package['allOtherParagraphsDetached'] is False
                and type(package['packages']) is list and len(package['packages']) <= 32,
                'package-collection-structural-contract')
        wire_record('FileObservation', package['statusFile'], 'record', until)
        for row in package['packages']:
            deadline(until)
            wire_record('PackageObservation', row, 'record', until)
            wire_record('PackageStatus', row['statusRecord'],
                        'record' if row['statusRecord'] is not None else 'unavailable', until)
            require(len(row['infoFiles']) <= 2, 'package-info-structural-count')
            for info in row['infoFiles']:
                deadline(until)
                wire_record('PackageInfo', info, 'record' if info['file'] is not None else 'absent', until)
                if info['file'] is not None:
                    wire_record('FileObservation', info['file'], 'record', until)
    if evidence['directories'] is not None:
        require(len(evidence['directories']) <= 6, 'directory-structural-count')
        for row in evidence['directories']:
            deadline(until)
            wire_record('DirectoryObservation', row,
                        'absent' if row['resolution']['resolvedPath'] is None else 'present', until)
    if evidence['historicalText'] is not None:
        require(len(evidence['historicalText']) <= 5, 'historical-text-structural-count')
        for row in evidence['historicalText']:
            deadline(until)
            wire_record('TextObservation', row['text'], 'record', until)
    if summary['passed']:
        require(len(seen) == 265 and not summary['observationFaults'] and summary['primaryFault'] is None
                and not summary['finalizationFaults'] and not summary['faultLedgerOverflow'], 'passed-capture-denominator')
        require(summary['sourcePinsBefore'] is not None and summary['sourcePinsBefore'] == summary['sourcePinsAfter'], 'passed-source-boundaries')
        require(COUNTS['inputTextReads'] <= 128 and COUNTS['inputTextBytes'] <= TEXT_TOTAL
                and COUNTS['attemptedInputTextReads'] <= 128 and COUNTS['attemptedInputTextBytes'] <= TEXT_TOTAL
                and COUNTS['attemptedStatusBytes'] <= 16 * 1024 ** 2
                and COUNTS['statusTextBytes'] <= 16 * 1024 ** 2
                and COUNTS['selectedStatusParagraphs'] <= 512, 'passed-input-budget')
        require(evidence['packages'] is not None and len(evidence['packages']['packages']) == 32
                and evidence['directories'] is not None and len(evidence['directories']) == 6
                and evidence['historicalText'] is not None and len(evidence['historicalText']) == 5, 'passed-data-denominators')
        package = evidence['packages']
        require(set(package) == {'statusFile', 'packages', 'allOtherParagraphsDetached'}
                and package['allOtherParagraphsDetached'] is False
                and package['statusFile']['requestedPath'] == DOC['packageStatus']['path']
                and package['statusFile']['bytes'] == COUNTS['statusTextBytes'], 'selected-status-stream-binding')
        role_count = sum(r['text'] is not None for r in evidence['observations'])
        role_bytes = sum(r['text']['textBytes'] for r in evidence['observations'] if r['text'] is not None)
        for row, name in zip(package['packages'], DOC['packageStatus']['packageNames']):
            deadline(until)
            wire_record('PackageObservation', row, 'record', until)
            require(set(row) == {'packageName', 'statusRecord', 'infoFiles', 'missingPackage'}
                    and row['packageName'] == name and type(row['missingPackage']) is bool
                    and row['missingPackage'] == (row['statusRecord'] is None) and len(row['infoFiles']) == 2, 'package-observation-schema')
            status = row['statusRecord']
            wire_record('PackageStatus', status, 'record' if status is not None else 'unavailable', until)
            if status is not None:
                require(set(status) == {'exactName', 'architecture', 'paragraphSha256', 'selectedFields', 'matchingInfoFiles', 'declaredOwnershipOnly'}
                        and status['exactName'] == name and status['architecture'] in {'amd64', 'all'}
                        and status['declaredOwnershipOnly'] is True and status['matchingInfoFiles'] == []
                        and set(status['selectedFields']) <= set(DOC['packageStatus']['paragraphFields']), 'package-status-schema')
            for info, suffix in zip(row['infoFiles'], ['.list', '.md5sums']):
                deadline(until)
                wire_record('PackageInfo', info, 'record' if info['file'] is not None else 'absent', until)
                require(info['suffix'] == suffix, 'package-info-suffix')
                if info['file'] is not None:
                    require(set(info) == {'suffix', 'absence', 'file', 'selectedEntries', 'withheldEntryCount',
                        'withheldEntriesSha256', 'arbitraryOwnedFilesOpened'} and info['arbitraryOwnedFilesOpened'] is False
                        and len(info['selectedEntries']) <= 65536 and info['file']['bytes'] <= TEXT_MAX, 'package-info-schema')
                    for p in info['selectedEntries']:
                        deadline(until)
                        require(set(p) == {'path', 'declaredMD5', 'note'} and p['path'] in ALLOWED
                                and not forbidden_path(p['path']), 'package-info-no-authority-expansion')
                    role_count += 1
                    role_bytes += info['file']['bytes']
                else:
                    require(set(info) == {'suffix', 'absence', 'file', 'selectedEntries'}
                            and info['selectedEntries'] == [] and len(info['absence']) == 2, 'package-info-absence-schema')
        for row, selector in zip(evidence['directories'], DOC['shallowDirectorySelectors']):
            deadline(until)
            wire_record('DirectoryObservation', row, 'absent' if row['resolution']['resolvedPath'] is None else 'present', until)
            require(row['selectorPath'] == selector['path'] and (row['resolution']['resolvedPath'] is None
                    or row['resolution']['resolvedPath'] in ALLOWED), 'directory-selector-binding')
            if row.get('entries') is None and row['resolution']['resolvedPath'] is None:
                require(set(row) == {'selectorPath', 'resolution', 'entries'}, 'directory-absence-schema')
            else:
                require(set(row) == {'selectorPath', 'resolution', 'lstatBefore', 'lstatAfter', 'eligiblePresence', 'totalImmediateCount',
                    'boundedNamesSha256', 'unknownImmediateCount', 'changed', 'recursive'} and row['recursive'] is False
                    and row['changed'] is False and set(row['eligiblePresence']) == set(selector['eligibleNames'])
                    and all(type(v) is bool for v in row['eligiblePresence'].values())
                    and 0 <= row['totalImmediateCount'] <= selector['maximumImmediateEntries'], 'directory-shallow-bounds')
        for row, selector in zip(evidence['historicalText'], DOC['archiveTextSelectors']):
            deadline(until)
            require(set(row) == {'origin', 'file', 'text', 'reusedHistoricalData', 'freshArchiveDownloaded', 'currentRunnerProvenance'}
                    and row['reusedHistoricalData'] is True and row['freshArchiveDownloaded'] is False
                    and row['currentRunnerProvenance'] is False
                    and (row['origin']['archive'], row['origin']['member']) == (selector['archive'], selector['member'])
                    and row['origin']['sha256'] == row['file']['sha256'] == row['text']['textSha256']
                    and row['text']['contentDisposition'] == 'complete-public-text', 'historical-text-schema-and-origin')
            role_count += 1
            role_bytes += row['text']['textBytes']
        require(role_count == COUNTS['inputTextReads'] and role_bytes + COUNTS['statusTextBytes'] == COUNTS['inputTextBytes'],
                'independent-text-budget-accounting')
        require(COUNTS['attemptedInputTextReads'] == COUNTS['inputTextReads']
                and COUNTS['attemptedInputTextBytes'] == COUNTS['inputTextBytes']
                and COUNTS['attemptedStatusBytes'] == COUNTS['statusTextBytes'], 'passed-attempted-input-accounting')


def main():
    global DOC, ALLOWED, CURRENT, _CORROBORATION_PUBLICATION_DEADLINE, OUTPUT_DESCRIPTOR
    OUTPUT_DESCRIPTOR = fresh_output_directory(str(OUTPUT), WORK)
    initial = final = None
    package_records = dir_records = reference_records = None
    primary = None
    terminal = None
    finalize_faults = []
    try:
        require(sys.argv[1:] == [] and sys.flags.isolated == 1 and sys.flags.no_site == 1
                and sys.flags.dont_write_bytecode == 1 and sys.platform == 'linux', 'fixed-isolated-Linux-host')
        require(sys.version_info[:2] == (3, 12), 'Python3.12-host-only')
        initial = source_pins()
        DOC = validate_contract()
        ALLOWED = {r['path'] for r in DOC['selectors']}
        ALLOWED.update(DOC['allowedSymlinkDestinations']['native-alias'])
        ALLOWED.update(DOC['allowedSymlinkDestinations']['os-text'])
        ALLOWED.add(DOC['packageStatus']['path'])
        ALLOWED.update(r['path'] for r in DOC['shallowDirectorySelectors'])
        for package in DOC['packageStatus']['packageNames']:
            ALLOWED.update('/var/lib/dpkg/info/' + package + arch + suffix
                           for arch in ['', ':amd64'] for suffix in ['.list', '.md5sums'])
        for row in DOC['selectors']:
            CURRENT = row['selectorId']
            deadline()
            require(not FAULT_OVERFLOW, 'fault-ledger-overflow')
            try:
                ROWS.append(observation(row))
            except Exception as error:
                failure(CURRENT, error)
                ROWS.append({'selectorId': CURRENT, 'group': row['group'], 'kind': row['kind'], 'file': None,
                             'archivedComparison': 'unobserved-new-input', 'text': None, 'binary': None,
                             'captureFault': {'code': str(error)[:128] if type(error) is Fault else 'read-or-parser-fault',
                                              'exceptionType': type(error).__name__,
                                              'safePartialFacts': error.facts if type(error) is Fault else None}})
        CURRENT = 'package-records'
        package_records = packages()
        CURRENT = 'shallow-directories'
        dir_records = directories()
        CURRENT = 'historical-text'
        reference_records = archive_texts()
    except Exception as error:
        failure(CURRENT, error)
        terminal = {'selectorId': CURRENT, 'code': str(error)[:128] if type(error) is Fault else 'read-or-parser-fault',
                    'exceptionType': type(error).__name__}
        primary = FAULTS[0] if FAULTS else terminal
    finally:
        until = time.monotonic() + 30
        try:
            final = source_pins(until)
            require(initial is not None and initial == final, 'source-final-boundary-changed')
        except Exception as error:
            finalize_faults.append({'code': str(error)[:128] if type(error) is Fault else 'final-source-fault',
                                    'exceptionType': type(error).__name__})
        try:
            recheck_rows(until)
            if dir_records is not None:
                require(directories(until, track=False) == dir_records, 'shallow-final-observation-changed')
        except Exception as error:
            finalize_faults.append({'code': 'final-selected-recheck-incomplete', 'exceptionType': type(error).__name__})
        primary = primary or (FAULTS[0] if FAULTS else None)
        completed = (len(ROWS) == 265 and package_records is not None and dir_records is not None
                     and reference_records is not None and primary is None and not finalize_faults and not FAULT_OVERFLOW)
        interpreter = None
        try:
            interpreter = python_host()
        except Exception as error:
            finalize_faults.append({'code': 'interpreter-observation-fault', 'exceptionType': type(error).__name__})
            completed = False
        summary = {'schemaVersion': 2, 'scope': 'finite-read-only-retention-corroboration-NOT-source-closure',
                   'captureCompleted': completed, 'passed': completed, 'selectorDocumentSha256': SELECTOR_SHA,
                   'sourcePinsBefore': initial, 'sourcePinsAfter': final, 'counts': COUNTS,
                   'exactSelectorCountExpected': 265, 'exactSelectorCountObserved': len(ROWS),
                   'primaryFault': primary, 'terminalWorkFault': terminal,
                   'finalizationFaults': finalize_faults, 'observationFaults': FAULTS,
                   'faultLedgerOverflow': FAULT_OVERFLOW,
                   'archiveReference': DOC['archiveReference'] if DOC else None,
                   'nineOriginalMissingEdgesRemainUnresolved': True, 'fourOriginalSourceContractsRemainPending': True,
                   'completeDynamicLoadClosure': False, 'reviewedRunnerClosure': False, 'fullDiagnosticEnabled': False,
                   'SDKExecuted': False, 'collectorExecuted': False, 'analyzerExecuted': False, 'targetExecuted': False,
                   'nativeProofOrCostParityAccepted': False, 'originalScannerInvokedOrImported': False,
                   'newArchiveDownloaded': False, 'interpreter': interpreter,
                   'historicalDataReused': reference_records is not None, 'changedRunnerFactsAreNewInputs': True,
                   'wireSchemaSha256': WIRE_SCHEMA_SHA,
                   'wireSchemaCaptured': WIRE is not None,
                   'frozenConceptualRequiredNamesEnforcedUnchanged': False,
                   'outputReservationsBeforeSummary': dict(OUTPUT_COUNTS),
                   'stabilityScope': 'bounded descriptor reads and rechecks; not namespace confinement or permanent immutability'}
        evidence = {'schemaVersion': 2, 'scope': summary['scope'], 'observations': ROWS,
                    'packages': package_records, 'directories': dir_records, 'historicalText': reference_records}
        publication = time.monotonic() + 5
        _CORROBORATION_PUBLICATION_DEADLINE = publication
        try:
            validate_evidence(evidence, summary, publication)
            summary['evidence'] = publish('corroboration.json', evidence, publication)
        except Exception as error:
            summary['captureCompleted'] = summary['passed'] = False
            summary['publicationFault'] = {'code': 'evidence-publication-fault', 'exceptionType': type(error).__name__}
        try:
            summary['outputReservationsBeforeSummary'] = dict(OUTPUT_COUNTS)
            if WIRE is not None:
                wire_record('PacketSummary', summary, 'record', publication)
            else:
                require(summary['passed'] is False and summary['wireSchemaCaptured'] is False,
                        'failed-summary-schema-fallback-only')
            publish('summary.json', summary, publication)
        except Exception:
            summary['captureCompleted'] = summary['passed'] = False
            print('NOT GREEN: bounded corroboration publication failed', flush=True)
        print('Finite read-only corroboration: ' + ('CAPTURED ONLY' if summary['passed'] else 'NOT GREEN'), flush=True)
        os.close(OUTPUT_DESCRIPTOR)
        OUTPUT_DESCRIPTOR = None
    return 0


if __name__ == '__main__':
    try:
        main()
    finally:
        if OUTPUT_DESCRIPTOR is not None:
            os.close(OUTPUT_DESCRIPTOR)
            OUTPUT_DESCRIPTOR = None
