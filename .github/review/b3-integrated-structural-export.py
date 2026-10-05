"""Read-only bounded upload selection; no structural or remote-upload attestation."""
import codecs
import hashlib
import json
import math
import os
import re
import stat
import time

ORIGINAL = 'b3-integrated-structural'
METADATA = 'b3-integrated-structural-export'
RECEIPT = 'export.json'
MAXIMUM_BYTES = 256 * 1024 * 1024
MAXIMUM_FILES = 40000
MAXIMUM_DIRECTORIES = 40000
MAXIMUM_DEPTH = 32
MAXIMUM_PATH_BYTES = 4096
MAXIMUM_METADATA_BYTES = 16 * 1024 * 1024
MAXIMUM_RECEIPT_BYTES = 65536
MAXIMUM_SUMMARY_BYTES = 16 * 1024 * 1024
MAXIMUM_JSON_DEPTH = 64
MAXIMUM_JSON_TOKENS = 200000
DEADLINE_SECONDS = 60
DIRECTORY_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC
FILE_FLAGS = os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
SCOPE = 'integrated-current-source-structural-export-only'
ORIGINAL_SCOPE = 'integrated-current-source-structural-only'


class Fault(Exception):
    def __init__(self, category, message, **facts):
        super().__init__(message)
        self.record = {'category': category, 'message': message, **facts}


def identity(info, full=True):
    base = (info.st_dev, info.st_ino, info.st_mode)
    return base + (info.st_size, info.st_mtime_ns, info.st_ctime_ns) if full else base


class Budgets:
    def __init__(self, deadline):
        self.deadline = min(deadline, time.monotonic() + DEADLINE_SECONDS)
        self.metadata_attempted = 0
        self.reads = {name: {'maximumContentBytes': cap, 'attemptedContentBytes': 0,
                            'completedContentBytes': 0, 'maximumProbeBytes': probes,
                            'attemptedProbeBytes': 0, 'completedProbeBytes': 0}
                      for name, cap, probes in [('initial', MAXIMUM_BYTES, MAXIMUM_FILES),
                          ('summary', MAXIMUM_SUMMARY_BYTES, 1),
                          ('final', MAXIMUM_BYTES, MAXIMUM_FILES),
                          ('receipt', MAXIMUM_RECEIPT_BYTES, 1)]}

    def tick(self):
        if time.monotonic() >= self.deadline:
            raise Fault('deadline', 'Export metadata deadline expired')

    def metadata(self, amount):
        self.tick()
        self.metadata_attempted += amount
        if self.metadata_attempted > MAXIMUM_METADATA_BYTES:
            raise Fault('metadata-bound', 'Export metadata admission exceeded its bound',
                        attemptedMetadataBytes=self.metadata_attempted)

    def read(self, descriptor, request, kind, probe=False):
        self.tick()
        row = self.reads[kind]
        attempted = 'attemptedProbeBytes' if probe else 'attemptedContentBytes'
        completed = 'completedProbeBytes' if probe else 'completedContentBytes'
        maximum = 'maximumProbeBytes' if probe else 'maximumContentBytes'
        row[attempted] += request
        if request < 1 or request > 65536 or row[attempted] > row[maximum]:
            raise Fault('read-bound', 'Export read request exceeded its fixed allowance', budget=kind)
        chunk = os.read(descriptor, request)
        row[completed] += len(chunk)
        self.tick()
        if probe:
            if chunk:
                raise Fault('growth-probe', 'Export file grew beyond admitted content', budget=kind)
        elif len(chunk) != request:
            raise Fault('short-read', 'Export short content read; no retry', budget=kind)
        return chunk


def encoded_bound(value, depth=0):
    if depth > 16:
        raise Fault('receipt-bound', 'Receipt structure exceeded finite depth')
    if value is None:
        return 4
    if type(value) is bool:
        return 5
    if type(value) is int:
        if abs(value) >= 10 ** 32:
            raise Fault('receipt-bound', 'Receipt integer exceeded finite range')
        return 33
    if type(value) is str:
        # Includes escaped astral scalars; checked before JSON/UTF-8 allocation.
        return 2 + 12 * len(value)
    if type(value) is list:
        return 2 + sum(1 + encoded_bound(item, depth + 1) for item in value)
    if type(value) is dict:
        return 2 + sum(2 + encoded_bound(key, depth + 1) + encoded_bound(item, depth + 1)
                       for key, item in value.items())
    raise Fault('receipt-type', 'Unexpected receipt value type')


