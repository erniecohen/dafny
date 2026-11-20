#!/usr/bin/env python3
"""Validate the selected upstream snapshot, patch inventory and trust policy."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
TRUST = re.compile(r'^\s*(?:assume\b|@Axiom\b|@AssumeCrossModuleTermination\b)|\{:\s*(?:axiom\b|verify\s+false\b|termination\s+false\b)|\bdecreases\s+\*', re.M)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate(upstream=None):
    manifest = json.loads((ROOT / 'UPSTREAM.json').read_text())
    entries = manifest['files']
    paths = [entry['path'] for entry in entries]
    assert len(paths) == len(set(paths)) and entries, 'empty or duplicate provenance'
    changed = set()
    for entry in entries:
        assert sha(ROOT / entry['path']) == entry['sha256'], entry['path']
        if entry['sha256'] != entry['upstream_sha256']:
            changed.add(entry['path'])
        if upstream:
            assert sha(upstream / entry['upstream_path']) == entry['upstream_sha256'], entry['upstream_path']
    assert changed == {patch['path'] for patch in manifest['patches']}, 'incomplete patch inventory'
    for source in (ROOT / 'src').rglob('*.dfy'):
        assert not TRUST.search(source.read_text()), 'trusted or unchecked premise: ' + str(source.relative_to(ROOT))
    if upstream:
        revision = subprocess.check_output(['git', '-C', str(upstream), 'rev-parse', 'HEAD'], text=True).strip()
        assert revision == manifest['commit'], 'wrong upstream revision'
        selected = set()
        for item in manifest['selected_paths']:
            path = upstream / item
            selected.update(p.relative_to(upstream).as_posix() for p in path.rglob('*') if p.is_file()) if path.is_dir() else selected.add(item)
        assert selected == {entry['upstream_path'] for entry in entries}, 'incomplete selected upstream tree'
    print(f"{len(entries)} copied files; {len(changed)} patches; no unchecked vendored premises")


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--upstream', type=Path)
    validate(parser.parse_args().upstream)
