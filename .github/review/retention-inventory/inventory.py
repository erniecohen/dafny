"""Fixed read-only prerequisite. Hash and inspect files; never launch inspected code."""
import sys
sys.dont_write_bytecode = True
import hashlib
import json
import os
import re
import stat
import struct
import tarfile
import time
import urllib.request
import zipfile
from pathlib import Path

SOURCE = Path('.github/review/retention-inventory')
OLD = Path('.github/review/alc-controls')
OUTPUT = Path('out/b3-retention-inventory')
OLD_SHA = 'b2e0e676f8c279e25358330ac3a91e740a12ddce319fd9aaedc5f1bd508dbc90'
PACKAGES = [
    ('8.0.31', 'https://builds.dotnet.microsoft.com/dotnet/Runtime/8.0.31/dotnet-runtime-8.0.31-linux-x64.tar.gz',
     31298876, 'e2e392eedd49fd5a6eba078d508383fa9191059cb362c9d93cf533d5207b8db6', 188, 'tar.gz', 'runtime'),
    ('9.0.652701', 'https://api.nuget.org/v3-flatcontainer/dotnet-dump/9.0.652701/dotnet-dump.9.0.652701.nupkg',
     9626393, 'a89bb212881600dde4b8e0c3b39c75957d84f6ec3e1cb7a874d4048e774494c9', 96, 'zip', 'analyzer')
]
MAX_FILES = 65536
MAX_BYTES = 8 * 1024 ** 3
MAX_FILE = 512 * 1024 ** 2
MAX_JSON = 32 * 1024 ** 2
START = time.monotonic()
FINALIZATION_SECONDS = 30
PUBLICATION_SECONDS = 5


def require(value, reason):
    if not value:
        raise RuntimeError(reason)


def deadline(until=None):
    require(time.monotonic() < (START + 600 if until is None else until),
            'Read-only inventory deadline exceeded.' if until is None else 'Bounded finalization/publication deadline exceeded.')


def safe_relative(value):
    require(isinstance(value, str) and len(value) <= 1024 and '\\' not in value and
            not value.startswith('/') and all(p not in ['', '.', '..'] for p in value.split('/')),
            'Safe bounded POSIX relative path required.')
    return value


def capture_image(path, maximum=MAX_FILE, until=None):
    deadline(until)
    path = Path(path)
    before = path.stat()
    require(stat.S_ISREG(before.st_mode) and not path.is_symlink() and before.st_size <= maximum,
            'Bounded regular file required: ' + str(path))
    h = hashlib.sha256()
    size = 0
    prefix = b''
    with path.open('rb') as stream:
        opened = os.fstat(stream.fileno())
        require((opened.st_dev, opened.st_ino, opened.st_size, opened.st_mtime_ns, opened.st_ctime_ns) ==
                (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns),
                'File changed before capture: ' + str(path))
        for block in iter(lambda: stream.read(1048576), b''):
            deadline(until)
            if size == 0:
                prefix = block[:64]
            size += len(block)
            require(size <= maximum, 'File byte bound exceeded.')
            h.update(block)
        after = os.fstat(stream.fileno())
    outside = path.stat()
    identity = lambda p: (p.st_dev, p.st_ino, p.st_size, p.st_mtime_ns, p.st_ctime_ns)
    require(identity(before) == identity(after) == identity(outside) and size == before.st_size,
            'File changed during capture: ' + str(path))
    return {'path': str(path.resolve(strict=True)), 'bytes': size, 'sha256': h.hexdigest()}, prefix


def capture(path, maximum=MAX_FILE, until=None):
    return capture_image(path, maximum, until)[0]


def data(path, maximum=MAX_JSON, until=None):
    path = Path(path)
    pin = capture(path, maximum, until)
    body = path.read_bytes()
    deadline(until)
    require(len(body) == pin['bytes'] and hashlib.sha256(body).hexdigest() == pin['sha256'],
            'Captured metadata changed.')
    return body, pin


def document(path, until=None):
    body, _ = data(path, until=until)
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'Duplicate JSON property.')
            result[key] = value
        return result
    return json.loads(body, object_pairs_hook=pairs)


def publish(path, value, until=None):
    deadline(until)
    path = Path(path)
    body = (json.dumps(value, indent=2, sort_keys=True) + '\n').encode()
    deadline(until)
    require(len(body) <= MAX_JSON and not path.exists() and not path.is_symlink(), 'Fresh bounded receipt required.')
    pending = path.with_name(path.name + '.pending')
    with pending.open('xb') as stream:
        stream.write(body)
        stream.flush()
        os.fsync(stream.fileno())
    os.rename(pending, path)
    fd = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)
    return capture(path, MAX_JSON, until)


