import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('catalog', Path(__file__).with_name('check-release-catalog.py'))
catalog = importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalog)


class ReleaseCatalogTests(unittest.TestCase):
    def fixture(self, directory):
        for name in catalog.RESOURCES:
            self.bundle(directory / name)

    def bundle(self, path, status='complete', build='same-build'):
        with zipfile.ZipFile(path, 'w') as archive:
            archive.writestr('program', 'module Example {}')
            archive.writestr('termination-contracts.json', '{}')
            archive.writestr('manifest.toml', '[modular_termination]\nenabled = true\n' +
                             f'status = "{status}"\nfork_build_id = "{build}"\n')

    def test_complete_catalog(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            self.fixture(directory)
            catalog.check_resources(directory)

    def test_missing_target_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            self.fixture(directory)
            extra = directory / 'DafnyStandardLibraries-checked-unsupported.doo'
            self.bundle(extra)
            with self.assertRaisesRegex(ValueError, 'unexpected.*checked-unsupported'):
                catalog.check_resources(directory)
            extra.unlink()
            (directory / 'DafnyStandardLibraries-checked-cs.doo').unlink()
            with self.assertRaisesRegex(ValueError, 'missing.*checked-cs'):
                catalog.check_resources(directory)

    def test_incomplete_or_mixed_build_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            self.fixture(directory)
            path = directory / 'DafnyStandardLibraries-checked.doo'
            self.bundle(path, status='incomplete')
            with self.assertRaisesRegex(ValueError, 'complete record'):
                catalog.check_resources(directory)
            self.bundle(path, build='other-build')
            with self.assertRaisesRegex(ValueError, 'different verifier builds'):
                catalog.check_resources(directory)

    def test_release_produces_requires_and_checks_embedded_catalog(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / '.github/workflows/review.yml').read_text().split('\n  binaries:\n', 1)[1].split('\n  verdict:\n', 1)[0]
        self.assertIn('timeout-minutes: 240', workflow)
        self.assertLess(workflow.index('-t:RunCoco'), workflow.index('dotnet publish'))
        self.assertEqual(workflow.count('-p:PathMap="$PWD=/_/"'), 2)
        self.assertLess(workflow.index('modular-termination-checked-std.py'),
                        workflow.index('-p:RequireCheckedStandardLibraries=true'))
        self.assertIn('cmp release-catalog-core.dll out/dafny/DafnyCore.dll', workflow)
        self.assertLess(workflow.index('zip -Xqr'), workflow.index('check-release-catalog.py'))
        project = (root / 'Source/DafnyPipeline/DafnyPipeline.csproj').read_text()
        for name in catalog.RESOURCES:
            self.assertIn(name, project)
        self.assertIn("!Exists('%(RequiredCheckedStandardLibrary.Identity)')", project)


if __name__ == '__main__':
    unittest.main()
