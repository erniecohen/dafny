#!/usr/bin/env python3
"""Generate reproducible source cohorts without invoking a compiler or solver."""
import argparse
import hashlib
import json
from pathlib import Path
import tempfile
from fixture_algorithms import depth_fixture, nesting_fixture

ROOT = Path(__file__).resolve().parent
ARMS = ('B', 'N', 'W-erased', 'W-materialized')

def checksum(family, n):
    if family in ('list', 'codatatype-lazy'):
        return n * (n - 1) // 2
    if family in ('tuple', 'generic-boundary'):
        return n * (n + 1) // 2 + 7 * n
    if family == 'refinement-quantified':
        return n * (n + 1) // 2 + 8 * n
    if family in ('arrow-adapter', 'arrow-coercion'):
        return n
    if family == 'ordinal-finite-refinement':
        return n * (n + 1)
    return n * (n + 1) // 2

def flat_depth(arm, depth):
    if arm != 'N':
        return depth_fixture(arm, depth)
    definitions = ''.join('newtype Layer' + str(i) + ' = ' +
                          ('Pair' if i == 0 else 'Layer' + str(i - 1)) + '\n'
                          for i in range(depth))
    value = 'b' + ''.join(' as Layer' + str(i) for i in range(depth))
    return ('type Pair = (int, int)\n' + definitions +
            'lemma RoundTrip(b: Pair) { var v: Layer' + str(depth - 1) +
            ' := ' + value + '; assert (v as Pair) == b; }\n' +
            'lemma ConcreteWitness() { RoundTrip((0, 1)); }\n')

def generate(output, args):
    catalog = json.loads((ROOT / 'templates/catalog.json').read_text())
    for corpus, model in catalog.items():
        rows = []
        models = model['cases']
        ordinary = [c for c in models if not c['id'].startswith(('depth-', 'generic-nesting-'))]
        if corpus == 'fixtures':
            for family, sizes in [('depth', args.depths), ('generic-nesting', args.generic_depths)]:
                for size in sizes:
                    for arm in ARMS:
                        c = dict(next(c for c in models if c['id'].startswith(family + '-') and c['arm'] == arm))
                        filename = 'W' if arm.startswith('W-') else arm
                        c.update(id=f'{family}-{size}/{arm}', family=family, size=size,
                                 path=f'{family}-{size}/{filename}.dfy')
                        ordinary.append(c)
        for original in ordinary:
            c = dict(original)
            if c['id'].startswith('depth-'):
                source = flat_depth(c['arm'], c['size']) if c['size'] >= 1000 else depth_fixture(c['arm'], c['size'])
            elif c['id'].startswith('generic-nesting-'):
                source = nesting_fixture(c['arm'], c['size'])
            else:
                template = (ROOT / 'templates' / corpus / (c['path'] + '.in')).read_text()
                if c['family'] == 'visibility-session':
                    source = ''.join(template.replace('@MODULE_INDEX@', str(i)) for i in range(args.visibility_modules))
                    c['modules'] = args.visibility_modules
                else:
                    source = (template.replace('@WARMUP@', str(args.warmup))
                              .replace('@ITERATIONS@', str(args.iterations))
                              .replace('@TREE_HEIGHT@', str(args.tree_height))
                              .replace('@TREE_LEAVES@', str(2 ** args.tree_height)))
            if c['runtime']:
                c['warmup_iterations'] = args.warmup
                c['measured_iterations'] = args.iterations
                if c['family'] == 'tree-observation':
                    c['warmup_checksum'] = 2 ** args.tree_height * args.warmup
                    c['measured_checksum'] = 2 ** args.tree_height * args.iterations
                else:
                    c['warmup_checksum'] = checksum(c['family'], args.warmup)
                    c['measured_checksum'] = checksum(c['family'], args.iterations)
            c['sha256'] = hashlib.sha256(source.encode()).hexdigest()
            path = output / corpus / c['path']
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(source)
            rows.append(c)
        model.update(cases=rows, unexecuted=True,
                     generator_parameters={'warmup': args.warmup, 'iterations': args.iterations,
                                           'depths': args.depths, 'generic_depths': args.generic_depths,
                                           'visibility_modules': args.visibility_modules,
                                           'tree_height': args.tree_height,
                                           'flat_newtype_depth_threshold': 1000})
        (output / corpus / 'manifest.json').write_text(json.dumps(model, indent=2) + '\n')

def sizes(text):
    values = [int(value) for value in text.split(',')]
    if len(values) != len(set(values)) or not values or any(not 1 <= value <= 1000 for value in values):
        raise argparse.ArgumentTypeError('use unique sizes in 1..1000')
    return values

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true', help='compare regeneration with checked-in sources and manifests')
    parser.add_argument('--warmup', type=int, default=2000)
    parser.add_argument('--iterations', type=int, default=20000)
    parser.add_argument('--depths', type=sizes, default=[1, 10, 100, 1000])
    parser.add_argument('--generic-depths', type=sizes, default=[1, 8, 32])
    parser.add_argument('--visibility-modules', type=int, default=50)
    parser.add_argument('--tree-height', type=int, default=5)
    args = parser.parse_args()
    if not (0 < args.warmup <= args.iterations <= 200000 and 1 <= args.visibility_modules <= 200 and 0 <= args.tree_height <= 10):
        parser.error('require bounded iteration/module/tree sizes')
    if args.check:
        with tempfile.TemporaryDirectory(prefix='issue113-generator-') as directory:
            temporary = Path(directory)
            generate(temporary, args)
            failures = [str(path.relative_to(temporary)) for path in temporary.rglob('*') if path.is_file() and
                        (not (args.output / path.relative_to(temporary)).is_file() or
                         path.read_bytes() != (args.output / path.relative_to(temporary)).read_bytes())]
            if failures:
                parser.exit(1, 'Generated sources differ: ' + ', '.join(failures) + '\n')
        print('All generated sources and manifests match.')
    else:
        generate(args.output, args)

if __name__ == '__main__':
    main()