def source_pins(until=None):
    manifest = document(SOURCE / 'source-manifest.json', until)
    require(manifest['schemaVersion'] == 1 and manifest['original44Sha256'] == OLD_SHA and
            [p['path'] for p in manifest['repositoryInputs']] == ['global.json'],
            'Fixed original44 source boundary changed.')
    require(len(manifest['files']) == 3 and len({p['path'] for p in manifest['files']}) == 3,
            'Exact three prerequisite sources required.')
    declared = {safe_relative(p['path']) for p in manifest['files']} | {'source-manifest.json'}
    actual = {p.relative_to(SOURCE).as_posix() for p in SOURCE.rglob('*') if p.is_file() or p.is_symlink()}
    require(actual == declared, 'No supplemental prerequisite source file.')
    for pin in manifest['files']:
        current = capture(SOURCE / pin['path'], until=until)
        require(current['bytes'] == pin['bytes'] and current['sha256'] == pin['sha256'], 'Declared source changed.')
    old_body, old_pin = data(OLD / 'source-manifest.json', until=until)
    require(old_pin['sha256'] == OLD_SHA, 'Original44 manifest changed.')
    old = json.loads(old_body)
    require(len(old['files']) == 44 and len({p['path'] for p in old['files']}) == 44, 'Original44 denominator changed.')
    require({p.relative_to(OLD).as_posix() for p in OLD.rglob('*') if p.is_file() or p.is_symlink()} ==
            {safe_relative(p['path']) for p in old['files']} | {'source-manifest.json'}, 'Original44 inventory changed.')
    for pin in old['files']:
        current = capture(OLD / safe_relative(pin['path']), until=until)
        require(current['bytes'] == pin['bytes'] and current['sha256'] == pin['sha256'], 'Original44 file changed.')
    for pin in manifest['repositoryInputs']:
        current = capture(safe_relative(pin['path']), until=until)
        require(current['bytes'] == pin['bytes'] and current['sha256'] == pin['sha256'], 'Repository input changed.')
    outer_path = Path('.github/review/b3-retention-inventory-manifest.json')
    outer = document(outer_path, until)
    require(outer['sourceManifestSha256'] == capture(SOURCE / 'source-manifest.json', until=until)['sha256'] and
            [p['path'] for p in outer['files']] == ['.github/review/retention-inventory/source-manifest.json',
             '.github/review/base', '.github/workflows/review.yml'], 'Exact outer routing inventory required.')
    for pin in outer['files']:
        current = capture(safe_relative(pin['path']), until=until)
        require(current['bytes'] == pin['bytes'] and current['sha256'] == pin['sha256'], 'Outer source changed.')
    return {'sourceManifest': capture(SOURCE / 'source-manifest.json', until=until), 'outerManifest': capture(outer_path, until=until)}


class Redirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, url):
        require(url.startswith(('https://builds.dotnet.microsoft.com/', 'https://api.nuget.org/',
                                'https://globalcdn.nuget.org/')), 'Unexpected official archive redirect.')
        return super().redirect_request(request, fp, code, message, headers, url)


def official_archives():
    manifest = document(SOURCE / 'official-archives.json')
    require(manifest['schemaVersion'] == 1 and len(manifest['packages']) == 2, 'Exact two official archives required.')
    extracted = []
    for package, fixed in zip(manifest['packages'], PACKAGES):
        version, url, size, sha, count, kind, name = fixed
        require((package['version'], package['url'], package['bytes'], package['sha256'], len(package['files']),
                 package['kind'], package['directory']) == fixed, 'Fixed complete archive inventory changed.')
        expected = {safe_relative(p['path']): p for p in package['files']}
        require(len(expected) == count, 'Duplicate archive member inventory.')
        archive = OUTPUT / safe_relative(package['archive'])
        require(not archive.exists(), 'Fresh archive path required.')
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), Redirects())
        with opener.open(url, timeout=30) as response:
            require(response.geturl().startswith(('https://builds.dotnet.microsoft.com/', 'https://api.nuget.org/',
                                                  'https://globalcdn.nuget.org/')), 'Unexpected archive origin.')
            blocks = []
            received = 0
            while True:
                deadline()
                block = response.read(min(65536, size + 1 - received))
                if not block:
                    break
                blocks.append(block)
                received += len(block)
                require(received <= size, 'Archive download byte bound exceeded.')
            payload = b''.join(blocks)
        require(len(payload) == size and hashlib.sha256(payload).hexdigest() == sha, 'Official archive pin mismatch.')
        with archive.open('xb') as stream:
            stream.write(payload)
        require(capture(archive)['sha256'] == sha, 'Downloaded archive changed.')
        folder = OUTPUT / name
        folder.mkdir(mode=0o700)
        observed = set()
        directories = set()

        def member(name, body):
            safe_relative(name)
            require(name in expected and name not in observed and len(body) == expected[name]['bytes'] and
                    hashlib.sha256(body).hexdigest() == expected[name]['sha256'], 'Complete member hash mismatch.')
            observed.add(name)
            target = folder / name
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(body)
            target.chmod(0o644)  # Inspection only. Never give downloaded tools executable permissions.
            pin = capture(target)
            require(pin['bytes'] == expected[name]['bytes'] and pin['sha256'] == expected[name]['sha256'],
                    'Extracted asset changed.')
            extracted.append(pin)

        if kind == 'tar.gz':
            with tarfile.open(archive) as tar:
                for item in tar:
                    deadline()
                    raw = item.name.removeprefix('./').rstrip('/')
                    if raw in ['', '.']:
                        require(item.isdir() and item.name in ['.', './'], 'Only the fixed tar root directory is allowed.')
                        directories.add('.')
                        continue
                    safe_relative(raw)
                    require(not item.name.startswith('/'), 'Absolute archive path.')
                    if item.isdir():
                        require(raw not in directories, 'Duplicate tar directory.')
                        directories.add(raw)
                        continue
                    require(item.isfile() and item.size <= MAX_FILE, 'Only bounded regular tar members permitted.')
                    with tar.extractfile(item) as stream:
                        member(raw, stream.read(item.size + 1))
            require(sorted(directories) == package['directories'], 'Complete tar directory inventory changed.')
        else:
            with zipfile.ZipFile(archive) as archive_zip:
                require(len(archive_zip.infolist()) == count, 'Complete ZIP denominator changed.')
                for item in archive_zip.infolist():
                    deadline()
                    require(not item.is_dir() and item.file_size <= MAX_FILE and item.flag_bits & 1 == 0 and
                            stat.S_IFMT(item.external_attr >> 16) in [0, stat.S_IFREG], 'Only regular unencrypted ZIP members.')
                    member(item.filename, archive_zip.read(item))
        require(observed == set(expected) and
                {p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file() or p.is_symlink()} == set(expected),
                'Complete extracted archive inventory required.')
    return manifest, extracted