def encoded_packet(value):
    if encoded_bound(value) + 1 > MAXIMUM_RECEIPT_BYTES:
        raise Fault('receipt-bound', 'Receipt escaped encoding exceeds 64 KiB')
    data = (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True) + '\n').encode('utf-8')
    if len(data) > MAXIMUM_RECEIPT_BYTES:
        raise Fault('receipt-bound', 'Receipt actual encoding exceeds 64 KiB')
    return data


def inventory_seal(rows):
    digest = hashlib.sha256()
    digest.update(b'[')
    for index, row in enumerate(rows):
        if index:
            digest.update(b',')
        digest.update(json.dumps(row, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode())
    digest.update(b']\n')
    return digest.hexdigest()


def file_content(directory, name, before, budget, kind, capture=False):
    descriptor = os.open(name, FILE_FLAGS, dir_fd=directory)
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode) or identity(opened) != identity(before):
            raise Fault('file-identity', 'File type or identity changed during open')
        if opened.st_size < 0 or opened.st_size > budget.reads[kind]['maximumContentBytes']:
            raise Fault('file-size', 'File exceeds its fixed content allowance', observedBytes=opened.st_size)
        remaining = opened.st_size
        digest = hashlib.sha256()
        chunks = [] if capture else None
        while remaining:
            chunk = budget.read(descriptor, min(65536, remaining), kind)
            digest.update(chunk)
            if capture:
                chunks.append(chunk)
            remaining -= len(chunk)
        budget.read(descriptor, 1, kind, probe=True)
        if identity(os.fstat(descriptor)) != identity(before):
            raise Fault('file-identity', 'File changed across captured content')
        return digest.hexdigest(), b''.join(chunks) if capture else None
    finally:
        os.close(descriptor)


