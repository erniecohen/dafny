"""Exercise product versioning with real commits and depth-1 fetches."""
import hashlib
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).with_name('version.sh').read_text()
EXCLUDED = ['Source/IntegrationTests/example.dfy', 'Source/Example.Test/Test.cs',
            'Source/Example.Tests/Test.cs', 'docs/example.md', '.github/example',
            'friction/example', 'REVIEW.md', 'ROADMAP.md', 'AGENTS.md', 'CLAUDE.md']


class VersionTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix='version-')
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name) / 'source'
        self.root.mkdir()
        self.git('init', '-q')
        self.git('config', 'user.name', 'Version test')
        self.git('config', 'user.email', 'test@example.invalid')
        self.git('config', 'commit.gpgsign', 'false')
        self.git('config', 'core.hooksPath', '/dev/null')
        self.write('Source/Directory.Build.props', '<VersionPrefix>4.11.0</VersionPrefix>\n')
        self.write('Source/Product.cs', 'original\n')
        self.commit()
        self.git('tag', 'v4.11.0')
        self.upstream = self.git('rev-parse', '--short=8', 'HEAD')
        self.write('Source/Product.cs', 'fixed\n')
        self.product = self.commit()
        self.write('.github/review/base', f'v4.11.0 {self.product}\n')
        self.write('.github/review/version.sh', SCRIPT)
        self.commit()
        self.git('remote', 'add', 'origin', self.root.as_uri())

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()

    def write(self, path, contents):
        dest = self.root / path
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(contents)

    def commit(self):
        self.git('add', '.')
        self.git('commit', '-qm', 'Version fixture')
        return self.git('rev-parse', 'HEAD')

    def version(self):
        return subprocess.check_output(['sh', '.github/review/version.sh'],
                                       cwd=self.root, text=True)

    def test_matches_previous_stamped_diff(self):
        diff = subprocess.check_output(['git', 'diff', '--no-ext-diff', '--no-color',
                                       '--no-renames', '--abbrev=7', 'v4.11.0', self.product,
                                       '--', 'Source'], cwd=self.root)
        revision = f'{self.upstream}.review.{hashlib.sha256(diff).hexdigest()[:8]}'
        self.assertEqual(self.version(), f'revision={revision}\nversion=4.11.0+{revision}\n')

    def test_product_edits_additions_and_deletions_change_version_without_restamp(self):
        for path, contents in [('Source/Product.cs', 'changed\n'),
                               ('Source/New.cs', 'new\n'), ('Source/New.cs', None)]:
            with self.subTest(path=path, contents=contents):
                before = self.version()
                if contents is None:
                    (self.root / path).unlink()
                else:
                    self.write(path, contents)
                self.commit()
                self.assertNotEqual(before, self.version())

    def test_all_nonproduct_paths_keep_version(self):
        before = self.version()
        for path in EXCLUDED:
            self.write(path, 'excluded\n')
        self.commit()
        self.assertEqual(before, self.version())

    def test_second_word_is_optional_and_ignored_even_if_unresolvable(self):
        before = self.version()
        for contents in ['v4.11.0\n', 'v4.11.0 not-a-commit\n']:
            self.write('.github/review/base', contents)
            self.commit()
            self.assertEqual(before, self.version())

    def test_uncommitted_product_changes_do_not_affect_head_version(self):
        before = self.version()
        self.write('Source/Product.cs', 'uncommitted\n')
        self.assertEqual(before, self.version())

    def test_depth_one_checkout_fetches_only_upstream_endpoint(self):
        before = self.version()
        source = self.root
        clone = source.parent / 'shallow'
        subprocess.check_call(['git', 'clone', '-q', '--depth=1', '--no-tags',
                               source.as_uri(), str(clone)])
        self.root = clone
        self.assertEqual(self.git('rev-parse', '--is-shallow-repository'), 'true')
        self.assertNotEqual(subprocess.run(['git', 'cat-file', '-e', self.product],
                                          cwd=clone, capture_output=True).returncode, 0)
        self.assertEqual(before, self.version())
        self.assertNotEqual(subprocess.run(['git', 'cat-file', '-e', self.product],
                                          cwd=clone, capture_output=True).returncode, 0)

    def test_without_base_uses_commit_revision(self):
        (self.root / '.github/review/base').unlink()
        self.commit()
        revision = self.git('rev-parse', '--short=8', 'HEAD')
        self.assertEqual(self.version(), f'revision={revision}\nversion=4.11.0+{revision}\n')

    def test_release_macos_stops_when_derivation_prints_then_fails(self):
        script = Path(__file__).with_name('release-macos.sh').read_text()
        capture = script[script.index('out=$(cd src'):script.index('(cd src && sh Scripts/')]
        # A failing derivation can print a plausible version before it fails.
        self.write('src/.github/review/version.sh', 'echo version=expected; exit 1\n')
        result = subprocess.run(['sh', '-ec', 'version=expected\n' + capture],
                                cwd=self.root, capture_output=True)
        self.assertNotEqual(result.returncode, 0)

    def test_maintenance_stops_when_derivation_prints_then_fails(self):
        workflow = Path(__file__).parents[1] / 'workflows/maintenance.yml'
        text = workflow.read_text()
        capture = text[text.index('          out=$(sh .github/review/version.sh)'):text.index('          tag="v$version"')]
        for script in ['echo version=expected; exit 1\n', 'exit 0\n']:
            with self.subTest(script=script):
                self.write('.github/review/version.sh', script)
                result = subprocess.run(['sh', '-ec', 'refuse() { exit 1; }; SHA=fixture\n' + capture],
                                        cwd=self.root, capture_output=True)
                self.assertNotEqual(result.returncode, 0)

    def test_maintenance_preserves_derivation_failure_and_tag_guards(self):
        workflow = Path(__file__).parents[1] / 'workflows/maintenance.yml'
        text = workflow.read_text()
        self.assertIn('out=$(sh .github/review/version.sh) || refuse', text)
        self.assertIn('[ -n "$version" ] || refuse', text)
        self.assertIn('refuse "tag $tag already exists"', text)
        review = workflow.with_name('review.yml').read_text()
        self.assertIn('test "${{ github.ref_name }}" = "v${{ needs.build.outputs.version }}"', review)


if __name__ == '__main__':
    unittest.main()
