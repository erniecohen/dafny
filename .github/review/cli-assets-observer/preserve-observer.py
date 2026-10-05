"""Fixed isolated pre-load byte preservation; never copy/exclusion admission.

Only sys/posix/time/_sha2 are imported. Python/ABI/import byte prerequisites must
be reviewed and frozen before this source is executed. Nonfatal faults return
zero diagnostically and leave no ready marker. That is not compiler success.
"""
import sys
import posix
import time
import _sha2

MAX_OUTPUT = 8 * 1024 * 1024
started = time.monotonic()
attempted_output = 0


def tick():
    if time.monotonic() - started > 10:
        raise ValueError('bootstrap deadline')


def grammar(path):
    if type(path) is not str or not 1 < len(path) <= 4096 or path[0] != '/' or path[-1] == '/':
        raise ValueError('absolute path grammar')
    if any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/_.-' for c in path):
        raise ValueError('path character grammar')
    parts = path[1:].split('/')
    if len(parts) > 128 or any(not p or len(p) > 255 or p in ('.', '..') for p in parts):
        raise ValueError('path component grammar')
    return parts


def identity(info):
    return (info.st_dev, info.st_ino, info.st_mode, info.st_nlink, info.st_size,
            info.st_mtime_ns, info.st_ctime_ns)


class Route:
    def __init__(self, path, create=False):
        parts = grammar(path)
        self.fds = []
        self.before = []
        self.leaf = parts[-1]
        try:
            self.add(posix.open('/', posix.O_RDONLY | posix.O_DIRECTORY | posix.O_NOFOLLOW | posix.O_CLOEXEC))
            for part in parts[:-1]:
                try:
                    fd = posix.open(part, posix.O_RDONLY | posix.O_DIRECTORY | posix.O_NOFOLLOW | posix.O_CLOEXEC,
                                    dir_fd=self.fds[-1])
                except FileNotFoundError:
                    if not create:
                        raise
                    posix.mkdir(part, 0o700, dir_fd=self.fds[-1])
                    self.before[-1] = identity(posix.fstat(self.fds[-1]))
                    fd = posix.open(part, posix.O_RDONLY | posix.O_DIRECTORY | posix.O_NOFOLLOW | posix.O_CLOEXEC,
                                    dir_fd=self.fds[-1])
                self.add(fd)
        except BaseException:
            self.close()
            raise

    def add(self, fd):
        info = posix.fstat(fd)
        if info.st_mode & 0o170000 != 0o040000:
            posix.close(fd)
            raise ValueError('non-directory ancestor')
        self.fds.append(fd)
        self.before.append(identity(info))

    def stable(self):
        if [identity(posix.fstat(fd)) for fd in self.fds] != self.before:
            raise ValueError('ancestor changed')

    def close(self):
        for fd in reversed(self.fds):
            posix.close(fd)
        self.fds.clear()


class ReadBudget:
    # Requested bytes are charged before each read, including EOF and errors.
    def __init__(self, limit):
        self.limit = limit
        self.requested = 0
        self.consumed = 0

    def request(self, wanted):
        count = min(wanted, self.limit - self.requested)
        if count <= 0 or count > 65536:
            raise ValueError('attempted read budget')
        self.requested += count
        return count

    def received(self, count):
        self.consumed += count
        if not 0 <= self.consumed <= self.requested <= self.limit:
            raise ValueError('read accounting')


