"""Copies a .doo library without the key legacy-no-modular-termination in its manifest, as a version of Dafny without
the rules of https://github.com/erniecohen/dafny/issues/211 writes it."""
import sys
import zipfile

source, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(source) as old, zipfile.ZipFile(target, 'w') as new:
    for item in old.infolist():
        data = old.read(item.filename)
        if item.filename == 'manifest.toml':
            lines = data.decode().split('\n')
            kept = [l for l in lines if not l.startswith('legacy-no-modular-termination')]
            assert len(kept) == len(lines) - 1, 'the manifest has no legacy-no-modular-termination key'
            data = '\n'.join(kept).encode()
        new.writestr(item, data)