def elf_kind(prefix):
    """Detach only header bytes from the same stream used to hash this image."""
    if prefix[:4] != b'\x7fELF':
        return None
    result = {'class': prefix[4] if len(prefix) > 4 else None,
              'byteOrder': prefix[5] if len(prefix) > 5 else None,
              'identVersion': prefix[6] if len(prefix) > 6 else None,
              'prefixBytes': len(prefix), 'prefixSha256': hashlib.sha256(prefix).hexdigest(),
              'completeHeader': False}
    if len(prefix) >= 20 and result['byteOrder'] in [1, 2]:
        endian = '<' if result['byteOrder'] == 1 else '>'
        result.update(elfType=struct.unpack_from(endian + 'H', prefix, 16)[0],
                      machine=struct.unpack_from(endian + 'H', prefix, 18)[0])
        size = 64 if result['class'] == 2 else 52 if result['class'] == 1 else None
        if size and len(prefix) >= size:
            wide = result['class'] == 2
            base = 52 if wide else 40
            result.update(completeHeader=True, headerVersion=struct.unpack_from(endian + 'I', prefix, 20)[0],
                          headerBytes=struct.unpack_from(endian + 'H', prefix, base)[0],
                          programHeaderOffset=struct.unpack_from(endian + ('Q' if wide else 'I'), prefix, 32 if wide else 28)[0],
                          sectionHeaderOffset=struct.unpack_from(endian + ('Q' if wide else 'I'), prefix, 40 if wide else 32)[0],
                          programHeaderEntryBytes=struct.unpack_from(endian + 'H', prefix, base + 2)[0],
                          programHeaderCount=struct.unpack_from(endian + 'H', prefix, base + 4)[0],
                          sectionHeaderEntryBytes=struct.unpack_from(endian + 'H', prefix, base + 6)[0],
                          sectionHeaderCount=struct.unpack_from(endian + 'H', prefix, base + 8)[0],
                          sectionNameIndex=struct.unpack_from(endian + 'H', prefix, base + 10)[0])
    result['typeName'] = {0: 'ET_NONE', 1: 'ET_REL', 2: 'ET_EXEC', 3: 'ET_DYN', 4: 'ET_CORE'}.get(result.get('elfType'), 'unknown-or-reserved')
    return result


class ElfInspectionFailure(RuntimeError):
    def __init__(self, phase, image, header, cause):
        super().__init__(str(cause))
        self.detached = {'phase': phase, 'catalogImage': dict(image), 'capturedHeader': dict(header),
                         'exceptionType': type(cause).__name__, 'failingBound': str(cause)[:4096],
                         'binding': 'exact-catalog-digest-and-header-from-that-hashed-stream'}


def elf(path, pin):
    body, current = data(path, MAX_FILE)
    require(current == pin and len(body) >= 64 and body[:7] == b'\x7fELF\x02\x01\x01', 'Pinned ELF64 x64 input required.')
    require(struct.unpack_from('<H', body, 18)[0] == 62, 'x64 ELF machine required.')
    require(struct.unpack_from('<H', body, 16)[0] in [2, 3], 'ET_EXEC or ET_DYN loadable candidate required.')
    offset = struct.unpack_from('<Q', body, 32)[0]
    entry, count = struct.unpack_from('<HH', body, 54)
    require(entry == 56 and 0 < count <= 1024 and offset + entry * count <= len(body), 'Bounded ELF program headers required.')
    headers = [struct.unpack_from('<IIQQQQQQ', body, offset + entry * i) for i in range(count)]
    dynamic = [h for h in headers if h[0] == 2]
    interpreters = [h for h in headers if h[0] == 3]
    require(len(dynamic) <= 1 and len(interpreters) <= 1, 'Unique dynamic/interpreter ELF segment required.')
    values = []
    if dynamic:
        header = dynamic[0]
        require(header[5] <= 65536 and header[5] % 16 == 0 and header[2] + header[5] <= len(body), 'Bounded ELF dynamic table.')
        terminated = False
        for i in range(header[2], header[2] + header[5], 16):
            tag, value = struct.unpack_from('<qQ', body, i)
            if tag == 0:
                terminated = True
                break
            values.append((tag, value))
        require(terminated, 'ELF dynamic table needs a bounded terminator.')

    def string(index):
        tables = [v for t, v in values if t == 5]
        lengths = [v for t, v in values if t == 10]
        require(len(tables) == len(lengths) == 1 and index < lengths[0], 'Unique bounded dynamic string table.')
        address = tables[0]
        segments = [h for h in headers if h[0] == 1 and h[3] <= address < h[3] + h[5]]
        require(len(segments) == 1, 'ELF string table file mapping required.')
        require(address + lengths[0] <= segments[0][3] + segments[0][5] and
                segments[0][2] + segments[0][5] <= len(body), 'Entire dynamic string table must map into file bytes.')
        start = segments[0][2] + address - segments[0][3] + index
        end_bound = min(len(body), start + lengths[0] - index, start + 4097)
        require(start < end_bound, 'ELF string file bound.')
        end = body.find(b'\0', start, end_bound)
        require(end >= 0, 'ELF string terminator required.')
        return body[start:end].decode('ascii')

    interpreter = None
    if interpreters:
        header = interpreters[0]
        require(0 < header[5] <= 1024 and header[2] + header[5] <= len(body), 'Bounded interpreter path.')
        encoded = body[header[2]:header[2] + header[5]]
        require(encoded[-1:] == b'\0' and b'\0' not in encoded[:-1], 'Exactly terminated interpreter path.')
        interpreter = encoded[:-1].decode('ascii')
        require(interpreter.startswith('/'), 'Absolute ELF interpreter required.')
    result = {'interpreter': interpreter, 'needed': [string(v) for t, v in values if t == 1],
              'rpath': [string(v) for t, v in values if t == 15], 'runpath': [string(v) for t, v in values if t == 29],
              'soname': [string(v) for t, v in values if t == 14]}
    require(len(result['needed']) <= 256 and all(len(result[p]) <= 1 for p in ['rpath', 'runpath', 'soname']),
            'Bounded unique ELF metadata required.')
    return result


