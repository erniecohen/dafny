import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
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

    def test_cross_published_catalog_inspection_never_executes_apphost(self):
        with tempfile.TemporaryDirectory() as tmp:
            directory = Path(tmp)
            archive = directory / 'linux.zip'
            with zipfile.ZipFile(archive, 'w') as package:
                package.writestr('dafny/DafnyPipeline.dll', 'managed fixture')
            def reader(command, **kwargs):
                self.assertEqual(command[0], 'dotnet')
                resources = Path(command[-1]); resources.mkdir()
                self.fixture(resources)
                from types import SimpleNamespace
                return SimpleNamespace(returncode=0)
            with patch.object(catalog.subprocess, 'run', side_effect=reader) as run:
                catalog.check_archive(archive, directory / 'reader.dll', directory / 'z3', inspect_only=True)
                self.assertEqual(run.call_count, 1)

    def test_both_archives_and_checksums_are_required(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / '.github/workflows/review.yml').read_text()
        self.assertIn('-r linux-x64 --self-contained', workflow)
        self.assertIn('cmp release-catalog-core.dll out/linux/dafny/DafnyCore.dll', workflow)
        self.assertIn('check-linux-release.py "$name"', workflow)
        self.assertIn('check-release-catalog.py "$name" --inspect-only', workflow)
        self.assertIn('for rid in osx-arm64 linux-x64; do', workflow)
        self.assertIn('test -f "dafny-${{ needs.build.outputs.version }}-$rid.zip"', workflow)
        self.assertIn('shasum -a 256 dafny-*.zip | tee SHA256SUMS', workflow)
        self.assertIn('cat SHA256SUMS', workflow)
        self.assertIn('Linux execution is qualified downstream', workflow)

    def test_release_produces_requires_and_checks_embedded_catalog(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / '.github/workflows/review.yml').read_text().split('\n  binaries:\n', 1)[1].split('\n  verdict:\n', 1)[0]
        self.assertIn('timeout-minutes: 240', workflow)
        self.assertLess(workflow.index('-t:RunCoco'), workflow.index('dotnet publish'))
        self.assertEqual(workflow.count('-p:PathMap="$PWD=/_/"'), 3)
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