class PacketScan:
    def __init__(self, root, budget, kind, content=True, expected=None):
        self.root, self.budget, self.kind = root, budget, kind
        self.content, self.expected = content, expected
        self.expected_hashes = None
        if expected is not None:
            budget.metadata(128 * len(expected.files))
            self.expected_hashes = {row['path']: row['sha256'] for row in expected.files}
        self.files, self.file_identities, self.directory_identities = [], {}, {}
        self.bytes, self.file_count, self.directory_count = 0, 0, 0
        self.summary_bytes = None

    def run(self):
        self.walk(self.root, '', 0)
        self.files.sort(key=lambda row: row['path'])
        return self

    def walk(self, descriptor, prefix, depth):
        self.budget.tick()
        if depth > MAXIMUM_DEPTH:
            raise Fault('depth', 'Packet directory depth exceeded 32')
        before = os.fstat(descriptor)
        self.directory_count += 1
        if self.directory_count > MAXIMUM_DIRECTORIES or not stat.S_ISDIR(before.st_mode):
            raise Fault('directory-bound', 'Packet directory count or type rejected')
        self.budget.metadata(256 + 12 * len(prefix))
        self.directory_identities[prefix] = identity(before)
        with os.scandir(descriptor) as entries:
            for entry in entries:
                self.budget.tick()
                name = entry.name
                if (not name or name in {'.', '..'} or name.startswith('.') or
                        '/' in name or '\\' in name or any(ord(char) < 32 or ord(char) == 127 for char in name)):
                    raise Fault('path', 'Noncanonical, hidden or control packet name')
                relative = prefix + '/' + name if prefix else name
                if len(relative.encode('utf-8', errors='strict')) > MAXIMUM_PATH_BYTES:
                    raise Fault('path-bound', 'Packet relative path exceeds 4096 UTF-8 bytes')
                info = entry.stat(follow_symlinks=False)
                if stat.S_ISDIR(info.st_mode):
                    child = os.open(name, DIRECTORY_FLAGS, dir_fd=descriptor)
                    try:
                        if identity(os.fstat(child)) != identity(info):
                            raise Fault('directory-identity', 'Directory changed during open')
                        self.walk(child, relative, depth + 1)
                    finally:
                        os.close(child)
                elif stat.S_ISREG(info.st_mode):
                    self.file_count += 1
                    self.bytes += info.st_size
                    if self.file_count + 1 > MAXIMUM_FILES or info.st_size < 0 or self.bytes > MAXIMUM_BYTES:
                        raise Fault('payload-bound', 'Original packet leaves no bounded metadata-file admission',
                                    observedFileCount=self.file_count, observedByteLowerBound=self.bytes)
                    self.budget.metadata(1024 + 12 * len(relative))
                    if relative in self.file_identities or relative in self.directory_identities:
                        raise Fault('duplicate-path', 'Canonical packet path duplicated or changes type')
                    if self.content:
                        capture_summary = self.kind == 'initial' and relative == 'summary.json' and info.st_size <= MAXIMUM_SUMMARY_BYTES
                        digest, captured = file_content(descriptor, name, info, self.budget, self.kind, capture=capture_summary)
                        if capture_summary:
                            self.summary_bytes = {'raw': captured, 'sha256': digest, 'identity': identity(info)}
                    else:
                        prior = self.expected.file_identities.get(relative) if self.expected else None
                        if prior != identity(info):
                            raise Fault('file-identity', 'Final packet identity differs')
                        check = os.open(name, FILE_FLAGS, dir_fd=descriptor)
                        try:
                            if identity(os.fstat(check)) != identity(info):
                                raise Fault('file-identity', 'Final regular file changed during open')
                        finally:
                            os.close(check)
                        digest = self.expected_hashes[relative]
                    self.file_identities[relative] = identity(info)
                    self.files.append({'path': relative, 'mode': format(stat.S_IMODE(info.st_mode), '04o'),
                                       'bytes': info.st_size, 'sha256': digest})
                else:
                    raise Fault('entry-type', 'Packet contains symlink or special entry')
        if identity(os.fstat(descriptor)) != identity(before):
            raise Fault('directory-identity', 'Directory changed during enumeration')

    def same_as(self, other):
        return (self.files == other.files and self.file_identities == other.file_identities
                and self.directory_identities == other.directory_identities)


