#!/usr/bin/env python3
"""Scratch CI entry point for sealed Real SMT diagnostics, never acceptance."""
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import stat
import sys
import types

ROOT = Path(__file__).resolve().parents[2]
SEALED = ROOT / '.github/review/real-smt-capture'
SOURCE_SEAL = '4792f707a0f00dfd153478a9911065ceb55428fe5f688a79c714b39aa1778d03'
MAX_SOURCE = 1024 * 1024


def require(condition, message):
    if not condition: raise ValueError(message)


def captured_regular(path, maximum, allow_empty=False):
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and (0 if allow_empty else 1) <= before.st_size <= maximum, 'Bounded regular input required: ' + path.name)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    try:
        pinned = os.fstat(fd)
        require((pinned.st_dev,pinned.st_ino) == (before.st_dev,before.st_ino), 'Input identity changed before capture')
        data = bytearray()
        while block := os.read(fd,65536):
            data.extend(block); require(len(data) <= maximum, 'Source capture byte bound')
        after = os.fstat(fd)
        require((pinned.st_dev,pinned.st_ino,pinned.st_size,pinned.st_mtime_ns,pinned.st_ctime_ns) ==
          (after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns,after.st_ctime_ns) and len(data) == pinned.st_size,
          'Input changed during capture')
        return bytes(data)
    finally: os.close(fd)


def source_snapshot():
    raw = captured_regular(SEALED/'source-manifest.json',65536)
    require(hashlib.sha256(raw).hexdigest() == SOURCE_SEAL, 'Approved diagnostic source seal differs')
    manifest = json.loads(raw)
    require(manifest['schemaVersion'] == 1 and manifest['diagnosticOnly'] is True and len(manifest['files']) == 19,
      'Approved diagnostic source inventory differs')
    captured = {}; total = 0
    for name,entry in manifest['files'].items():
        relative = Path(name)
        require(not relative.is_absolute() and '..' not in relative.parts, 'Unsafe source-seal path')
        data = captured_regular(SEALED/relative,MAX_SOURCE); total += len(data)
        require(total <= 4 * MAX_SOURCE and len(data) == entry['bytes'] and hashlib.sha256(data).hexdigest() == entry['sha256'],
          'Approved diagnostic source payload differs: ' + name)
        captured[name] = data
    require(captured_regular(SEALED/'source-manifest.json',65536) == raw, 'Seal changed while capturing source')
    return manifest,captured


