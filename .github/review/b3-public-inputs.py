"""Fetch hash-pinned public bootstrap and solver inputs for the scratch B3 gate."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.request
import zipfile

output = Path('out/b3-native-compile/inputs')
output.mkdir(parents=True, exist_ok=True)
bootstrap_sha = 'ba06f4d5048ecf0cc40f79281230eb44b429fd73a8bbed5c4377a3d0ff010331'
solver_name = 'z3-5.1.0-x64-glibc-2.39'
solver_release = 'https://github.com/Z3Prover/z3/releases/download/z3-5.1.0'
solver_archive_sha = 'f47be8d27d3230e823bf1eeede2fe0abaca55bb78d0b59974370e6689a92284a'
solver_sha = 'b4e0b3483ce37817230b20d6cad48390eb6a3aefde1d93342ad6dc763f24bc23'
assert os.environ['Z3_RELEASE'] == solver_release
assert os.environ['Z3_LINUX_X64'] == solver_name
assert os.environ['Z3_LINUX_X64_SHA256'] == solver_archive_sha
subprocess.run(['gh', 'run', 'download', '37157932239', '-R', 'erniecohen/dafny',
                '-n', 'dafny', '-D', str(output / 'bootstrap')], check=True, timeout=120)
archive = output / 'bootstrap/dafny.tar.gz'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == bootstrap_sha
solver_archive = output / 'z3.zip'
url = solver_release + '/' + solver_name + '.zip'
with urllib.request.urlopen(url, timeout=60) as source, solver_archive.open('wb') as target:
    while block := source.read(65536):
        target.write(block)
assert hashlib.sha256(solver_archive.read_bytes()).hexdigest() == solver_archive_sha
with zipfile.ZipFile(solver_archive) as packed_solver:
    packed_solver.extractall(output)
solver = output / solver_name / 'bin/z3'
assert hashlib.sha256(solver.read_bytes()).hexdigest() == solver_sha
solver.chmod(0o755)
assert subprocess.check_output([str(solver), '-version'], text=True).strip() == 'Z3 version 5.1.0 - 64 bit'
installed = Path('Binaries/z3/bin/z3-5.1.0')
installed.parent.mkdir(parents=True, exist_ok=True)
installed.write_bytes(solver.read_bytes())
installed.chmod(0o755)
(output / 'receipt.json').write_text(json.dumps({
    'bootstrapPublicRun': 'https://github.com/erniecohen/dafny/actions/runs/37157932239',
    'bootstrapArchiveSha256': bootstrap_sha,
    'solverArchiveSha256': solver_archive_sha,
    'solverExecutableSha256': solver_sha,
    'solverVersion': '5.1.0',
    'verifiedLibraryReused': False,
}, indent=2) + '\n')
print('Public bootstrap and Z3 input hashes passed')