class Summary:
    """Finite JSON recognition and fixed seven root fields; no whole-object load."""
    SELECTED = {'scope', 'head', 'passed', 'cleanupPoisoned', 'primaryFailure', 'finalError', 'artifactFailure'}

    def __init__(self, raw, budget):
        self.raw, self.budget, self.index, self.tokens = raw, budget, 0, 0
        decoder = codecs.getincrementaldecoder('utf-8')('strict')
        for offset in range(0, len(raw), 65536):
            budget.tick()
            decoder.decode(raw[offset:offset + 65536], final=False)
        decoder.decode(b'', final=True)
        if raw.startswith(b'\xef\xbb\xbf'):
            self.index = 3

    def tick(self):
        # One lexical string/scalar or punctuation token; whitespace is excluded.
        # A selected subtree reparse is conservatively charged again.
        self.budget.tick()
        self.tokens += 1
        if self.tokens > MAXIMUM_JSON_TOKENS:
            raise Fault('summary-tokens', 'Summary exceeds 200000 JSON tokens')

    def whitespace(self):
        while self.index < len(self.raw) and self.raw[self.index] in b' \r\n\t':
            self.index += 1
            if self.index % 1024 == 0:
                self.budget.tick()

    def string_end(self):
        assert self.raw[self.index] == 34
        self.index += 1
        while self.index < len(self.raw):
            char = self.raw[self.index]
            self.index += 1
            if char == 34:
                return
            if char < 32:
                raise Fault('summary-json', 'JSON string contains raw control')
            if char == 92:
                if self.index >= len(self.raw):
                    break
                escaped = self.raw[self.index]
                self.index += 1
                if escaped == 117:
                    digits = self.raw[self.index:self.index + 4]
                    if len(digits) != 4 or any(c not in b'0123456789abcdefABCDEF' for c in digits):
                        raise Fault('summary-json', 'Malformed JSON Unicode escape')
                    self.index += 4
                elif escaped not in b'"\\/bfnrt':
                    raise Fault('summary-json', 'Malformed JSON escape')
            if self.index % 1024 == 0:
                self.budget.tick()
        raise Fault('summary-json', 'Unterminated JSON string')

    def string_value(self, start, end, limit):
        if self.raw[start] != 34 or self.raw[end - 1] != 34:
            raise Fault('summary-type', 'Expected fixed selected string')
        position, count, prefix = start + 1, 0, []
        escapes = {34: '"', 92: '\\', 47: '/', 98: '\b', 102: '\f', 110: '\n', 114: '\r', 116: '\t'}
        while position < end - 1:
            byte = self.raw[position]
            if byte == 92:
                code = self.raw[position + 1]
                if code == 117:
                    scalar = int(self.raw[position + 2:position + 6], 16)
                    position += 6
                    if 0xD800 <= scalar <= 0xDBFF:
                        if self.raw[position:position + 2] != b'\\u':
                            raise Fault('summary-unicode', 'Unpaired selected Unicode surrogate')
                        low = int(self.raw[position + 2:position + 6], 16)
                        if not 0xDC00 <= low <= 0xDFFF:
                            raise Fault('summary-unicode', 'Unpaired selected Unicode surrogate')
                        scalar = 0x10000 + ((scalar - 0xD800) << 10) + low - 0xDC00
                        position += 6
                    elif 0xDC00 <= scalar <= 0xDFFF:
                        raise Fault('summary-unicode', 'Unpaired selected Unicode surrogate')
                    char = chr(scalar)
                else:
                    char = escapes[code]
                    position += 2
            else:
                width = 1 if byte < 128 else 2 if byte < 224 else 3 if byte < 240 else 4
                char = self.raw[position:position + width].decode('utf-8', errors='strict')
                position += width
            if count < limit:
                prefix.append(char)
            count += 1
            if count % 1024 == 0:
                self.budget.tick()
        return {'prefix': ''.join(prefix), 'truncated': count > limit,
                'rawTokenSha256': hashlib.sha256(memoryview(self.raw)[start:end]).hexdigest()}

    def value(self, depth=0, selected=None, strict=False):
        self.tick()
        if depth > MAXIMUM_JSON_DEPTH:
            raise Fault('summary-depth', 'Summary JSON depth exceeds 64')
        self.whitespace()
        start = self.index
        if start >= len(self.raw):
            raise Fault('summary-json', 'Missing JSON value')
        char = self.raw[start]
        if char == 34:
            self.string_end()
        elif char in (123, 91):
            is_object = char == 123
            self.index += 1
            self.whitespace()
            closing = 125 if is_object else 93
            if self.index < len(self.raw) and self.raw[self.index] == closing:
                self.tick()
                self.index += 1
                return start, self.index
            while True:
                key = None
                if is_object:
                    self.tick()
                    self.whitespace()
                    key_start = self.index
                    if key_start >= len(self.raw) or self.raw[key_start] != 34:
                        raise Fault('summary-json', 'Object key is not a JSON string')
                    self.string_end()
                    if selected is not None:
                        decoded = self.string_value(key_start, self.index, 64)
                        key = decoded['prefix'] if not decoded['truncated'] else None
                        if strict and key not in selected:
                            raise Fault('summary-shape', 'Unexpected primary failure key')
                    self.whitespace()
                    if self.index >= len(self.raw) or self.raw[self.index] != 58:
                        raise Fault('summary-json', 'Missing JSON object colon')
                    self.tick()
                    self.index += 1
                span = self.value(depth + 1)
                if selected is not None and key in selected:
                    if selected[key] is not None:
                        raise Fault('summary-duplicate', 'Duplicate selected JSON key')
                    selected[key] = span
                self.whitespace()
                if self.index >= len(self.raw):
                    raise Fault('summary-json', 'Unterminated JSON container')
                if self.raw[self.index] == closing:
                    self.tick()
                    self.index += 1
                    break
                if self.raw[self.index] != 44:
                    raise Fault('summary-json', 'Missing JSON container comma')
                self.tick()
                self.index += 1
        else:
            match = re.match(rb'(?:null|true|false|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)', memoryview(self.raw)[start:])
            if match is None:
                raise Fault('summary-json', 'Unknown JSON primitive')
            self.index += match.end()
        return start, self.index

    def primitive(self, span, kind, limit=0):
        start, end = span
        if end - start == 4 and self.raw[start:end] == b'null':
            return None
        if kind == 'string':
            return self.string_value(start, end, limit)
        if end - start > 32:
            raise Fault('summary-type', 'Selected scalar exceeds fixed type bound')
        token = self.raw[start:end]
        if kind == 'bool' and token in (b'true', b'false'):
            return token == b'true'
        if kind == 'int' and re.fullmatch(rb'-?(?:0|[1-9][0-9]*)', token):
            return int(token)
        raise Fault('summary-type', 'Selected scalar has wrong type')

    def extract(self, head):
        self.whitespace()
        if self.index >= len(self.raw) or self.raw[self.index] != 123:
            raise Fault('summary-type', 'Summary root must be object')
        fields = {key: None for key in self.SELECTED}
        self.value(selected=fields)
        self.whitespace()
        if self.index != len(self.raw):
            raise Fault('summary-json', 'Trailing JSON tokens')
        for key in self.SELECTED - {'artifactFailure'}:
            if fields[key] is None:
                raise Fault('summary-shape', 'Required selected summary key absent')
        scope = self.primitive(fields['scope'], 'string', 64)
        observed_head = self.primitive(fields['head'], 'string', 40)
        if scope is None or scope['truncated'] or scope['prefix'] != ORIGINAL_SCOPE:
            raise Fault('summary-association', 'Summary scope does not match')
        if observed_head is not None and (observed_head['truncated'] or observed_head['prefix'] != head):
            raise Fault('summary-association', 'Summary head does not match workflow metadata')
        passed = self.primitive(fields['passed'], 'bool')
        poisoned = self.primitive(fields['cleanupPoisoned'], 'bool')
        if passed is None or poisoned is None:
            raise Fault('summary-type', 'Source Bool field cannot be null')
        start, end = fields['primaryFailure']
        primary = None
        if end - start != 4 or self.raw[start:end] != b'null':
            if self.raw[start] != 123:
                raise Fault('summary-type', 'Primary failure must be object or null')
            self.index = start
            selected = {key: None for key in ['stage', 'actualProcessExitCode', 'failure', 'boundaryFailure']}
            self.value(selected=selected, strict=True)
            if self.index != end or any(span is None for span in selected.values()):
                raise Fault('summary-shape', 'Incomplete primary failure object')
            primary = {key: self.primitive(span, 'int' if key == 'actualProcessExitCode' else 'string',
                                          128 if key == 'stage' else 512) for key, span in selected.items()}
            if primary['stage'] is None:
                raise Fault('summary-type', 'Source failure stage cannot be null')
        artifact = {'status': 'absent'}
        if fields['artifactFailure'] is not None:
            token = self.primitive(fields['artifactFailure'], 'string', 1024)
            if token is None:
                raise Fault('summary-type', 'Source artifactFailure is absent or string, never null')
            artifact = {'status': 'present', 'value': token}
        return {'status': 'extracted-source-associated', 'sourceValidatedHeadClaim': False,
                'originalHead': observed_head, 'originalSummaryPassed': passed,
                'originalCleanupPoisoned': poisoned, 'originalPrimaryFailure': primary,
                'originalFinalError': self.primitive(fields['finalError'], 'string', 2048),
                'originalArtifactFailure': artifact}