def main():
    output = ROOT/'out/b3-native-compile'
    require(not output.exists(), 'Scratch diagnostic output must be fresh')
    output.mkdir(parents=True)
    receipt = {'diagnosticOnly':True,'acceptanceClaimed':False,'instrumentedAcceptanceClaimed':False,
      'sourceSealSha256':SOURCE_SEAL,'libraryRebuilt':False,'compilerProductRebuilt':False,
      'declaredCIHead':os.environ.get('GITHUB_SHA'),'receiptProduced':False,
      'prerequisiteSetupOk':os.environ.get('REAL_SMT_SETUP_OK') == 'true','sourceSealValidated':False}
    try:
        require(len(sys.argv) == 1 and os.environ.get('GITHUB_EVENT_NAME') == 'workflow_dispatch' and
          os.environ.get('GITHUB_REPOSITORY') == 'erniecohen/dafny' and
          os.environ.get('GITHUB_REF','').startswith('refs/heads/scratch/') and
          os.environ.get('B3_FOCUS_GATE') == 'real-smt-capture' and
          os.environ.get('B3_COMPILE_ONLY') == 'true' and os.environ.get('B3_FULL_GATE') == 'false',
          'Require scratch workflow_dispatch, real-smt-capture focus, compile-only=true and full=false')
        require(sys.platform == 'linux' and platform.machine() == 'x86_64' and os.geteuid() != 0,
          'Require non-root Linux x64 diagnostic execution')
        receipt['effectiveUid'] = os.geteuid()
        receipt['routingSourceSha256'] = hashlib.sha256(captured_regular(Path(__file__),MAX_SOURCE)).hexdigest()
        receipt['workflowSourceSha256'] = hashlib.sha256(captured_regular(ROOT/'.github/workflows/review.yml',MAX_SOURCE)).hexdigest()
        receipt['productLedger'] = captured_regular(ROOT/'.github/review/base',4096).decode().strip()
        setup = ROOT/'out/b3-real-smt-setup'
        receipt['prerequisiteSetupFiles'] = {}
        for name in ['status.txt','apt-update.log','apt-install.log','package-version.log','version.log','executable-sha256.log','executable-pin.log']:
            path = setup/name
            if path.exists():
                data = captured_regular(path,4 * MAX_SOURCE,allow_empty=True)
                receipt['prerequisiteSetupFiles'][name] = {'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}
                if name == 'status.txt':
                    require(len(data) <= 8192, 'Setup status byte bound')
                    receipt['prerequisiteSetupStatus'] = data.decode('ascii')
        require(receipt['prerequisiteSetupOk'], 'Reviewed tracer prerequisite setup failed; no SDK/parser/trace stages started')
        require(receipt.get('prerequisiteSetupStatus','').endswith('setupPassed=true\n'), 'Setup success receipt missing')
        require(captured_regular(setup/'package-version.log',4096) == b'6.8-0ubuntu2\n' and
          captured_regular(setup/'version.log',65536).splitlines()[0] == b'strace -- version 6.8', 'Exact setup tracer pins differ')
        raw_hash = captured_regular(setup/'executable-sha256.log',4096).decode('ascii')
        matched = re.fullmatch(r'([0-9a-f]{64})  /usr/bin/strace\n',raw_hash)
        require(matched is not None, 'Setup tracer digest record differs')
        tracer = Path(shutil.which('strace') or '').resolve(strict=True)
        require(tracer == Path('/usr/bin/strace').resolve(strict=True), 'Installed tracer shadowed on invocation PATH')
        receipt['setupTracerSha256'] = matched[1]
        require(hashlib.sha256(captured_regular(tracer,16 * MAX_SOURCE)).hexdigest() == matched[1], 'Tracer bytes changed after setup')
        manifest,captured = source_snapshot()
        receipt['sourceSealValidated'] = True
        receipt['approvedSourceManifest'] = manifest
        path = SEALED/'gate.py'
        gate = types.ModuleType('b3_real_smt_capture_gate'); gate.__file__ = str(path)
        # Execute only the bytes captured under the literal seal, never an import or cached pyc.
        exec(compile(captured['gate.py'],str(path),'exec'),gate.__dict__)
        diagnostic = output/'real-smt-capture'
        original_argv = sys.argv
        try:
            sys.argv = [str(path),str(diagnostic)]
            require(gate.main() == 0, 'Diagnostic entry returned nonzero')
        finally: sys.argv = original_argv
        require(source_snapshot() == (manifest,captured), 'Approved source changed after diagnostic execution')
        raw = captured_regular(diagnostic/'summary.json',8 * MAX_SOURCE)
        inner = json.loads(raw)
        require(inner['diagnosticOnly'] is True and inner['acceptanceClaimed'] is False,
          'Diagnostic receipt boundary differs')
        observed_tracer = inner.get('diagnostic',{}).get('straceExecutableSha256')
        require(observed_tracer in (None,receipt['setupTracerSha256']), 'Diagnostic tracer digest differs from successful setup')
        receipt['receiptProduced'] = True
        receipt['diagnosticReceiptSha256'] = hashlib.sha256(raw).hexdigest()
        receipt['cleanupPoisoned'] = inner.get('cleanupPoisoned',False)
        receipt['diagnosticFailure'] = inner.get('failure')
        receipt['captureSummary'] = {key:inner.get('diagnostic',{}).get(key,False)
          for key in ['diagnosticDenominatorComplete','allCapturesComplete','allMathematicalExpectationsMatched']}
    except Exception as error:
        receipt['failure'] = type(error).__name__ + ': ' + str(error)
    finally:
        (output/'summary.json').write_text(json.dumps(receipt,indent=2)+'\n')
        print('Real SMT capture diagnostic receipt:',receipt['receiptProduced'],'acceptance False',flush=True)
        if os.environ.get('GITHUB_STEP_SUMMARY'):
            with open(os.environ['GITHUB_STEP_SUMMARY'],'a') as stream:
                stream.write('Real SMT capture routing: diagnostic only; zero exit does not establish acceptance.\n\n')
                if 'failure' in receipt: stream.write(receipt['failure']+'\n')
    return 0


if __name__ == '__main__': raise SystemExit(main())
