"""Introduction identity remains strict without claiming later compilers are identical."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class ReferenceIntroductionIdentityTests(unittest.TestCase):
    def test_selected_introduction_passes_and_later_compiler_changes_still_reject(self):
        source = Path(__file__).with_name('issue193-identity.py').read_text()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)

            def git(*arguments):
                return subprocess.check_output(['git', *arguments], cwd=root, text=True).strip()

            git('init', '-q')
            git('config', 'user.name', 'Test fixture')
            git('config', 'user.email', 'fixture@example.invalid')
            script = root / '.github/review/issue193-identity.py'
            script.parent.mkdir(parents=True)
            script.write_text(source)
            compiler = root / 'Source/DafnyCore/Core.cs'
            compiler.parent.mkdir(parents=True)
            compiler.write_text('old compiler input\n')
            git('add', '.github', 'Source')
            git('commit', '-qm', 'Base fixture')
            baseline = git('rev-parse', 'HEAD')
            script.with_name('issue193-base').write_text(baseline + '\n')
            model = root / 'Source/DafnyStandardLibraries/optional/IntegerBitwise.dfy'
            model.parent.mkdir(parents=True)
            model.write_text('optional proof fixture\n')
            git('add', '.github', 'Source')
            git('commit', '-qm', 'Reference introduction fixture')
            introduction = git('rev-parse', 'HEAD')
            compiler.write_text('later compiler input\n')
            git('add', 'Source')
            git('commit', '-qm', 'Later compiler fixture')
            later = git('rev-parse', 'HEAD')
            output = root / 'results'
            args = [os.sys.executable, str(script), str(output)]
            selected = subprocess.run(args + ['--product-commit', introduction], capture_output=True, text=True)
            self.assertEqual(selected.returncode, 0, selected.stderr)
            identity = json.loads((output / 'identity.json').read_text())
            self.assertEqual(identity['head'], introduction)
            self.assertEqual(identity['candidate_head'], later)
            for extra in ([], ['--product-commit', later]):
                rejected = subprocess.run(args + extra, capture_output=True, text=True)
                self.assertNotEqual(rejected.returncode, 0)
                self.assertIn('Source/DafnyCore/Core.cs', rejected.stderr)


if __name__ == '__main__':
    unittest.main()