def summary_capture(original, budget, head, scan=None, complete=False):
    record = {'status': 'unavailable', 'captured': False, 'interpretation': None}
    try:
        cached = scan.summary_bytes if scan is not None else None
        if cached is not None:
            raw, digest = cached['raw'], cached['sha256']
            record['captureBudget'] = 'initial'
        elif complete:
            row = next((row for row in scan.files if row['path'] == 'summary.json'), None) if scan is not None else None
            if row is None:
                raise Fault('summary-absent', 'Complete initial scan had no fixed summary file')
            if row['bytes'] > MAXIMUM_SUMMARY_BYTES:
                raise Fault('summary-oversized', 'Regular summary exceeds interpretation allowance',
                            observedBytes=row['bytes'], maximumBytes=MAXIMUM_SUMMARY_BYTES)
            raise Fault('summary-uncaptured', 'Complete initial scan lacks the expected bounded summary buffer')
        else:
            try:
                before = os.stat('summary.json', dir_fd=original, follow_symlinks=False)
            except FileNotFoundError:
                raise Fault('summary-absent', 'Fixed summary is absent after early initial failure')
            if not stat.S_ISREG(before.st_mode):
                raise Fault('summary-nonregular', 'Fixed summary is not a regular file',
                            observedType=stat.S_IFMT(before.st_mode))
            if before.st_size < 0 or before.st_size > MAXIMUM_SUMMARY_BYTES:
                raise Fault('summary-oversized', 'Regular summary exceeds interpretation allowance',
                            observedBytes=before.st_size, maximumBytes=MAXIMUM_SUMMARY_BYTES)
            digest, raw = file_content(original, 'summary.json', before, budget, 'summary', capture=True)
            record['captureBudget'] = 'separate-after-early-initial-failure'
        record.update({'captured': True, 'bytes': len(raw), 'sha256': digest})
        record['interpretation'] = Summary(raw, budget).extract(head)
        record['status'] = 'captured-and-extracted'
    except Exception as error:
        record['interpretation'] = {'status': 'unavailable', 'reason': error.record if isinstance(error, Fault)
                                    else {'category': 'summary-unavailable', 'errorType': type(error).__name__[:128]}}
    return record


