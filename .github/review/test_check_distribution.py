import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('distribution', Path(__file__).with_name('check-distribution.py'))
distribution = importlib.util.module_from_spec(spec)
spec.loader.exec_module(distribution)


class DistributionTests(unittest.TestCase):
    def test_rejects_private_binary_paths_in_both_encodings(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for encoding in ['utf-8', 'utf-16-le']:
                (root / 'compiler.dll').write_bytes('private-checkout/source.cs'.encode(encoding))
                with self.assertRaisesRegex(ValueError, 'compiler.dll'):
                    distribution.check(root, ['private-checkout'])

    def test_accepts_normalized_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'compiler.dll').write_bytes(b'/_/source.cs')
            distribution.check(root, ['private-checkout'])

    def test_dependency_hosted_build_path_is_not_this_checkout(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'Dependency.dll').write_bytes(b'hosted-home/other-project/source.cs')
            distribution.check(root, ['hosted-home/this-project'], home='hosted-home')
            (root / 'DafnyCore.dll').write_bytes(b'hosted-home/private-checkout/source.cs')
            with self.assertRaisesRegex(ValueError, 'DafnyCore.dll'):
                distribution.check(root, ['hosted-home/this-project'], home='hosted-home')

    def test_rejects_apple_archive_metadata(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            archive = root / 'binary.zip'
            with zipfile.ZipFile(archive, 'w') as handle:
                handle.writestr('dafny/._compiler.dll', b'metadata')
            with self.assertRaisesRegex(ValueError, 'Apple metadata'):
                distribution.check(root, ['private-checkout'], archive)


if __name__ == '__main__':
    unittest.main()
