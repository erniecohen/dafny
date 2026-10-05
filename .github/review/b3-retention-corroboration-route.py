"""Fixed finite read-only routing. Invalid flags never invoke the packet."""
import sys
sys.dont_write_bytecode = True
import hashlib
import json
import os
import stat
from pathlib import Path

SOURCE = Path('.github/review/retention-corroboration')
OUTPUT = Path('out/b3-retention-corroboration-routing')
SOURCE_SHA = '3f4530a045f4813f27ef1e03695fcfdd9a8308cdc197d39646908c21f19ef35d'
PACKET_SHA = 'ccb483eac6a1c76b8d0f20cd4cacafb065e608620d6ad9c907df4b2c710ab8de'
PACKET_BYTES = 69914
EXPECTED = {'B3_FOCUS_GATE': 'retention-corroboration', 'B3_COMPILE_ONLY': 'true',
            'B3_FULL_GATE': 'false', 'B3_BINARIES': 'false',
            'B3_RESOLVER_PROBE': 'false', 'B3_ADDITIONAL_AXIOMS_PROBE': 'false'}


def read(path, maximum=32 * 1024 ** 2):
    path = Path(path)
    if any(p.is_symlink() for p in [path.absolute(), *path.absolute().parents]):
        raise ValueError('Source paths cannot follow aliases.')
    before = path.lstat()
    if not stat.S_ISREG(before.st_mode) or before.st_size > maximum:
        raise ValueError('Require a bounded regular source file.')
    with path.open('rb') as stream:
        body = stream.read(maximum + 1)
        after = os.fstat(stream.fileno())
    identity = lambda s: (s.st_dev, s.st_ino, s.st_size, s.st_mtime_ns, s.st_ctime_ns)
    if len(body) != before.st_size or len(body) > maximum or identity(before) != identity(after) or identity(after) != identity(path.lstat()):
        raise ValueError('Source changed during routing capture.')
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


def main():
    if OUTPUT.exists() or OUTPUT.is_symlink() or any(p.is_symlink() for p in OUTPUT.absolute().parents):
        print('NOT GREEN: fresh routing output required')
        return 0
    OUTPUT.mkdir(parents=True)
    route = {'schemaVersion': 1, 'scope': 'finite-read-only-retention-corroboration-routing', 'passed': False,
             'flagsAccepted': False, 'packetInvoked': False, 'sourcePinsAccepted': False,
             'SDKExecuted': False, 'collectorExecuted': False, 'analyzerExecuted': False,
             'targetExecuted': False, 'newArchiveDownloaded': False, 'reviewedRunnerClosure': False,
             'fullDiagnosticEnabled': False, 'sourceManifestSha256': SOURCE_SHA,
             'packetBodySha256': PACKET_SHA, 'failureCode': None, 'failureType': None}
    code = None
    try:
        if sys.argv[1:] or sys.flags.isolated != 1 or sys.flags.no_site != 1 or sys.flags.dont_write_bytecode != 1:
            raise ValueError('Fixed isolated argument-free routing required.')
        flags = {name: os.environ.get(name) for name in EXPECTED}
        route['flags'] = flags
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
        namespace = {'__name__': '__main__', '__file__': str(SOURCE / 'packet.py')}
        exec(code, namespace)
        code = None
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
        if (type(summary.get('captureCompleted')) is not bool or type(summary.get('passed')) is not bool
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
        (OUTPUT / 'routing.json').write_text(json.dumps(route, indent=2, sort_keys=True) + '\n')
        print('Finite corroboration routing: ' + ('CAPTURED ONLY' if route['passed'] else 'NOT GREEN'))
    return 0


if __name__ == '__main__':
    main()
