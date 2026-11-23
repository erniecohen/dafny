#!/usr/bin/env python3
"""Inspect the cross-published Linux archive without executing its apphost."""
import argparse
from pathlib import Path
import zipfile

REQUIRED = {'dafny/' + name for name in [
    'dafny', 'Dafny.dll', 'DafnyCore.dll', 'DafnyPipeline.dll', 'Dafny.deps.json',
    'Dafny.runtimeconfig.json', 'System.Private.CoreLib.dll', 'libcoreclr.so',
    'libhostfxr.so', 'libhostpolicy.so', 'LICENSE.txt', 'NOTICES.txt', 'REVIEW.md']}


def check(archive):
    with zipfile.ZipFile(archive) as package:
        if package.testzip() is not None:
            raise ValueError('Corrupt Linux release archive')
        missing = REQUIRED - set(package.namelist())
        if missing:
            raise ValueError('Missing Linux release files: ' + ', '.join(sorted(missing)))
        header = package.read('dafny/dafny')[:20]
        if len(header) != 20 or header[:6] != b'\x7fELF\x02\x01' or header[18:20] != b'\x3e\x00':
            raise ValueError('Linux apphost must be little-endian ELF64 x86-64')
        print(f'Linux release: {len(package.namelist())} entries; required files and ELF64 x86-64 apphost checked')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    check(parser.parse_args().archive)
