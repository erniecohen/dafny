"""Keep M1's historical identity guard strict without freezing later compilers."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('identity', Path(__file__).with_name('issue193-identity.py'))
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)


class IdentityTests(unittest.TestCase):
    def setUp(self):
        scratch = Path('work/issue193-identity-tests')
        scratch.mkdir(parents=True, exist_ok=True)
        directory = tempfile.TemporaryDirectory(dir=scratch.resolve())
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.git('init', '-q')
        self.git('config', 'user.name', 'Identity test')
        self.git('config', 'user.email', 'test@example.invalid')
        self.git('config', 'commit.gpgsign', 'false')
        (self.root / 'hooks').mkdir()
        self.git('config', 'core.hooksPath', str(self.root / 'hooks'))
        self.write('Source/Compiler.cs', 'original compiler\n')
        self.base = self.commit()
        self.add_optional()
        self.introduced = self.commit()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    def add_optional(self, missing=None):
        for path in sorted(identity.ADDED - ({missing} if missing else set())):
            self.write(path, 'optional proof\n')

    def commit(self):
        self.git('add', '.')
        self.git('commit', '-qm', 'test input')
        return self.git('rev-parse', 'HEAD')

    def check(self):
        with patch.multiple(identity, ROOT=self.root, BASE=self.base, INTRODUCED=self.introduced):
            return identity.check()

    def test_later_independent_compiler_change_retains_the_bounded_claim(self):
        self.write('Source/Compiler.cs', 'independently reviewed compiler fix\n')
        head = self.commit()
        result = self.check()
        self.assertEqual(result['introduced'], self.introduced)
        self.assertEqual(result['head'], head)
        self.assertTrue(result['existing_product_input_identity'])
        self.assertTrue(result['current_optional_input_identity'])

    def test_introducing_commit_cannot_change_existing_product_inputs(self):
        self.git('checkout', '-q', self.base)
        self.add_optional()
        self.write('Source/Compiler.cs', 'unexpected compiler change\n')
        self.introduced = self.commit()
        with self.assertRaisesRegex(ValueError, 'Old product input changed'):
            self.check()

    def test_current_optional_input_change_is_rejected(self):
        self.write(sorted(identity.ADDED)[0], 'different proof\n')
        self.commit()
        with self.assertRaisesRegex(ValueError, 'Current optional inputs differ'):
            self.check()

    def test_missing_introduced_input_is_rejected(self):
        self.git('checkout', '-q', self.base)
        self.add_optional(missing=sorted(identity.ADDED)[0])
        self.introduced = self.commit()
        with self.assertRaisesRegex(ValueError, 'every optional input'):
            self.check()

    def test_advancing_the_base_cannot_make_the_check_vacuous(self):
        self.base = self.introduced
        with self.assertRaisesRegex(ValueError, 'introducing commit parent'):
            self.check()


if __name__ == '__main__':
    unittest.main()