def validate_source_pins(value):
    if type(value) is not dict or set(value) != {'parent', 'product', 'coordinator', 'inputs', 'helper'}:
        raise Fault('source-pins', 'Source pin fields do not match finite interface')
    for key in ['parent', 'product']:
        if type(value[key]) is not str or re.fullmatch('[0-9a-f]{40}', value[key]) is None:
            raise Fault('source-pins', 'Expected source revision has wrong shape')
    for key in ['coordinator', 'inputs', 'helper']:
        row = value[key]
        if (type(row) is not dict or set(row) != {'bytes', 'sha256'} or type(row['bytes']) is not int
                or not 0 < row['bytes'] <= 1024 * 1024 or type(row['sha256']) is not str
                or re.fullmatch('[0-9a-f]{64}', row['sha256']) is None):
            raise Fault('source-pins', 'Expected source record has wrong shape')
    return value


def packet_record(pins, head, budget, scan, summary, fault):
    interpretation = summary.get('interpretation') or {'status': 'unavailable'}
    source_associated = interpretation.get('status') == 'extracted-source-associated'
    evidence = {key: value for key, value in summary.items() if key != 'interpretation'}
    evidence['interpretationStatus'] = interpretation.get('status', 'unavailable')
    if not source_associated:
        evidence['reason'] = interpretation.get('reason', 'not captured')
    def original(key):
        return interpretation[key] if source_associated else {'status': 'unavailable'}
    return {'schemaVersion': 1, 'scope': SCOPE, 'exportAdmitted': fault is None,
            'fullPacketSelectedForUpload': fault is None, 'structuralAcceptance': False,
            'backendAccepted': False, 'completeLibraryVerified': False, 'libraryBinaryProduced': False,
            'workerRuntimeVerified': False, 'cliVerified': False, 'corpusVerified': False,
            'regressionRuntimeVerified': False, 'defaultCompatibilityVerified': False,
            'head': head, 'headEvidence': 'workflow-dispatch-metadata-only', 'sourcePins': pins,
            'exportFault': fault, 'originalSummary': evidence,
            'originalSummaryPassed': original('originalSummaryPassed'),
            'originalPrimaryFailure': original('originalPrimaryFailure'),
            'originalFinalError': original('originalFinalError'),
            'originalArtifactFailure': original('originalArtifactFailure'),
            'originalInventory': {'complete': scan is not None, 'fileCount': scan.file_count if scan else None,
                'directoryCount': scan.directory_count if scan else None, 'rawBytes': scan.bytes if scan else None,
                'compactSortedJsonLfSealSha256': inventory_seal(scan.files) if scan else None},
            'observedReadBudgetsBeforeReceipt': budget.reads,
            'attemptedMetadataBytesBeforeReceipt': budget.metadata_attempted,
            'requiredAfterReceiptChecks': ['own regular receipt exact bytes/hash', 'original full identity inventory',
                                           'anchored out ancestor device/inode/type'],
            'maximumCombinedRawBytes': MAXIMUM_BYTES, 'maximumCombinedRegularFiles': MAXIMUM_FILES,
            'maximumMetadataPacketBytes': MAXIMUM_RECEIPT_BYTES,
            'deadlineSeconds': DEADLINE_SECONDS,
            'timingScope': 'Selection before upload; no uploader/service acknowledgement',
            'provenanceScope': 'Filesystem observations with fixed no-unrelated-writer premise; no atomic execution/upload attestation'}


