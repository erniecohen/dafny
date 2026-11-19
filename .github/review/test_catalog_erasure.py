from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from catalog_erasure import check_ghost_erasure


class CatalogErasureTests(unittest.TestCase):
    def check(self, text, target='cs', suffix='.cs'):
        with tempfile.TemporaryDirectory(prefix='catalog-erasure-') as directory:
            path = Path(directory) / ('families' + suffix)
            path.write_text(text)
            check_ghost_erasure(path, target)

    def test_retained_dafny_source_is_metadata(self):
        self.check('[assembly: DafnyAssembly.DafnySourceAttribute(@"'
                   'assert p.allowance > 0;\nprint ""maximumProduced"";\n'
                   'ghost const producedAllowanceCeiling: nat;\n")]\n'
                   'public class Producer { public int Next() { return 1; } }')

    def test_compiled_fields_are_rejected_even_beside_metadata(self):
        for field in ['allowance', 'producedAllowanceCeiling', 'maximumProduced']:
            with self.subTest(field=field), self.assertRaisesRegex(AssertionError, 'ghost evidence'):
                self.check('[assembly: DafnyAssembly.DafnySourceAttribute(@"ghost code")]\n'
                           'public class Producer { public int ' + field + '; }')

    def test_incomplete_metadata_is_not_ignored(self):
        with self.assertRaisesRegex(AssertionError, 'ghost evidence'):
            self.check('[assembly: DafnyAssembly.DafnySourceAttribute(@"allowance')

    def test_other_backend_output_is_not_stripped(self):
        with self.assertRaisesRegex(AssertionError, 'ghost evidence'):
            self.check('[assembly: DafnyAssembly.DafnySourceAttribute(@"allowance")]', 'java', '.java')

    def test_nonpositive_core_limit_is_rejected_before_launch(self):
        script = Path(__file__).with_name('modular-termination-catalog.py')
        with tempfile.TemporaryDirectory(prefix='catalog-cores-') as directory:
            result = subprocess.run([sys.executable, str(script), 'unused-dafny', 'unused-solver',
                                     directory, '--cores', '0'], capture_output=True, text=True)
        self.assertEqual(result.returncode, 2)
        self.assertIn('--cores must be positive', result.stderr)


if __name__ == '__main__':
    unittest.main()