def loader_cache():
    body, pin = data('/etc/ld.so.cache', 1048576)
    require(len(body) >= 48 and body[:20] == b'glibc-ld.so.cache1.1', 'Fixed glibc1.1 new-cache format required.')
    count, length = struct.unpack_from('<II', body, 20)
    require(count <= 16384 and 48 + count * 24 + length <= len(body), 'Bounded glibc cache table.')

    def text(offset):
        require(48 + count * 24 <= offset < 48 + count * 24 + length, 'Cache string offset bound.')
        end = body.find(b'\0', offset, min(48 + count * 24 + length, offset + 4097))
        require(end >= 0, 'Cache string terminator required.')
        return body[offset:end].decode('ascii')

    entries = []
    for i in range(count):
        flags, key, value, version, hwcap = struct.unpack_from('<iIIIQ', body, 48 + i * 24)
        entries.append({'name': text(key), 'path': text(value), 'flags': flags, 'osVersion': version, 'hwcap': hwcap})
    return pin, entries


class Catalog:
    def __init__(self):
        self.files = {}
        self.paths = {}
        self.elf_roots = set()
        self.foreign = {}
        self.elf_assets = {}
        self.nonloadable = {}
        self.blockers = []
        self.bytes = 0
        self.unresolved = []
        self.ambiguities = []
        self.trees = {}
        self.patterns = []

    def add(self, path, purpose):
        deadline()
        requested = Path(path).absolute()
        try:
            resolved = requested.resolve(strict=True)
        except FileNotFoundError:
            self.unresolved.append({'owner': purpose, 'requestedPath': str(requested), 'reason': 'missing-file'})
            return None
        if not resolved.is_file():
            self.unresolved.append({'owner': purpose, 'requestedPath': str(requested), 'reason': 'not-regular-file'})
            return None
        # Each selected real file is captured once; all requested alias paths are
        # recorded and re-resolved at the final boundary. No symlink can add code.
        name = str(resolved)
        if name not in self.files:
            pin, prefix = capture_image(resolved)
            self.bytes += pin['bytes']
            require(len(self.files) < MAX_FILES and self.bytes <= MAX_BYTES, 'Catalog bound exceeded.')
            self.files[name] = pin
            classification = elf_kind(prefix)
            if classification:
                self.elf_assets[name] = classification
                if not (classification['completeHeader'] is True and classification['identVersion'] == 1 and
                        classification['headerVersion'] == 1 and classification['headerBytes'] ==
                        (64 if classification['class'] == 2 else 52)):
                    self.blockers.append({'path': name, 'reason': 'incomplete-or-unsupported-ELF-header'})
                elif classification['elfType'] not in [1, 2, 3]:
                    self.blockers.append({'path': name, 'reason': 'ELF-type-' + classification['typeName'] + '-is-an-explicit-blocker'})
                elif classification['elfType'] == 1:
                    self.nonloadable[name] = {**classification, 'reason': 'ELF-ET_REL-relocatable-asset-outside-runtime-load-graph'}
                elif classification['class'] == 2 and classification['byteOrder'] == 1 and classification['machine'] == 62 and '/linux-musl-' not in name:
                    self.elf_roots.add(name)
                else:
                    self.foreign[name] = {**classification, 'reason': 'foreign-architecture-or-musl-platform-asset'}
        self.paths[str(requested)] = {'requestedPath': str(requested), 'resolvedPath': name,
                                     'purpose': purpose, 'finalLinkTarget': os.readlink(requested) if requested.is_symlink() else None}
        return name

    def tree(self, folder, purpose):
        folder = Path(folder)
        if not folder.is_dir():
            self.unresolved.append({'owner': purpose, 'requestedPath': str(folder), 'reason': 'missing-directory'})
            return
        count = 0
        members = []
        for base, directories, files in os.walk(folder, followlinks=False):
            deadline()
            directories.sort()
            files.sort()
            for name in directories:
                item = Path(base) / name
                if item.is_symlink():
                    self.ambiguities.append({'owner': purpose, 'requestedPath': str(item), 'reason': 'directory-link-not-recursively-followed'})
                    members.append(item.relative_to(folder).as_posix() + '/')
            for name in files:
                count += 1
                require(count <= MAX_FILES, 'Tree file bound exceeded.')
                self.add(Path(base) / name, purpose)
                members.append((Path(base) / name).relative_to(folder).as_posix())
        self.trees[str(folder)] = sorted(members)

    def pattern(self, directory, pattern):
        choices = sorted(Path(directory).glob(pattern))
        selection = {'directory': directory, 'pattern': pattern, 'paths': [str(p) for p in choices]}
        self.patterns.append(selection)
        return selection

    def recheck(self, until=None):
        for pin in self.files.values():
            require(capture(pin['path'], until=until) == pin, 'Catalog image changed after inspection.')
        for alias in self.paths.values():
            deadline(until)
            path = Path(alias['requestedPath'])
            require(str(path.resolve(strict=True)) == alias['resolvedPath'] and
                    (os.readlink(path) if path.is_symlink() else None) == alias['finalLinkTarget'], 'Selected path changed.')
        for folder, expected in self.trees.items():
            actual = []
            for base, directories, files in os.walk(folder, followlinks=False):
                deadline(until)
                require(len(actual) + len(files) + len(directories) <= MAX_FILES, 'Rechecked tree bound.')
                actual.extend((Path(base) / n).relative_to(folder).as_posix() for n in files)
                actual.extend((Path(base) / n).relative_to(folder).as_posix() + '/' for n in directories if (Path(base) / n).is_symlink())
            require(sorted(actual) == expected, 'Selected tree inventory changed.')
        for selection in self.patterns:
            deadline(until)
            require([str(p) for p in sorted(Path(selection['directory']).glob(selection['pattern']))] == selection['paths'],
                    'Native lookup candidate set changed.')