def read_file(path, maximum, consume=None, prefix=False, budget=None, exact_bytes=None):
    tick()
    route = Route(path)
    fd = None
    try:
        fd = posix.open(route.leaf, posix.O_RDONLY | posix.O_NOFOLLOW | posix.O_CLOEXEC | posix.O_NONBLOCK,
                        dir_fd=route.fds[-1])
        before = posix.fstat(fd)
        if before.st_mode & 0o170000 != 0o100000 or before.st_size < 0:
            raise ValueError('not regular')
        if before.st_size > maximum and not prefix:
            raise ValueError('file size bound')
        if exact_bytes is not None and before.st_size != exact_bytes:
            raise ValueError('exact pinned length differs before stream')
        target = min(before.st_size, maximum)
        digest = _sha2.sha256()
        count = 0
        while count < target:
            tick()
            wanted = min(65536, target - count)
            requested = budget.request(wanted) if budget is not None else wanted
            block = posix.read(fd, requested)
            if budget is not None:
                budget.received(len(block))
            if not block:
                raise ValueError('short file')
            count += len(block)
            digest.update(block)
            if consume is not None:
                consume(block)
        if not prefix:
            tick()
            requested = budget.request(1) if budget is not None else 1
            probe = posix.read(fd, requested)
            if budget is not None:
                budget.received(len(probe))
            if probe:
                raise ValueError('file grew')
        if identity(before) != identity(posix.fstat(fd)):
            raise ValueError('file changed')
        route.stable()
        return (count, digest.hexdigest(), before.st_size > maximum)
    finally:
        if fd is not None:
            posix.close(fd)
        route.close()


def bytes_file(path, maximum):
    blocks = []
    count, digest, _ = read_file(path, maximum, blocks.append)
    return b''.join(blocks), count, digest


def ascii_lines(path, maximum=65536):
    data, count, digest = bytes_file(path, maximum)
    if not data or not data.endswith(b'\n') or any(c < 32 and c != 10 or c > 126 for c in data):
        raise ValueError('ASCII LF control')
    if data.count(b'\n') > 64:
        raise ValueError('control row cap')
    return data.decode('ascii').splitlines(), count, digest


def write_file(path, parts):
    global attempted_output
    route = Route(path, True)
    fd = None
    digest = _sha2.sha256()
    size = 0
    try:
        fd = posix.open(route.leaf, posix.O_WRONLY | posix.O_CREAT | posix.O_EXCL | posix.O_NOFOLLOW | posix.O_CLOEXEC,
                        0o600, dir_fd=route.fds[-1])
        for part in parts:
            tick()
            attempted_output += len(part)
            if attempted_output > MAX_OUTPUT:
                raise ValueError('first-output aggregate')
            size += len(part)
            digest.update(part)
            offset = 0
            while offset < len(part):
                tick()
                n = posix.write(fd, part[offset:])
                if n <= 0:
                    raise ValueError('write failed')
                offset += n
        return size, digest.hexdigest()
    finally:
        if fd is not None:
            posix.close(fd)
        route.close()


def copy_file(source, destination, maximum, prefix=False, exact_bytes=None):
    global attempted_output
    route = Route(destination, True)
    fd = None
    try:
        fd = posix.open(route.leaf, posix.O_WRONLY | posix.O_CREAT | posix.O_EXCL | posix.O_NOFOLLOW | posix.O_CLOEXEC,
                        0o600, dir_fd=route.fds[-1])
        def consume(block):
            global attempted_output
            attempted_output += len(block)
            if attempted_output > MAX_OUTPUT:
                raise ValueError('first-output aggregate')
            start = 0
            while start < len(block):
                tick()
                n = posix.write(fd, block[start:])
                if n <= 0:
                    raise ValueError('write failed')
                start += n
        return read_file(source, maximum, consume, prefix, exact_bytes=exact_bytes)
    finally:
        if fd is not None:
            posix.close(fd)
        route.close()


def publish(partial, complete):
    source = Route(partial)
    destination = Route(complete)
    try:
        posix.link(source.leaf, destination.leaf, src_dir_fd=source.fds[-1],
                   dst_dir_fd=destination.fds[-1], follow_symlinks=False)
        posix.unlink(source.leaf, dir_fd=source.fds[-1])
    finally:
        source.close()
        destination.close()


def hash_text(value):
    return len(value) == 64 and all(c in '0123456789abcdef' for c in value)


