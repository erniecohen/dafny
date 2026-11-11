#!/usr/bin/env python3
"""Reject checkout/home paths and Apple metadata in published distributions."""
import argparse
import os
from pathlib import Path
import tarfile
import zipfile


def check(directory, roots, archive=None):
    needles = [str(root).rstrip('/').encode(encoding) for root in roots
               for encoding in ['utf-8', 'utf-16-le'] if str(root).rstrip('/')]
    bad = [str(path.relative_to(directory)) for path in directory.rglob('*')
           if path.is_file() and any(needle in path.read_bytes() for needle in needles)]
    if bad:
        raise ValueError('Private build paths in: ' + ', '.join(bad))
    if archive:
        if zipfile.is_zipfile(archive):
            with zipfile.ZipFile(archive) as handle:
                names = handle.namelist()
        else:
            with tarfile.open(archive) as handle:
                names = handle.getnames()
        if any(any(part.startswith('._') or part == '__MACOSX' for part in Path(name).parts)
               for name in names):
            raise ValueError('Apple metadata in distribution archive')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--archive', type=Path)
    args = parser.parse_args()
    check(args.directory, [Path.cwd(), Path.home(), os.environ.get('GITHUB_WORKSPACE', '')], args.archive)
    print('Distribution paths and archive metadata checked')
