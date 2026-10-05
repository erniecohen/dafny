#!/usr/bin/env python3
# Identity observation only; this helper never runs a compiler or solver.
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
SPEC = json.loads((HERE / 'issue113-benchmark-source-identity.json').read_text())
MANIFEST = json.loads((HERE / 'issue113-benchmark-product-inputs.json').read_text())

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()

def portable(files):
    return hashlib.sha256(''.join(h + '  ' + p + '\n' for p, h in sorted(files.items())).encode()).hexdigest()

def inputs():
    exclusions = SPEC['build_input_recipe']['exclusions']
    names = git('ls-files', '-z').split('\0')
    files = {p: digest(ROOT / p) for p in names if p and not any(
        p.startswith(x) if x.endswith('/') else p == x for x in exclusions)}
    if len(files) != SPEC['required_build_input_count']:
        raise ValueError('Product input denominator changed')
    if files != MANIFEST['files'] or portable(files) != SPEC['required_build_inputs_sha256']:
        raise ValueError('Actual portable product inputs changed')
    actual_tree = git('rev-parse', 'HEAD:Source')
    if actual_tree != SPEC['source_tree']:
        raise ValueError('Actual Source tree differs from pinned product')
    for p, expected in SPEC['version_inputs_sha256'].items():
        if digest(ROOT / p) != expected:
            raise ValueError('Version input changed: ' + p)
    for p, expected in SPEC['benchmark_files_sha256'].items():
        if digest(ROOT / p) != expected:
            raise ValueError('Benchmark input/helper changed: ' + p)
    benchmark_root = ROOT / '.github/review/issue113-benchmarks'
    actual_names = {p.relative_to(ROOT).as_posix() for p in benchmark_root.rglob('*') if p.is_file()
                    and '__pycache__' not in p.parts}
    if actual_names != set(SPEC['benchmark_files_sha256']):
        raise ValueError('Benchmark input/helper denominator changed')
    # The complete pinned byte manifest is sufficient before a shallow checkout
    # has fetched the product commit. The existing version.sh subsequently fetches
    # that object and enforces its authoritative post-product diff policy.
    return {'scratch_source_sha': git('rev-parse', 'HEAD'),
            'product_commit': SPEC['product_commit'], 'source_tree': actual_tree,
            'product_input_count': len(files), 'portable_build_inputs_sha256': portable(files),
            'product_files_sha256': files, 'build_input_recipe': SPEC['build_input_recipe'],
            'benchmark_template_source': SPEC['benchmark_template_source'],
            'benchmark_file_count': len(SPEC['benchmark_files_sha256']),
            'benchmark_files_sha256': SPEC['benchmark_files_sha256'],
            'version_inputs_sha256': SPEC['version_inputs_sha256']}

def version(path):
    values = dict(line.split('=', 1) for line in Path(path).read_text().splitlines() if '=' in line)
    if values.get('revision') != SPEC['expected_source_revision_id']:
        raise ValueError('Actual product-diff SourceRevisionId changed')
    if values.get('version') != SPEC['expected_version']:
        raise ValueError('Actual product-diff version changed')
    return values

def published(root):
    root = Path(root)
    files = {p.relative_to(root).as_posix(): digest(p) for p in sorted(root.rglob('*')) if p.is_file()}
    critical = ['DafnyCore.dll', 'DafnyDriver.dll', 'Dafny.dll', 'Dafny.deps.json',
                'Dafny.runtimeconfig.json', 'Dafny']
    if any(p not in files for p in critical):
        raise ValueError('Missing actual published compiler component')
    return files

def bundle(args):
    receipt = inputs()
    version_values = version(args.version_env)
    observed_version = Path(args.version_output).read_text().strip()
    if observed_version != version_values['version']:
        raise ValueError('Actual compiler version differs from product-diff revision')
    original = json.loads(Path(args.components).read_text())
    if original['source_sha'] != receipt['scratch_source_sha'] or original['last_product_revision'] != SPEC['product_commit']:
        raise ValueError('Original component collector source/product identity mismatch')
    archive = digest(args.archive)
    if original['archive_sha256'] != archive:
        raise ValueError('Actual compiler archive differs from component collector')
    files = published(args.bundle_root)
    if any(files.get(p) != h for p, h in original['components_sha256'].items()):
        raise ValueError('Actual original compiler component map mismatch')
    receipt.update({'SourceRevisionId': version_values['revision'], 'actual_version': observed_version,
                    'archive_sha256': archive, 'all_published_files_sha256': files,
                    'original_components_sha256': original['components_sha256'],
                    'boundary': 'All compiler files are actual byte hashes; source and compiler hashes are separate identities. No crossplatform DLL equality is assumed.'})
    return receipt

def verify_bundle(args):
    current = inputs()
    receipt = json.loads(Path(args.receipt).read_text())
    for key in ['scratch_source_sha', 'product_commit', 'source_tree', 'portable_build_inputs_sha256',
                'product_files_sha256', 'benchmark_files_sha256']:
        if current[key] != receipt[key]:
            raise ValueError('Measurement checkout identity differs: ' + key)
    if receipt['actual_version'] != SPEC['expected_version'] or receipt['SourceRevisionId'] != SPEC['expected_source_revision_id']:
        raise ValueError('Measurement compiler version binding differs')
    if digest(args.archive) != receipt['archive_sha256']:
        raise ValueError('Measurement compiler archive changed')
    if published(args.bundle_root) != receipt['all_published_files_sha256']:
        raise ValueError('Measurement unpacked actual compiler components changed')
    return {'source_and_components_match': True, 'product_commit': receipt['product_commit'],
            'actual_version': receipt['actual_version'],
            'portable_build_inputs_sha256': receipt['portable_build_inputs_sha256'],
            'archive_sha256': receipt['archive_sha256'],
            'published_file_count': len(receipt['all_published_files_sha256'])}

def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='command', required=True)
    for name in ['inputs', 'version', 'bundle', 'verify-bundle']:
        p = sub.add_parser(name)
        p.add_argument('--output', required=True)
        if name == 'version':
            p.add_argument('--version-env', required=True)
        if name == 'bundle':
            for flag in ['version-env', 'version-output', 'components', 'archive', 'bundle-root']:
                p.add_argument('--' + flag, required=True)
        if name == 'verify-bundle':
            for flag in ['receipt', 'archive', 'bundle-root']:
                p.add_argument('--' + flag, required=True)
    args = parser.parse_args()
    result = inputs() if args.command == 'inputs' else version(args.version_env) if args.command == 'version' else bundle(args) if args.command == 'bundle' else verify_bundle(args)
    target = Path(args.output)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')

if __name__ == '__main__':
    main()
