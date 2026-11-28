#!/usr/bin/env python3
"""Qualify Go integer-newtype operations with the shared arithmetic oracles."""
import importlib.util
from pathlib import Path
import re

spec = importlib.util.spec_from_file_location('java_oracle', Path(__file__).with_name('issue193-runtime-java.py'))
java = importlib.util.module_from_spec(spec)
spec.loader.exec_module(java)
oracle = java.oracle
program = java.program


def inspect_go(output, kind, text):
    if kind == 'boolean':
        assert 'NATIVECOUNT:2:2' in text.splitlines()
    if kind == 'rotation':
        assert 'CARRIER:18446744073709551615:18446744073709551615:1:65536' in text.splitlines()
    files = list(output.with_name(output.name + '-go').rglob('*.go'))
    assert files
    source = '\n'.join(f.read_text() for f in files)
    name = 'Once64' if kind == 'boolean' else 'Once'
    bodies = re.findall(r'func [^\n]*\b' + name + r'\([^\n]*\) [^{]+\{(.*?)\n}', source, re.S)
    assert len(bodies) == 1, (kind, 'missing single-evaluation function')
    body = bodies[0]
    assert body.count('.Id64(' if kind == 'boolean' else '.Identity(') == 1
    if kind != 'population':
        assert body.count('.Count(') == 1
    assert '})(' in body
    assert body.index('})(') < body.index('.Id64(' if kind == 'boolean' else '.Identity(')
    assert not re.search(r'IntegerBitwiseModel\.(andNat|orNat|xorNat|notNat|shlNat|shrNat|rolNat|rorNat|popNat)\(', source)


if __name__ == '__main__':
    oracle.main(target='go', inspect=inspect_go, program=program)