def native_graph(catalog):
    if catalog.blockers:
        blocker = sorted(catalog.blockers, key=lambda p: p['path'])[0]
        path = blocker['path']
        cause = RuntimeError(blocker['reason'])
        raise ElfInspectionFailure('native-graph-admission', catalog.files[path], catalog.elf_assets[path], cause) from cause
    cache_pin, cache = loader_cache()
    catalog.add('/etc/ld.so.cache', 'glibc-cache')
    require(catalog.files[cache_pin['path']] == cache_pin, 'Loader cache changed.')
    queue = [(p, ()) for p in sorted(catalog.elf_roots)]
    visited = set()
    metadata = {}
    edges = []
    while queue:
        deadline()
        path, inherited = queue.pop(0)
        context = (path, inherited)
        if context in visited:
            continue
        visited.add(context)
        require(len(visited) <= 8192, 'ELF graph context bound exceeded.')
        try:
            info = elf(path, catalog.files[path])
        except Exception as error:
            raise ElfInspectionFailure('native-graph-ELF-parser', catalog.files[path], catalog.elf_assets.get(path, {}), error) from error
        metadata[path] = info
        if info['interpreter']:
            selected = catalog.add(info['interpreter'], 'ELF-interpreter')
            edges.append({'owner': path, 'needed': 'PT_INTERP', 'selected': selected, 'method': 'exact-interpreter-path'})
            if selected:
                queue.append((selected, ()))
        search = info['runpath'] or info['rpath']
        directories = []
        unsupported = False
        for value in search:
            for part in value.split(':'):
                if not (part.startswith('/') or part.startswith('$ORIGIN')):
                    catalog.ambiguities.append({'owner': path, 'searchPath': part, 'reason': 'relative-or-empty-loader-search'})
                    unsupported = True
                    break
                expanded = part.replace('$ORIGIN', str(Path(path).parent))
                if '$' in expanded:
                    catalog.ambiguities.append({'owner': path, 'searchPath': part, 'reason': 'unsupported-loader-token'})
                    unsupported = True
                    break
                directories.append(Path(expanded))
        if unsupported:
            continue
        if not info['runpath']:
            directories.extend(Path(p) for p in inherited)
        directories = list(dict.fromkeys(directories))
        for name in info['needed']:
            require(0 < len(name) <= 256 and '/' not in name, 'Simple bounded DT_NEEDED name required.')
            local = [d / name for d in directories if (d / name).is_file()]
            candidates = [item for item in cache if item['name'] == name and item['flags'] == 0x303]
            if any(item['hwcap'] != 0 or item['osVersion'] != 0 for item in candidates):
                catalog.ambiguities.append({'owner': path, 'name': name, 'reason': 'unreviewed-cache-hwcap-or-os-version'})
                continue
            cache_paths = [Path(item['path']) for item in candidates if Path(item['path']).is_file()]
            defaults = [Path(d) / name for d in ['/lib/x86_64-linux-gnu', '/usr/lib/x86_64-linux-gnu', '/lib', '/usr/lib']
                        if (Path(d) / name).is_file()]
            choices = local[:1] if local else cache_paths or defaults
            selection_dirs = directories if local else [p.parent for p in choices]
            if any((d / 'glibc-hwcaps').is_dir() and any((d / 'glibc-hwcaps').rglob(name)) for d in selection_dirs):
                catalog.ambiguities.append({'owner': path, 'name': name, 'reason': 'unreviewed-glibc-hwcaps-directory-selection'})
                continue
            selected_paths = {str(p.resolve(strict=True)) for p in choices}
            if len(selected_paths) != 1:
                (catalog.unresolved if not selected_paths else catalog.ambiguities).append(
                    {'owner': path, 'name': name, 'reason': 'missing-native-image' if not selected_paths else 'multiple-native-images',
                     'candidates': sorted(selected_paths)})
                continue
            selected = catalog.add(choices[0], 'DT_NEEDED')
            if selected and selected not in catalog.elf_roots:
                error = RuntimeError('Selected dependency must be a supported ET_EXEC/ET_DYN ELF64 x64 image.')
                raise ElfInspectionFailure('native-graph-dependency-admission', catalog.files[selected],
                                           catalog.elf_assets.get(selected, {}), error) from error
            require(selected is not None, 'Selected dependency disappeared before capture.')
            ancestry = tuple(str(d) for d in directories) if not info['runpath'] else inherited
            queue.append((selected, ancestry))
            edges.append({'owner': path, 'needed': name, 'selected': selected,
                          'method': 'first-declared-origin-path' if local else 'cache-or-default-single-real-image',
                          'inheritedRpath': list(inherited), 'declaredSearchDirectories': [str(d) for d in directories],
                          'eligibleAliasPaths': sorted(str(p) for p in choices)})
    return {'metadata': [{'path': p, **metadata[p]} for p in sorted(metadata)], 'bindings': edges,
            'graphContexts': len(visited), 'cache': cache_pin,
            'resolverAcceptance': False, 'method': 'bounded-ELF64-interpreter-needed-origin-inherited-rpath-glibc-cache-candidate'}


