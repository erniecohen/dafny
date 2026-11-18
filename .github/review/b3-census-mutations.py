#!/usr/bin/env python3
"""Run census mutations serially; require the intended control failure after a clean build."""
import argparse
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MUTATIONS = [
    ('where', 'Add("where." + node.GetType().Name);', '', 'missing feature: where.Formal'),
    ('free-requires', 'req.Free ?', 'false ?', 'missing feature: contract.free-requires'),
    ('free-ensures', 'ens.Free ?', 'false ?', 'missing feature: contract.free-ensures'),
    ('structured', 'Structured(node.StructuredStmts);', '', 'missing feature: control.IfCmd'),
    ('attributes', 'VisitQKeyValue(carrier.Attributes);', '', 'missing feature: attribute.census_control'),
    ('operators', 'Add("operator." + call.Fun.GetType().Name + (call.Fun is B.FunctionCall ? "" : "." + call.Fun.FunctionName));', '', 'missing feature: operator.MapSelect.MapSelect'),
    ('hideable', 'axiom.CanHide', 'false', 'missing feature: visibility.hideable-axiom'),
    ('typing', 'program.Typecheck(options) != 0', 'false', 'ill-typed input was accepted'),
    ('old', 'Add("node." + node.GetType().Name);', 'if (node is not B.OldExpr) { Add("node." + node.GetType().Name); }', 'missing feature: node.OldExpr'),
    ('attribute-values', 'Add("attribute-value." + node.Key + "." + string.Join(",", node.Params));', '', 'missing feature: attribute-value.subsumption.0'),
    ('builtin', 'Add("builtin." + string.Join(",", node.Params));', '', 'missing feature: builtin.bvadd'),
    ('polymorphism', 'fun.TypeParameters.Count > 0', 'false', 'missing feature: polymorphic.function'),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', required=True, type=Path)
    parser.add_argument('--binaries', type=Path, default=ROOT / 'Binaries/net10.0')
    args = parser.parse_args()
    work = args.work.resolve()
    if not work.is_relative_to(ROOT / 'work'):
        parser.error('--work must be inside this checkout\'s work directory')
    original = ROOT / '.github/review/b3-census'
    source = (original / 'Program.cs').read_text()
    for name, old, new, diagnostic in MUTATIONS:
        if source.count(old) != 1:
            raise ValueError('mutation is ambiguous or stale: ' + name)
        stage = work / name
        stage.mkdir(parents=True, exist_ok=False)
        for file in ['Census.csproj', 'control.bpl']:
            shutil.copyfile(original / file, stage / file)
        (stage / 'Program.cs').write_text(source.replace(old, new))
        with (stage / 'build.log').open('w') as log:
            subprocess.run(['dotnet', 'build', str(stage / 'Census.csproj'), '--artifacts-path',
                            str(stage / 'build'), '-p:DafnyBinaries=' + str(args.binaries.resolve())],
                           cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
        result = subprocess.run(['dotnet', str(stage / 'build/bin/Census/debug/Census.dll'), '--self-test'],
                                cwd=ROOT, capture_output=True, text=True)
        (stage / 'control.log').write_text(result.stdout + result.stderr)
        if result.returncode == 0 or diagnostic not in result.stdout + result.stderr:
            raise AssertionError('intended control did not kill mutation: ' + name)
        print(name + ': killed by ' + diagnostic, flush=True)
        # Retain logs/source, discard the redundant copied dependencies after each site.
        shutil.rmtree(stage / 'build')
    print(str(len(MUTATIONS)) + ' mutations killed')


if __name__ == '__main__':
    main()