def decimal(value, maximum):
    if not value or len(value) > 11 or not value.isascii() or not value.isdecimal() or len(value) > 1 and value[0] == '0':
        raise ValueError('decimal grammar')
    number = int(value)
    if number > maximum:
        raise ValueError('decimal bound')
    return number


def compiler_exit(value):
    if value == 'unobserved':
        return
    magnitude = value[1:] if value.startswith('-') else value
    n = decimal(magnitude, 2147483648 if value.startswith('-') else 2147483647)
    if value.startswith('-') and n == 0:
        raise ValueError('negative zero')


def python_identity(work, own_path, own_size, own_hash):
    rows, _, _ = ascii_lines(work + '/source/python-pins.txt')
    if not rows or rows[0] != 'B3AssetsObserverPython/1' or len(rows) > 35:
        raise ValueError('Python pin rows')
    executable = rows[1].split('|')
    if len(executable) != 3 or sys.executable != executable[0] or sys.version_info[:2] != (3, 12) or sys.platform != 'linux':
        raise ValueError('Python executable/version')
    size = decimal(executable[1], 512 * 1024 * 1024)
    if read_file(executable[0], size, exact_bytes=size)[:2] != (size, executable[2]):
        raise ValueError('Python executable bytes')
    allowed = {'sys','builtins','_frozen_importlib','_imp','_thread','_warnings','_weakref','_io','marshal','posix',
        '_frozen_importlib_external','time','zipimport','_codecs','codecs','encodings','encodings.aliases','encodings.utf_8',
        'abc','_abc','io','_sha2'}
    if len(sys.modules) > 32:
        raise ValueError('Python module count')
    names = tuple(sorted(sys.modules))
    own = sys.modules.get('__main__')
    if own is None or getattr(own, '__file__', None) != own_path or getattr(own, '__spec__', 'missing') is not None or sys.argv[0] != own_path:
        raise ValueError('owned entry module association')
    if own_path != work + '/source/preserve-observer.py' or read_file(own_path, own_size, exact_bytes=own_size)[:2] != (own_size, own_hash):
        raise ValueError('owned entry source bytes')
    pins = {}
    consumed = 0
    module_budget = ReadBudget(8 * 1024 * 1024)
    for row in rows[2:]:
        fields = row.split('|')
        if len(fields) != 5 or fields[0] not in allowed or fields[0] in pins:
            raise ValueError('module pin shape')
        pins[fields[0]] = fields
    if set(names) != set(pins) | {'__main__'} or not set(pins) <= allowed:
        raise ValueError('unexpected Python module')
    for name in sorted(pins):
        tick()
        _, kind, origin, length, digest = pins[name]
        spec = getattr(sys.modules[name], '__spec__', None)
        actual = getattr(spec, 'origin', None)
        if actual != origin or kind not in ('builtin','frozen','source','extension') or not hash_text(digest):
            raise ValueError('module origin')
        if kind in ('builtin','frozen'):
            if actual != ('built-in' if kind == 'builtin' else 'frozen') or length != '0' or digest != executable[2]:
                raise ValueError('builtin/frozen association')
        else:
            size = decimal(length, 8 * 1024 * 1024)
            consumed += size
            if consumed > 8 * 1024 * 1024 or read_file(origin, size, budget=module_budget, exact_bytes=size)[:2] != (size, digest):
                raise ValueError('module byte pins')
    return names


