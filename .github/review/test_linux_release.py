import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('linux_release', Path(__file__).with_name('check-linux-release.py'))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class LinuxReleaseTests(unittest.TestCase):
    def fixture(self, archive, missing=None, header=None):
        with zipfile.ZipFile(archive, 'w') as package:
            for name in release.REQUIRED - {missing}:
                content = header if name == 'dafny/dafny' else b'fixture'
                package.writestr(name, content if content is not None else
                                 b'\x7fELF\x02\x01' + bytes(12) + b'\x3e\x00')

    def test_linux_x64_self_contained_layout(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / 'release.zip'
            self.fixture(archive)
            release.check(archive)

    def test_missing_runtime_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / 'release.zip'
            self.fixture(archive, missing='dafny/libcoreclr.so')
            with self.assertRaisesRegex(ValueError, 'Missing.*libcoreclr'):
                release.check(archive)

    def test_wrong_apphost_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / 'release.zip'
            for header in [b'Mach-O', b'\x7fELF\x02\x01' + bytes(12) + b'\xb7\x00']:
                self.fixture(archive, header=header)
                with self.assertRaisesRegex(ValueError, 'ELF64 x86-64'):
                    release.check(archive)


if __name__ == '__main__':
    unittest.main()
