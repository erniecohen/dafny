#!/usr/bin/env python3
"""Capture the sealed coordinator's source bytes before executing it; expected failures exit zero."""
import hashlib
import json
import os
from pathlib import Path
import stat
import sys
import types

HERE=Path(__file__).resolve().parent

def captured(path,maximum):
    before=path.lstat()
    if not stat.S_ISREG(before.st_mode) or not 0<before.st_size<=maximum:raise ValueError('Bounded regular source required')
    fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
    try:
        pinned=os.fstat(fd)
        if (pinned.st_dev,pinned.st_ino)!=(before.st_dev,before.st_ino):raise ValueError('Source changed at open')
        data=bytearray()
        while block:=os.read(fd,65536):
            data.extend(block)
            if len(data)>maximum:raise ValueError('Source byte bound')
        after=os.fstat(fd)
        if len(data)!=pinned.st_size or (pinned.st_dev,pinned.st_ino,pinned.st_size,pinned.st_mtime_ns,pinned.st_ctime_ns)!=(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns,after.st_ctime_ns):raise ValueError('Source changed while reading')
        return bytes(data)
    finally:os.close(fd)

def main():
    if len(sys.argv)!=2:raise ValueError('Usage: gate.py NEW_OUTPUT_DIRECTORY')
    output=Path(sys.argv[1]).resolve()
    try:
        manifest=json.loads(captured(HERE/'source-manifest.json',65536));source=captured(HERE/'coordinator.py',1024*1024)
        declared=manifest['files']['coordinator.py'];digest=hashlib.sha256(source).hexdigest()
        if len(source)!=declared['bytes'] or digest!=declared['sha256']:raise ValueError('Captured coordinator differs from seal')
        module=types.ModuleType('unsigned_source_coordinator');module.__file__=str(HERE/'coordinator.py')
        exec(compile(source,module.__file__,'exec'),module.__dict__)
        hashes=module.seal()
        if hashes['coordinator.py']!=digest:raise ValueError('Captured coordinator changed at seal')
        return module.main(output)
    except Exception as error:
        if not output.exists():
            output.mkdir(parents=True,exist_ok=False)
            with (output/'summary.json').open('x') as stream:
                stream.write(json.dumps({'diagnosticOnly':True,'acceptanceClaimed':False,
                  'diagnosticReceiptProduced':False,'bootstrapFailure':type(error).__name__+': '+str(error)},indent=2)+'\n')
        print('Unsigned diagnostic bootstrap failed; diagnostic only; acceptance: False',flush=True)
        return 0

if __name__=='__main__':raise SystemExit(main())
