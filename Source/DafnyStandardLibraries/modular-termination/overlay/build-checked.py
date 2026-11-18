"""Build recorded common/target Std snapshots with the normal, complete library producer."""
import argparse
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dafny', required=True, help='Dafny command, relative to the standard-library directory')
parser.add_argument('--solver', required=True, help='Z3 5.1.0 executable')
parser.add_argument('--cores', type=int, default=3)
parser.add_argument('--target', choices=['all', 'common', 'notarget', 'cs', 'java', 'js', 'go', 'py'], default='all')
args = parser.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[1]
source = root / 'build/checked/Std'
solver = str(Path(args.solver).resolve())
version = subprocess.check_output([solver, '--version'], text=True)
if not version.startswith('Z3 version 5.1.0 '):
    raise SystemExit('checked Std requires Z3 5.1.0')
if args.cores < 1:
    raise SystemExit('--cores must be positive')
if source.exists():
    if not (source / 'CheckedOverlay.dfy').is_file():
        raise SystemExit('refusing to replace a directory without checked-Std provenance')
    shutil.rmtree(source)
subprocess.run([sys.executable, str(here / 'build-source.py'), str(source)], check=True)
command = shlex.split(args.dafny)
common = 'binaries/DafnyStandardLibraries-checked.doo'
base = ['build', '--target:lib', '--solver-path', solver, '--cores', str(args.cores)]
if args.target in ['all', 'common']:
    subprocess.run(command + base + ['build/checked/Std/dfyconfig.toml', '--output:' + common], cwd=root, check=True)
if args.target != 'common':
    if not (root / common).is_file():
        raise SystemExit('build the checked common library first with --target common')
    # An existing certificate must describe this recipe, not an older overlay
    # built by the same verifier. Admission separately checks verifier/solver identity.
    recipe = (source / 'CheckedOverlay.dfy').read_text().split('"')[1]
    with zipfile.ZipFile(root / common) as archive:
        retained = archive.read('program').decode('utf-8')
    if recipe not in retained:
        raise SystemExit('checked common library uses a different overlay; rebuild with --target common')
    for target in (['notarget', 'cs', 'java', 'js', 'go', 'py'] if args.target == 'all' else [args.target]):
        inputs = sorted(source.glob('TargetSpecific/*-' + target + '*.dfy'))
        files = [str(p.relative_to(root)) for p in inputs]
        subprocess.run(command + base + ['build/checked/Std/TargetSpecific/dfyconfig.toml'] + files +
                       ['--library:' + common, '--output:binaries/DafnyStandardLibraries-checked-' + target + '.doo'],
                       cwd=root, check=True)
