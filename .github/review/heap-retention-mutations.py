#!/usr/bin/env python3
"""Mutate each retention-analysis boundary serially and require its intended failure."""
import argparse
import importlib.util
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('heap_retention', Path(__file__).with_name('heap-retention.py'))
launcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(launcher)

MUTATIONS = [
    ('reader-sampling', 'CheckSampling(dump.AverageCountMultiplier, dump.AverageSizeMultiplier, dump.CountMultipliersByType);',
     '', 'control: accepted sampled file'),
    ('count-sampling', 'count == 1 && size == 1', 'size == 1', 'control: accepted count sampling'),
    ('type-sampling', '(byType == null || byType.All(x => x == 1))', 'true', 'control: accepted per-type sampling'),
    ('cut-traversal', '!blocked.Contains(index) && !seen[index]', '!seen[index]', 'inconsistent graph cut'),
    ('byte-accounting', 'sum + graph.GetNode((NodeIndex)i, storage).Size',
     'sum + 2 * graph.GetNode((NodeIndex)i, storage).Size', 'control: rooted total'),
    ('shared-reachability', 'var retained = RootedBytes - Bytes(remaining);',
     'var retained = reachable;', 'control: shared cycle cut'),
    ('empty-selection', 'Require(selected.Count > 0, "no rooted nodes selected; measurement is vacuous");',
     '', 'control: accepted empty selector'),
    ('unrooted-selection', 'Require(selected.All(i => i >= 0 && i < baseline.Length && baseline[i]), "selected node is not rooted");',
     '', 'inconsistent graph cut'),
    ('synthetic-root', 'Require(!selected.Contains((int)graph.RootIndex), "cannot cut the synthetic graph root");',
     '', 'control: accepted graph root'),
    ('incomplete-graph', 'Require(graph.GetAddress((NodeIndex)id) == 0, "undefined non-null node; graph is incomplete");',
     '', 'control: accepted incomplete graph'),
    ('collective-cut', 'return Measure(selected);', 'return Measure([selected.First()]);', 'control: collective cut'),
    ('null-target', 'graph.GetAddress((NodeIndex)id) == 0', 'false', 'undefined non-null node'),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', required=True, type=Path)
    args = parser.parse_args()
    work = args.work.resolve()
    if not work.is_relative_to(ROOT / 'work'):
        parser.error('--work must be inside this checkout\'s work/')
    work.mkdir(parents=True, exist_ok=False)
    original = ROOT / '.github/review/heap-retention'
    source = (original / 'Program.cs').read_text()
    for name, old, new, diagnostic in MUTATIONS:
        if source.count(old) != 1:
            raise ValueError('ambiguous or stale mutation: ' + name)
        stage = work / name
        stage.mkdir()
        project = stage / 'HeapRetention.csproj'
        shutil.copyfile(original / project.name, project)
        (stage / 'Program.cs').write_text(source.replace(old, new))
        tool, env = launcher.build(project, stage)
        result = subprocess.run(['dotnet', str(tool), '--self-test'], env=env, capture_output=True,
                                text=True, timeout=110)
        output = result.stdout + result.stderr
        (stage / 'control.log').write_text(output)
        if result.returncode == 0 or diagnostic not in output:
            raise AssertionError('intended control did not kill mutation: ' + name)
        print(name + ': killed by ' + diagnostic, flush=True)
        shutil.rmtree(stage / 'build')
    print(str(len(MUTATIONS)) + ' mutations killed')


if __name__ == '__main__':
    main()