def catalog_snapshot(catalog):
    return {'schemaVersion': 1, 'scope': 'nonqualifying-captured-files-before-native-graph',
            'inventoryCaptured': False, 'nativeGraphCompleted': False, 'reviewedRunnerClosure': False,
            'completeDynamicLoadClosure': False, 'fullDiagnosticEnabled': False,
            'fileCount': len(catalog.files), 'fileBytes': catalog.bytes,
            'files': sorted(catalog.files.values(), key=lambda p: p['path']),
            'pathSelections': sorted(catalog.paths.values(), key=lambda p: p['requestedPath']),
            'selectedTreeMembership': [{'directory': p, 'members': catalog.trees[p]} for p in sorted(catalog.trees)],
            'capturedELFHeaders': [{'path': p, 'image': catalog.files[p], 'header': catalog.elf_assets[p]} for p in sorted(catalog.elf_assets)],
            'relocatableAssetsOutsideLoadGraph': [{'path': p, **catalog.nonloadable[p]} for p in sorted(catalog.nonloadable)],
            'foreignELFAssets': [{'path': p, **catalog.foreign[p]} for p in sorted(catalog.foreign)],
            'nativeRootsBeforeGraph': sorted(catalog.elf_roots), 'unsupportedELFBlockers': list(catalog.blockers),
            'unresolved': list(catalog.unresolved), 'ambiguities': list(catalog.ambiguities)}


