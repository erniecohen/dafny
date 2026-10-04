"""Fetch hash-pinned public bootstrap and solver inputs for the scratch B3 gate."""
import hashlib
import os
from pathlib import Path
import subprocess
import urllib.request
import zipfile

output = Path('out/b3-native-compile/inputs')
output.mkdir(parents=True, exist_ok=True)
subprocess.run(['gh', 'run', 'download', '37157932239', '-R', 'erniecohen/dafny',
                '-n', 'dafny', '-D', str(output / 'bootstrap')], check=True, timeout=120)
archive = output / 'bootstrap/dafny.tar.gz'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == 'ba06f4d5048ecf0cc40f79281230eb44b429fd73a8bbed5c4377a3d0ff010331'
solver_archive = output / 'z3.zip'
url = os.environ['Z3_RELEASE'] + '/' + os.environ['Z3_LINUX_X64'] + '.zip'
with urllib.request.urlopen(url, timeout=60) as source, solver_archive.open('wb') as target:
    while block := source.read(65536):
        target.write(block)
assert hashlib.sha256(solver_archive.read_bytes()).hexdigest() == os.environ['Z3_LINUX_X64_SHA256']
with zipfile.ZipFile(solver_archive) as archive:
    archive.extractall(output)
solver = output / os.environ['Z3_LINUX_X64'] / 'bin/z3'
solver.chmod(0o755)
assert subprocess.check_output([str(solver), '-version'], text=True).strip() == 'Z3 version 5.1.0 - 64 bit'
installed = Path('Binaries/z3/bin/z3-5.1.0')
installed.parent.mkdir(parents=True, exist_ok=True)
installed.write_bytes(solver.read_bytes())
installed.chmod(0o755)
print('Public bootstrap and Z3 input hashes passed')
