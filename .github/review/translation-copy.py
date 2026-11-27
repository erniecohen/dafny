#!/usr/bin/env python3
"""Real-solver equality and non-vacuity controls for issue 241's graph copier."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile


def sibling(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def controls(directory):
    results = json.loads((Path(directory) / 'log.json').read_text())['verificationResults']
    vcs = [(r['name'], v) for r in results for v in r['vcResults']]
    assert vcs and any(v['resourceCount'] > 0 for _, v in vcs), 'no solver work'
    assert any('Good' in name and v['outcome'] == 'Valid' for name, v in vcs), 'positive control missing'
    bad = [v for name, v in vcs if 'Bad' in name]
    assert len(bad) == 1 and bad[0]['outcome'] == 'Invalid', 'assert-false control did not fail'


def run(out, dafny, solver, source):
    window = sibling('issue241-module-window')
    boogie = sibling('translation-workers')
    Path(out).mkdir(parents=True, exist_ok=True)
    # The lit shell filters its environment. Keep child scratch beside the explicit outputs.
    os.environ.setdefault('TMPDIR', str(Path(out).resolve()))
    with tempfile.TemporaryDirectory(prefix='copy-', dir=out) as temporary:
        root = Path(temporary)
        comparisons = 0
        for refresh, extra in [(False, []), (True, []), (True, ['--additional-axioms']),
                               (True, ['--translation-cache'])]:
            pair = []
            for optimized in (False, True):
                # Keep path lengths equal: the solver log truncates task file names to fit.
                mode = 'on_' if optimized else 'off'
                directory = root / ('%s-%s-%s' % (refresh, len(extra), mode))
                # The additional-axioms and translation-cache pairs have the same length.
                if extra:
                    directory = root / (extra[0][2:] + '-' + mode)
                args = [source, '--type-system-refresh:' + str(refresh).lower(),
                        '--show-snippets:false', '--use-basename-for-filename',
                        '--verification-time-limit', '60', '--bprint', str(directory / 'program.bpl')] + extra
                if optimized:
                    args += ['--optimize-translation-copy']
                code = window.run(directory, dafny, solver, '.', 1, args, cores=2, smt=True)
                assert code == 4, (code, (directory / 'stdout.txt').read_text(), (directory / 'stderr.txt').read_text())
                controls(directory)
                pair.append(directory)
            differences, obligations, streams = window.compare_one(*pair)
            assert not differences, differences
            assert obligations > 0 and streams > 0, 'empty equality comparison'
            expected, actual = [boogie.modules(str(d / 'program')) for d in pair]
            assert len(expected) > 1 and actual == expected, 'Boogie modules differ or are empty'
            comparisons += 1
        print('%d pairs: identical Boogie, diagnostics, obligations, raw SMT and per-VC resources; Good passes, Bad fails' % comparisons)


if __name__ == '__main__':
    run(*sys.argv[1:])