def write_receipt(directory, data, budget):
    flags = os.O_WRONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC
    try:
        before = os.stat(RECEIPT, dir_fd=directory, follow_symlinks=False)
    except FileNotFoundError:
        descriptor = os.open(RECEIPT, flags | os.O_CREAT | os.O_EXCL, 0o600, dir_fd=directory)
    else:
        if not stat.S_ISREG(before.st_mode) or before.st_size > MAXIMUM_RECEIPT_BYTES:
            raise Fault('receipt-type', 'Existing own receipt is not regular bounded bytes')
        descriptor = os.open(RECEIPT, flags, dir_fd=directory)
        if identity(os.fstat(descriptor)) != identity(before):
            os.close(descriptor)
            raise Fault('receipt-identity', 'Own receipt changed during open')
    try:
        budget.tick()
        os.ftruncate(descriptor, 0)
        if os.write(descriptor, data) != len(data):
            raise Fault('receipt-write', 'Own receipt short write; no retry')
        before = os.fstat(descriptor)
        if before.st_size != len(data):
            raise Fault('receipt-write', 'Own receipt length changed')
    finally:
        os.close(descriptor)
    digest, captured = file_content(directory, RECEIPT, before, budget, 'receipt', capture=True)
    if captured != data or digest != hashlib.sha256(data).hexdigest():
        raise Fault('receipt-identity', 'Own receipt bytes do not match owned encoding')
    return identity(before)


def check_own_namespace(base, out, metadata, base_identity, out_identity,
                        metadata_identity, receipt_identity, budget):
    budget.tick()
    if (identity(os.fstat(base), full=False) != base_identity or identity(os.fstat(out), full=False) != out_identity
            or identity(os.fstat(metadata), full=False) != metadata_identity):
        raise Fault('ancestor-identity', 'Anchored ancestor device/inode/type changed')
    if identity(os.stat('out', dir_fd=base, follow_symlinks=False), full=False) != out_identity:
        raise Fault('ancestor-identity', 'Out directory entry changed')
    if identity(os.stat(METADATA, dir_fd=out, follow_symlinks=False), full=False) != metadata_identity:
        raise Fault('ancestor-identity', 'Own metadata directory entry changed')
    count = 0
    with os.scandir(metadata) as entries:
        for entry in entries:
            budget.tick()
            count += 1
            if count != 1 or entry.name != RECEIPT:
                raise Fault('metadata-namespace', 'Own export directory has unexpected entries')
    if count != 1:
        raise Fault('metadata-namespace', 'Own export directory is incomplete')
    if identity(os.stat(RECEIPT, dir_fd=metadata, follow_symlinks=False)) != receipt_identity:
        raise Fault('receipt-identity', 'Own receipt type/identity/size changed after final recheck')
    budget.tick()