def inspect(state):
    state['phase'] = 'fixed-host-prerequisites'
    require(os.name == 'posix' and sys.platform == 'linux' and os.uname().machine == 'x86_64', 'Linux x64 prerequisite only.')
    require(sys.flags.isolated == 1 and sys.flags.no_site == 1 and sys.dont_write_bytecode and
            'site' not in sys.modules and sys.argv == ['-'] and set(os.environ) <= {'PATH', 'LANG', 'LC_ALL'} and
            not any(k.startswith(('LD_', 'DOTNET_', 'CORECLR_', 'COR_', 'GH_')) for k in os.environ),
            'Isolated no-site no-bytecode credential-free fixed Python host required.')
    require(sys.version_info[:2] == (3, 12), 'Fixed Ubuntu Python3.12 inventory scope.')
    before = source_pins()
    state['sourceBoundaryBefore'] = before
    state['phase'] = 'official-archive-inspection'
    archive_manifest, extracted = official_archives()
    catalog = Catalog()
    state['catalog'] = catalog
    state['phase'] = 'runner-file-capture'
    for package in archive_manifest['packages']:
        catalog.add(OUTPUT / package['archive'], 'exact-official-archive')
    for pin in extracted:
        catalog.add(pin['path'], 'official-archive-asset')
    for path in ['/usr/bin/python3', '/usr/share/dotnet/dotnet', '/etc/os-release', '/etc/ld.so.conf',
                 '/etc/ssl/certs/ca-certificates.crt', '/etc/nsswitch.conf', '/etc/hosts', '/etc/resolv.conf']:
        catalog.add(path, 'fixed-runner-selector')
    catalog.tree('/etc/ld.so.conf.d', 'loader-configuration')
    catalog.tree('/usr/lib/python3.12', 'fixed-Python-stdlib')
    catalog.tree('/usr/share/dotnet/host', 'installed-hostfxr-choices')
    catalog.tree('/usr/share/dotnet/shared', 'installed-framework-choices')
    catalog.tree('/usr/share/dotnet/packs', 'installed-reference-pack-choices')
    sdk_root = Path('/usr/share/dotnet/sdk')
    sdk_entries = sorted(p.name for p in sdk_root.iterdir()) if sdk_root.is_dir() else []
    state['SDKSelectionBefore'] = sdk_entries
    sdk_dirs = sorted([p for p in sdk_root.iterdir() if p.is_dir() and re.fullmatch(r'8\.0\.\d+', p.name)],
                      key=lambda p: int(p.name.split('.')[-1])) if sdk_root.is_dir() else []
    other_sdk8 = sorted(p.name for p in sdk_root.iterdir() if p.is_dir() and p.name.startswith('8.0.') and
                        not re.fullmatch(r'8\.0\.\d+', p.name)) if sdk_root.is_dir() else []
    if other_sdk8:
        catalog.ambiguities.append({'owner': 'SDK-selector', 'reason': 'unreviewed-prerelease-selection', 'directories': other_sdk8})
    if sdk_dirs:
        catalog.tree(sdk_dirs[-1], 'highest-installed-stable-8.0-SDK-candidate')
    else:
        catalog.unresolved.append({'owner': 'SDK-selector', 'reason': 'no-stable-installed-8.0-SDK'})
    global_json = document('global.json')
    require(global_json == {'sdk': {'version': '8.0.111', 'rollForward': 'latestFeature'}}, 'Fixed global SDK request changed.')
    # These are explicit *candidate* dynamic families. DT_NEEDED does not prove
    # all dlopen/runtime/tool selections; unresolved source contracts stay typed.
    families = []
    for directory in ['/lib/x86_64-linux-gnu', '/usr/lib/x86_64-linux-gnu']:
        for pattern in ['libicuuc.so.*', 'libicui18n.so.*', 'libicudata.so.*', 'libssl.so.*', 'libcrypto.so.*', 'libnss_*.so.*']:
            selection = catalog.pattern(directory, pattern)
            families.append(selection)
            for path in selection['paths']:
                catalog.add(path, 'explicit-dynamic-lookup-candidate')
    modules = []
    for name, module in sorted(sys.modules.copy().items()):
        filename = getattr(module, '__file__', None)
        if filename and not name == '__main__':
            selected = catalog.add(filename, 'actual-Python-import')
            modules.append({'module': name, 'selectedFile': selected})
    mappings = []
    with Path('/proc/self/maps').open('rb') as stream:
        body = stream.read(8388609)
    require(len(body) <= 8388608, 'Current Python maps bound.')
    for line in body.decode('utf-8').splitlines():
        fields = line.split(maxsplit=5)
        if len(fields) == 6 and fields[5].startswith('/'):
            require(not fields[5].endswith(' (deleted)'), 'No deleted mapped image allowed.')
            selected = catalog.add(fields[5], 'actual-prerequisite-Python-mapping')
            mappings.append({'selectedFile': selected, 'permissions': fields[1]})
    state['phase'] = 'pre-graph-recheck'
    catalog.recheck()
    pre_graph_sources = source_pins()
    require(before == pre_graph_sources, 'Frozen sources changed before native graph.')
    require((sorted(p.name for p in sdk_root.iterdir()) if sdk_root.is_dir() else []) == sdk_entries, 'Installed SDK selection changed before graph.')
    snapshot = catalog_snapshot(catalog)
    snapshot.update(sourceBoundaryBefore=before, sourceBoundaryPreGraph=pre_graph_sources,
                    officialArchives=[{k: p[k] for k in ['url', 'version', 'archive', 'bytes', 'sha256']} for p in archive_manifest['packages']])
    state['phase'] = 'pre-graph-detached-publication'
    state['preGraphCatalog'] = publish(OUTPUT / 'pre-graph-catalog.json', snapshot)
    state['phase'] = 'native-graph'
    graph = native_graph(catalog)
    state['phase'] = 'post-graph-recheck'
    catalog.recheck()
    require((sorted(p.name for p in sdk_root.iterdir()) if sdk_root.is_dir() else []) == sdk_entries, 'Installed SDK selection set changed.')
    after = source_pins()
    require(before == after, 'Frozen sources changed during inventory.')
    return {'schemaVersion': 1, 'scope': 'read-only-retention-runner-inventory-prerequisite',
            'inventoryCaptured': True, 'reviewedRunnerClosure': False, 'fullDiagnosticEnabled': False,
            'completeDynamicLoadClosure': False, 'nativeProofOrCostParityAccepted': False,
            'launchedDownloadedOrSDKImages': [], 'prerequisitePythonHostExecuted': True, 'SDKExecuted': False, 'collectorExecuted': False,
            'analyzerExecuted': False, 'targetExecuted': False, 'sourceBoundaryBefore': before,
            'sourceBoundaryAfter': after, 'officialArchives': [{k: p[k] for k in ['url', 'version', 'archive', 'bytes', 'sha256']}
                                                          for p in archive_manifest['packages']],
            'fileCount': len(catalog.files), 'fileBytes': catalog.bytes,
            'files': sorted(catalog.files.values(), key=lambda p: p['path']),
            'pathSelections': sorted(catalog.paths.values(), key=lambda p: p['requestedPath']),
            'selectedTreeMembership': [{'directory': p, 'members': catalog.trees[p]} for p in sorted(catalog.trees)],
            'capturedELFHeaders': [{'path': p, 'image': catalog.files[p], 'header': catalog.elf_assets[p]} for p in sorted(catalog.elf_assets)],
            'relocatableAssetsOutsideLoadGraph': [{'path': p, **catalog.nonloadable[p]} for p in sorted(catalog.nonloadable)],
            'unsupportedELFBlockers': list(catalog.blockers), 'preGraphCatalog': state['preGraphCatalog'],
            'foreignELFAssets': [{'path': p, **catalog.foreign[p]} for p in sorted(catalog.foreign)],
            'nativeGraph': graph, 'unresolved': catalog.unresolved, 'ambiguities': catalog.ambiguities,
            'elfAvailabilityCandidateComplete': not catalog.unresolved and not catalog.ambiguities,
            'dynamicLookupCandidates': families,
            'requiredSourceContractsBeforeDiagnostic': ['runtime8.0.31-hostfxr-framework-native-ICU-OpenSSL-DAC-lookups',
                                                       'dotnet-dump9.0.652701-runtimeTargets-SOS-DAC-selection',
                                                       'Python3.12-native-extension-loader-selection',
                                                       'selected-SDK-native-tasks-and-compiler-lookup-closure'],
            'SDKSelectionCandidate': {'requested': global_json['sdk'], 'installedStable8Directories': [p.name for p in sdk_dirs],
                                     'candidatePath': str(sdk_dirs[-1]) if sdk_dirs else None, 'SDKResolverExecuted': False},
            'Python': {'version': sys.version, 'executable': str(Path(sys.executable).resolve()),
                       'modules': modules, 'initialMappedFiles': mappings, 'isolated': True, 'siteImported': False},
            'runner': {'machine': os.uname().machine, 'kernelRelease': os.uname().release, 'effectiveUid': os.geteuid()},
            'bounds': {'files': MAX_FILES, 'fileBytes': MAX_FILE, 'totalBytes': MAX_BYTES, 'receiptBytes': MAX_JSON,
                       'seconds': 600, 'ELFGraphContexts': 8192, 'finalizationSeconds': FINALIZATION_SECONDS,
                       'terminalPublicationSeconds': PUBLICATION_SECONDS}}