def main():
    global attempted_output
    output = None
    try:
        if len(sys.argv) != 4 or sys.argv[1] != '--preserve-before-load':
            raise ValueError('fixed invocation')
        control_path, supplied_hash = sys.argv[2:]
        if not hash_text(supplied_hash.lower()):
            raise ValueError('control hash grammar')
        rows, control_size, actual_hash = ascii_lines(control_path)
        if len(rows) != 14 or rows[0] != 'B3AssetsObserverPreload/1' or actual_hash != supplied_hash.lower():
            raise ValueError('control count/hash')
        source, source_hash, image, ref0, hash0, ref1, hash1, arguments, options, output, seal, last, exit_code = rows[1:]
        for path in (source, image, ref0, ref1, arguments, output, control_path):
            grammar(path)
        for digest in (source_hash, hash0, hash1, options, seal):
            if not hash_text(digest):
                raise ValueError('input hash grammar')
        if last not in ('true','false','unobserved'):
            raise ValueError('compiler intrinsic grammar')
        compiler_exit(exit_code)
        if not source.endswith('/source/B3AssetsObserver.cs'):
            raise ValueError('source path')
        work = source[:-len('/source/B3AssetsObserver.cs')]
        if image != work + '/build/B3AssetsObserver.dll' or arguments != work + '/control/compiler-arguments.txt' or control_path != work + '/control/compiler-inputs.txt':
            raise ValueError('fixed own paths')
        bootstrap, bootstrap_size, _ = ascii_lines(work + '/source/bootstrap-pins.txt')
        if len(bootstrap) != 13 or bootstrap[0] != 'B3AssetsObserverBootstrap/1' or bootstrap[1] != seal or bootstrap[3] != options:
            raise ValueError('bootstrap source/options association')
        own_fields = bootstrap[6].split('|')
        if len(own_fields) != 4 or own_fields[0] != work + '/source/preserve-observer.py' or own_fields[1] != 'preserve.py' or not hash_text(own_fields[3]):
            raise ValueError('owned entry fixed source role')
        names = python_identity(work, own_fields[0], decimal(own_fields[2], 262144), own_fields[3])
        owner = bootstrap[2]
        grammar(owner)
        if not owner.endswith('/Source/Dafny/Dafny.csproj'):
            raise ValueError('owner path')
        repository = owner[:-len('/Source/Dafny/Dafny.csproj')]
        if work != repository + '/out/b3-sdk-observer-work' or output != repository + '/out/b3-if-guard-corpus/sdk-observer':
            raise ValueError('separate fixed scratch/evidence roots')
        prepared, prepared_size, _ = ascii_lines(output + '/prepared/inventory.txt')
        prepared_names = ('B3AssetsObserver.cs','B3AssetsObserver.targets','preserve-observer.py','declared-options.txt',
                          'anchors.txt','python-pins.txt','B3AssetsObserver.inputs.props','bootstrap-pins.txt')
        if len(prepared) != len(prepared_names):
            raise ValueError('prepared fixed source count')
        attempted_output = prepared_size
        for name, line in zip(prepared_names, prepared):
            fields = line.split('|')
            if len(fields) != 4 or fields[0] != work + '/source/' + name or fields[1] != output + '/prepared/' + name or not hash_text(fields[3]):
                raise ValueError('prepared fixed source identity')
            maximum = 262144 if name in prepared_names[:3] else 65536
            size = decimal(fields[2], maximum)
            attempted_output += size
            if attempted_output > MAX_OUTPUT or read_file(fields[0], size, exact_bytes=size)[:2] != (size, fields[3]) or read_file(fields[1], size, exact_bytes=size)[:2] != (size, fields[3]):
                raise ValueError('prepared bytes changed or exceed first aggregate')
        control_info = copy_file(control_path, output + '/first/compiler-inputs.txt', 65536, exact_bytes=control_size)
        if control_info[1] != actual_hash:
            raise ValueError('control changed before preservation')
        # Csc output is diagnostic even if present: ExitCode and immediate
        # intrinsic are independently required. Failed/oversized images get
        # only bounded prefix evidence and never a load permission.
        if last != 'true' or exit_code != '0':
            copy_file(image, output + '/failed/compiler-image.prefix', 524288, True)
            raise ValueError('compiler success unobserved or false')
        image_info = copy_file(image, output + '/first/helper.dll.prefix', 524288, True)
        if image_info[2]:
            raise ValueError('compiler image over helper cap; preserved prefix only')
        publish(output + '/first/helper.dll.prefix', output + '/first/helper.dll')
        records = [(image, output + '/first/helper.dll', image_info[0], image_info[1])]
        records.append((control_path, output + '/first/compiler-inputs.txt', control_info[0], actual_hash))
        roles = ('source.cs','targets.xml','preserve.py','anchors.txt','python-pins.txt','declared-options.txt','inputs.props','netstandard.dll','build-framework.dll')
        if len(bootstrap) != 13:
            raise ValueError('exact bootstrap role count')
        for i, row in enumerate(bootstrap[4:]):
            fields = row.split('|')
            if len(fields) != 4:
                raise ValueError('bootstrap fixed file fields')
            live, relative, length, expected = fields
            if relative != roles[i] or not hash_text(expected):
                raise ValueError('bootstrap fixed role')
            maximum = 262144 if relative in ('source.cs','targets.xml','preserve.py') else 65536 if relative.endswith(('.txt','.props')) else 8 * 1024 * 1024
            size = decimal(length, maximum)
            info = copy_file(live, output + '/first/' + relative, size, exact_bytes=size)
            if info[:2] != (size, expected):
                raise ValueError('bootstrap input changed')
            records.append((live, output + '/first/' + relative, size, expected))
        # The bootstrap-pins file cannot contain a self-hash. It is itself
        # retained/bound separately after its finite role table has been read.
        info = copy_file(work + '/source/bootstrap-pins.txt', output + '/first/bootstrap-pins.txt', 65536, exact_bytes=bootstrap_size)
        records.append((work + '/source/bootstrap-pins.txt', output + '/first/bootstrap-pins.txt', info[0], info[1]))
        args_data, args_size, args_hash = bytes_file(arguments, 65536)
        if not args_data.endswith(b'\n') or args_data.count(b'\n') > 256 or any(c < 32 and c != 10 or c > 126 or c in (34,59,64) for c in args_data):
            raise ValueError('compiler argument producer grammar')
        args_lines = args_data.decode('ascii').splitlines()
        if not args_lines or sum(len(line) for line in args_lines) > 65536:
            raise ValueError('compiler argument bound')
        copy_file(arguments, output + '/first/compiler-arguments.txt', 65536, exact_bytes=args_size)
        records.append((arguments, output + '/first/compiler-arguments.txt', args_size, args_hash))
        required = {source:source_hash, ref0:hash0, ref1:hash1}
        for path, digest in required.items():
            if not any(row[0] == path and row[3] == digest for row in records):
                raise ValueError('control-to-preserved-input association')
        for live, copied, size, digest in records:
            if read_file(live, size, exact_bytes=size)[:2] != (size, digest) or read_file(copied, size, exact_bytes=size)[:2] != (size, digest):
                raise ValueError('first input changed after preservation')
        if tuple(sorted(sys.modules)) != names:
            raise ValueError('Python import closure changed')
        inventory = ''.join('|'.join((live, copied, str(size), digest)) + '\n' for live,copied,size,digest in records).encode('ascii')
        if len(inventory) > 65536 or len(records) > 32:
            raise ValueError('first inventory bound')
        write_file(output + '/first/inventory.txt', (inventory,))
        association = ('B3AssetsObserverFirst/1\n' + seal + '\n' + image + '\n' + image_info[1] + '\n' + owner + '\n' + actual_hash + '\n' + work + '/source/B3AssetsObserver.targets\n').encode('ascii')
        write_file(output + '/first/association.txt', (association,))
        write_file(output + '/first/ready', ((image_info[1] + '\n').encode('ascii'),))
    except Exception as error:
        if output is not None:
            try:
                # Type-only bounded failure, no traceback/environment or caller
                # message dump. Partial copied files remain unqualified.
                value = type(error).__name__
                if len(value) > 128 or not value.isascii():
                    value = 'bounded-exception-type'
                write_file(output + '/bootstrap-failure.txt', (('B3AssetsObserverFailure/1\naccepted=false\n' + value + '\n').encode('ascii'),))
            except Exception:
                pass
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
