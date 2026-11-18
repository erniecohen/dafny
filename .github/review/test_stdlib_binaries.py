"""Exercise the Makefile's freshness gate with controlled compiler output."""
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
MAKEFILE = ROOT / 'Source/DafnyStandardLibraries/src/Std/TargetSpecific/Makefile'
TARGETS = ('notarget', 'cs', 'java', 'js', 'go', 'py')
MANIFEST = 'solver_version = "5.1.0"\nhidden-no-verify = false\n'


class StandardLibraryBinaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.target = self.root / 'src/Std/TargetSpecific'
        self.target.mkdir(parents=True)
        shutil.copyfile(MAKEFILE, self.target / 'Makefile')
        (self.root / 'binaries').mkdir()
        (self.root / 'build').mkdir()
        # The stub stands in for compilation only. make, unzip, diff and grep are real.
        self.compiler = self.root / 'compiler.py'
        self.compiler.write_text('''import pathlib, sys, zipfile
assert "--solver-path=pinned-z3" in sys.argv
output = next(arg.split(":", 1)[1] for arg in sys.argv if arg.startswith("--output:"))
source = pathlib.Path(output).name
with zipfile.ZipFile(output, "w") as archive:
    archive.writestr("manifest.toml", pathlib.Path("manifest").read_text())
    archive.writestr("program", pathlib.Path("source-" + source).read_text())
''')
        (self.target / 'manifest').write_text(MANIFEST)
        for target in TARGETS:
            self.archive(target)
            (self.target / ('source-' + self.filename(target))).write_text('current program\n')

    @staticmethod
    def filename(target):
        return 'DafnyStandardLibraries-' + target + '.doo'

    def archive(self, target, manifest=MANIFEST, program='current program\n'):
        with zipfile.ZipFile(self.root / 'binaries' / self.filename(target), 'w') as archive:
            archive.writestr('manifest.toml', manifest)
            archive.writestr('program', program)

    def check(self):
        return subprocess.run([
            'make', 'check-binary-all',
            'DAFNY=' + shlex.join([sys.executable, str(self.compiler)]),
            'VERIFY_OPTIONS=--solver-path=pinned-z3',
        ], cwd=self.target, capture_output=True, text=True)

    def test_current_binaries_pass(self):
        result = self.check()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        for target in TARGETS:
            self.assertTrue((self.root / 'build' / self.filename(target)).exists())

    def test_old_solver_fails_for_every_target(self):
        for target in TARGETS:
            with self.subTest(target=target):
                self.archive(target, manifest=MANIFEST.replace('5.1.0', '4.12.1'))
                result = self.check()
                self.assertNotEqual(0, result.returncode)
                self.assertIn('solver_version', result.stdout)
                self.archive(target)

    def test_changed_source_fails_for_every_target(self):
        for target in TARGETS:
            with self.subTest(target=target):
                source = self.target / ('source-' + self.filename(target))
                source.write_text('changed source program\n')
                result = self.check()
                self.assertNotEqual(0, result.returncode)
                self.assertIn('changed source program', result.stdout)
                source.write_text('current program\n')

    def test_unverified_binaries_fail_even_if_they_match(self):
        manifest = MANIFEST.replace('hidden-no-verify = false', 'hidden-no-verify = true')
        (self.target / 'manifest').write_text(manifest)
        for target in TARGETS:
            self.archive(target, manifest=manifest)
        result = self.check()
        self.assertNotEqual(0, result.returncode)
        self.assertIn('ERROR: doo file built with --hidden-no-verify', result.stdout)

    def test_ci_checks_with_pinned_solver_and_default_axioms(self):
        workflow = (ROOT / '.github/workflows/review.yml').read_text()
        job = workflow.split('\n  stdlib:\n', 1)[1].split('\n  axiom-resources:\n', 1)[0]
        step = job.split('      - name: Check target-specific library binaries\n', 1)[1].split('      - ', 1)[0]
        self.assertIn("if: matrix.z3 == '5.1.0' && matrix.axioms == 'off'", step)
        self.assertIn('check-binary-all', step)
        self.assertIn('DAFNY="$PWD/out/dafny/Dafny"', step)
        self.assertIn('--solver-path $Z3', step)
        self.assertNotIn('NO_VERIFY=true', step)


if __name__ == '__main__':
    unittest.main()