def detached_error(error, phase):
    if isinstance(error, ElfInspectionFailure):
        return dict(error.detached)
    return {'phase': phase, 'exceptionType': type(error).__name__, 'failingBound': str(error)[:4096]}


def main():
    require(not OUTPUT.exists(), 'Fresh fixed prerequisite evidence directory required.')
    OUTPUT.mkdir(parents=True, mode=0o700)
    receipt = {'schemaVersion': 1, 'scope': 'read-only-retention-runner-inventory-prerequisite',
               'inventoryCaptured': False, 'reviewedRunnerClosure': False, 'fullDiagnosticEnabled': False,
               'completeDynamicLoadClosure': False, 'nativeProofOrCostParityAccepted': False,
               'SDKExecuted': False, 'collectorExecuted': False, 'analyzerExecuted': False, 'targetExecuted': False,
               'primaryFault': None, 'finalizationFaults': [], 'finalSourceRecheckPassed': False,
               'finalCatalogRecheckPassed': None, 'preGraphCatalog': None}
    state = {'phase': 'begin', 'catalog': None, 'preGraphCatalog': None}
    try:
        result = inspect(state)
        state['phase'] = 'completed-candidate-publication'
        pin = publish(OUTPUT / 'candidate-runner-inventory.json', result)
        receipt.update({'inventoryCaptured': True, 'candidateInventory': pin,
                        'fileCount': result['fileCount'], 'fileBytes': result['fileBytes'],
                        'unresolvedCount': len(result['unresolved']), 'ambiguityCount': len(result['ambiguities']),
                        'elfAvailabilityCandidateComplete': result['elfAvailabilityCandidateComplete'],
                        'sourceBoundaryBefore': result['sourceBoundaryBefore'], 'sourceBoundaryAfter': result['sourceBoundaryAfter']})
    except BaseException as error:
        receipt['inventoryCaptured'] = False
        receipt['primaryFault'] = detached_error(error, state['phase'])
        receipt['failure'] = receipt['primaryFault']['exceptionType'] + ': ' + receipt['primaryFault']['failingBound']
    finally:
        # An expired inspection clock never governs failure preservation or final
        # receipts. These checks have one new fixed budget, with no graph retry.
        until = time.monotonic() + FINALIZATION_SECONDS
        receipt['preGraphCatalog'] = state['preGraphCatalog']
        if receipt['primaryFault'] is not None:
            try:
                receipt['primaryFaultReceipt'] = publish(OUTPUT / 'primary-fault.json', receipt['primaryFault'], until)
            except BaseException as error:
                receipt['finalizationFaults'].append(detached_error(error, 'primary-fault-publication'))
        try:
            receipt['finalSourceBoundary'] = source_pins(until)
            require(state.get('sourceBoundaryBefore') in [None, receipt['finalSourceBoundary']], 'Final source boundary differs from initial capture.')
            receipt['finalSourceRecheckPassed'] = True
        except BaseException as error:
            receipt['finalizationFaults'].append(detached_error(error, 'final-declared-source-recheck'))
        if state['catalog'] is not None:
            try:
                state['catalog'].recheck(until)
                if 'SDKSelectionBefore' in state:
                    deadline(until)
                    root = Path('/usr/share/dotnet/sdk')
                    current = sorted(p.name for p in root.iterdir()) if root.is_dir() else []
                    require(current == state['SDKSelectionBefore'], 'Final installed SDK selection differs from capture.')
                receipt['finalCatalogRecheckPassed'] = True
            except BaseException as error:
                receipt['finalCatalogRecheckPassed'] = False
                receipt['finalizationFaults'].append(detached_error(error, 'final-catalog-file-path-tree-recheck'))
        if receipt['finalizationFaults']:
            receipt['inventoryCaptured'] = False
        receipt['finalizationBoundSeconds'] = FINALIZATION_SECONDS
        receipt['terminalPublicationBoundSeconds'] = PUBLICATION_SECONDS
        try:
            publish(OUTPUT / 'summary.json', receipt, time.monotonic() + PUBLICATION_SECONDS)
        except BaseException as error:
            # Preserve primary/finalization facts in the durable fault file when
            # available and in bounded logs; never replace them or retry forever.
            terminal = {'phase': 'terminal-summary-publication', 'fault': detached_error(error, 'terminal-summary-publication'),
                        'primaryFault': receipt['primaryFault'], 'finalizationFaults': receipt['finalizationFaults']}
            print('Inventory receipt publication failed: ' + json.dumps(terminal, sort_keys=True)[:32768])
            print('Read-only retention inventory: NOT GREEN')
        else:
            print('Read-only retention inventory: ' + ('CAPTURED (unreviewed)' if receipt['inventoryCaptured'] else 'NOT GREEN'))
    # A diagnostic prerequisite records its failed/partial status; workflow delivery remains zero.


if __name__ == '__main__':
    main()