def run_export(source_pins, head, deadline):
    """Return only selection/readiness facts; caller's literals establish source pins."""
    if type(deadline) is not float or not math.isfinite(deadline):
        raise Fault('deadline', 'Expected finite shared absolute metadata deadline')
    budget = Budgets(deadline)
    pins = validate_source_pins(source_pins)
    if type(head) is not str or re.fullmatch('[0-9a-f]{40}', head) is None:
        raise Fault('head', 'Workflow head must be 40 lowercase hex digits')
    base = out = original = metadata = None
    complete = partial = None
    summary = {'status': 'unavailable', 'captured': False, 'interpretation': None}
    try:
        base = os.open('.', DIRECTORY_FLAGS)
        base_identity = identity(os.fstat(base), full=False)
        try:
            os.mkdir('out', 0o700, dir_fd=base)
        except FileExistsError:
            pass
        out = os.open('out', DIRECTORY_FLAGS, dir_fd=base)
        out_identity = identity(os.fstat(out), full=False)
        os.mkdir(METADATA, 0o700, dir_fd=out)
        metadata = os.open(METADATA, DIRECTORY_FLAGS, dir_fd=out)
        metadata_identity = identity(os.fstat(metadata), full=False)
        fault = None
        try:
            original = os.open(ORIGINAL, DIRECTORY_FLAGS, dir_fd=out)
            partial = PacketScan(original, budget, 'initial')
            complete = partial.run()
            summary = summary_capture(original, budget, head, partial, complete=True)
            final = PacketScan(original, budget, 'final').run()
            if not final.same_as(complete):
                raise Fault('final-inventory', 'Original packet content/identity inventory changed')
        except Exception as error:
            fault = error.record if isinstance(error, Fault) else {'category': 'admission', 'errorType': type(error).__name__[:128]}
            if original is not None and not summary['captured']:
                summary = summary_capture(original, budget, head, partial, complete=complete is not None)
        record = packet_record(pins, head, budget, complete if fault is None else None, summary, fault)
        if fault is not None and partial is not None:
            record['originalInventory'].update({'observedFileCountLowerBound': partial.file_count,
                'observedDirectoryCountLowerBound': partial.directory_count, 'observedRawByteLowerBound': partial.bytes})
        try:
            data = encoded_packet(record)
        except Fault:
            record['originalSummary'] = {'captured': summary['captured'], 'bytes': summary.get('bytes'),
                'sha256': summary.get('sha256'), 'interpretationStatus': 'unavailable', 'reason': 'receipt encoding bound'}
            for key in ['originalSummaryPassed', 'originalPrimaryFailure', 'originalFinalError', 'originalArtifactFailure']:
                record[key] = {'status': 'unavailable', 'reason': 'receipt encoding bound'}
            data = encoded_packet(record)
        if fault is None and (complete.bytes + len(data) > MAXIMUM_BYTES or complete.file_count + 1 > MAXIMUM_FILES):
            fault = {'category': 'combined-bound', 'message': 'Original plus actual metadata encoding exceeds combined bound',
                     'observedOriginalBytes': complete.bytes, 'metadataEncodedBytes': len(data)}
            record['exportFault'], record['exportAdmitted'], record['fullPacketSelectedForUpload'] = fault, False, False
            data = encoded_packet(record)
        try:
            receipt_identity = write_receipt(metadata, data, budget)
            if fault is None:
                identities = PacketScan(original, budget, 'final', content=False, expected=complete).run()
                if not identities.same_as(complete):
                    raise Fault('final-inventory', 'Original changed after own metadata preservation')
            if fault is None and identity(os.stat(ORIGINAL, dir_fd=out, follow_symlinks=False)) != complete.directory_identities['']:
                raise Fault('ancestor-identity', 'Original directory entry changed')
            check_own_namespace(base, out, metadata, base_identity, out_identity,
                                metadata_identity, receipt_identity, budget)
            return {'full_packet': fault is None, 'metadata_ready': True}
        except Exception as error:
            final_fault = error.record if isinstance(error, Fault) else {'category': 'final-preservation', 'errorType': type(error).__name__[:128]}
            record['exportAdmitted'], record['fullPacketSelectedForUpload'] = False, False
            record['exportFault'] = {'category': 'final-preservation', 'fault': final_fault, 'priorExportFault': fault}
            # A bounded rejection keeps the original error fields, never rewrites original files.
            # Every read attempt remains charged; exhaustion cannot mint a ready packet.
            try:
                rejected = encoded_packet(record)
                rejected_identity = write_receipt(metadata, rejected, budget)
                check_own_namespace(base, out, metadata, base_identity, out_identity,
                                    metadata_identity, rejected_identity, budget)
                return {'full_packet': False, 'metadata_ready': True}
            except Exception:
                return {'full_packet': False, 'metadata_ready': False}
    finally:
        for descriptor in [metadata, original, out, base]:
            if descriptor is not None:
                os.close(descriptor)
