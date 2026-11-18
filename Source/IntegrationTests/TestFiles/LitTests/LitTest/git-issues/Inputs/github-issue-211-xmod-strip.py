"""Copy a recorded library as an old, unrecorded .doo; promises must still be rejected."""
import sys
import zipfile

source, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(source) as old, zipfile.ZipFile(target, 'w') as new:
    assert 'termination-contracts.json' in old.namelist()
    for item in old.infolist():
        if item.filename == 'termination-contracts.json':
            continue
        data = old.read(item.filename)
        if item.filename == 'manifest.toml':
            text = data.decode()
            assert '[modular_termination]' in text
            text = text.split('[modular_termination]', 1)[0]
            text = text.replace('doo_file_version = "2.0"', 'doo_file_version = "1.0"')
            text = '\n'.join(line for line in text.split('\n')
                             if not line.startswith('legacy-no-modular-termination'))
            data = text.encode()
        new.writestr(item, data)
