"""Keep the reviewed build handoffs explicit; actionlint validates YAML structure."""
from pathlib import Path
import re
import unittest

WORKFLOWS = Path(__file__).resolve().parents[1] / 'workflows'

EXPECTED = {'decreases-order.yml': [('build', 'put', 'decreases-candidate', 'candidate.tar.gz'),
                         ('suite', 'get', 'decreases-candidate', '.'),
                         ('std', 'get', 'decreases-candidate', '.')],
 'issue193-reference-probe.yml': [('build', 'put', 'issue193-dafny', 'dafny.tar.gz'),
                                  ('integer-bitwise-reference', 'get', 'issue193-dafny', '.')],
 'issue211-contracts.yml': [('products', 'put', 'identity-${{ matrix.product }}', 'out'),
                            ('identity', 'get', 'identity-base', 'products/base'),
                            ('identity', 'get', 'identity-candidate', 'products/candidate')],
 'issue23.yml': [('build', 'put', 'issue23-builds', 'builds.tar.gz'),
                 ('measure', 'get', 'issue23-builds', '.')],
 'issue241-frontend-profile.yml': [('build', 'put', 'dafny', 'out/dafny'),
                                   ('measure', 'get', 'dafny', 'out/dafny')],
 'issue241-module-window.yml': [('build', 'put', 'issue241-dafny', 'dafny.tar.gz'),
                                ('time', 'get', 'issue241-dafny', '.'),
                                ('smt', 'get', 'issue241-dafny', '.'),
                                ('corpus', 'get', 'issue241-dafny', '.')],
 'issue33-validation.yml': [('baseline', 'put', 'issue33-baseline', 'dafny.tar.gz'),
                            ('build', 'put', 'issue33-candidate', 'dafny.tar.gz'),
                            ('targeted', 'get', 'issue33-baseline', 'baseline-artifact'),
                            ('targeted', 'get', 'issue33-candidate', 'candidate-artifact'),
                            ('corpus', 'get', 'issue33-baseline', 'baseline-artifact'),
                            ('corpus', 'get', 'issue33-candidate', 'candidate-artifact'),
                            ('stdlib', 'get', 'issue33-baseline', 'baseline-artifact'),
                            ('stdlib', 'get', 'issue33-candidate', 'candidate-artifact'),
                            ('translation', 'get', 'issue33-baseline', 'baseline-artifact'),
                            ('translation', 'get', 'issue33-candidate', 'candidate-artifact')],
 'issue74-validation.yml': [('build', 'put', 'issue74-builds', 'builds.tar.gz'),
                            ('targeted', 'get', 'issue74-builds', 'build-artifact'),
                            ('corpus', 'get', 'issue74-builds', 'build-artifact'),
                            ('stdlib', 'get', 'issue74-builds', 'build-artifact')],
 'operators-m1-off.yml': [('binaries', 'put', 'operators-off-binaries', 'operators-off-binaries.tar.gz'),
                          ('compare', 'get', 'operators-off-binaries', '.')],
 'platform-parity.yml': [('build', 'put', 'parity-${{ inputs.platform }}-dafny', 'dafny.tar.gz'),
                         ('suite', 'get', 'parity-${{ inputs.platform }}-dafny', '.'),
                         ('stdlib', 'get', 'parity-${{ inputs.platform }}-dafny', '.')],
 'process-history.yml': [('build', 'put', 'dafny', 'dafny.tar.gz'), ('verify', 'get', 'dafny', '.')],
 'review.yml': [('build', 'put', 'dafny', 'dafny.tar.gz'),
                ('suite', 'get', 'dafny', '.'),
                ('verification-batch', 'get', 'dafny', '.'),
                ('resolver', 'get', 'dafny', '.'),
                ('stdlib', 'get', 'dafny', '.'),
                ('finite-support', 'get', 'dafny', '.'),
                ('statement-conclusions', 'get', 'dafny', '.'),
                ('integer-bitwise-reference', 'get', 'dafny', '.')],
 'typeencoding-a.yml': [('build', 'put', 'character-candidate', 'candidate.tar.gz'),
                        ('suite', 'get', 'character-candidate', '.'),
                        ('std', 'get', 'character-candidate', '.')],
 'typeencoding-map.yml': [('build', 'put', 'map-candidate', 'candidate.tar.gz'),
                          ('suite', 'get', 'map-candidate', '.'),
                          ('std', 'get', 'map-candidate', '.')]}


def handoffs(text):
    found = []
    for job in re.finditer(r'^  ([\w-]+):\n(.*?)(?=^  [\w-]+:\n|\Z)', text, re.M | re.S):
        for step in re.finditer(r'^      - uses: \./\.github/actions/host-artifact\n(.*?)(?=^      - |\Z)',
                                job.group(2), re.M | re.S):
            inputs = dict(re.findall(r'^          (mode|name|path): (.+)$', step.group(1), re.M))
            found.append((job.group(1), inputs.get('mode'), inputs.get('name'), inputs.get('path', '.')))
    return found


class HostArtifactWorkflowTests(unittest.TestCase):
    def test_exact_build_handoffs(self):
        actual = {path.name: handoffs(path.read_text()) for path in WORKFLOWS.glob('*.yml')}
        actual = {name: rows for name, rows in actual.items() if rows}
        self.assertEqual(actual, EXPECTED)

    def test_every_handoff_site_mutation_is_detected(self):
        count = 0
        for name, expected in EXPECTED.items():
            text = (WORKFLOWS / name).read_text()
            for match in re.finditer(r'mode: (put|get)', text):
                mutated = text[:match.start()] + 'mode: wrong' + text[match.end():]
                with self.subTest(workflow=name, site=count):
                    self.assertNotEqual(handoffs(mutated), expected)
                count += 1
        self.assertEqual(count, 51)

    def test_cross_workflow_builds_and_probe_failure_are_preserved(self):
        for workflow, name in [('review.yml', 'dafny'), ('issue74-validation.yml', 'issue74-builds')]:
            text = (WORKFLOWS / workflow).read_text()
            self.assertRegex(text, r'uses: actions/upload-artifact@v4\n(?:        .*\n)*?          name: ' + name + r'\n')
            self.assertIn('if test -n "${HOST_ARTIFACT_DIR:-}"; then enabled=true; fi', text)
        text = (WORKFLOWS / 'issue74-validation.yml').read_text()
        self.assertIn("if: steps.host-handoff.outputs.enabled == 'true' && steps.build.outputs.ready == 'true'", text)
        for workflow in ['issue74-comparisons.yml', 'issue74-regressions.yml', 'issue75-comparisons.yml']:
            text = (WORKFLOWS / workflow).read_text()
            self.assertNotIn('./.github/actions/host-artifact', text)
            self.assertIn('uses: actions/download-artifact@v4', text)


if __name__ == '__main__':
    unittest.main()
