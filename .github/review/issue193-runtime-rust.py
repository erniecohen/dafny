#!/usr/bin/env python3
"""Qualify Rust integer-newtype operations with shared arithmetic oracles."""
import importlib.util
from pathlib import Path
import re

spec = importlib.util.spec_from_file_location('java_gate', Path(__file__).with_name('issue193-runtime-java.py'))
java = importlib.util.module_from_spec(spec)
spec.loader.exec_module(java)
oracle = java.oracle


def program(kind, module, widths):
    lines = java.program(kind, module, widths)
    # Let Rust select unsigned 128-bit storage. Other widths retain the shared
    # carrier matrix, including arbitrary-precision and narrow native words.
    lines = [s.replace('{:nativeType false} U128', 'U128') for s in lines]
    if kind == 'boolean':
        at = next(i for i, line in enumerate(lines) if line.startswith('method Main()'))
        declarations = []
        for n, carrier in ((8, '"byte"'), (80, 'false')):
            declarations += [
                f'newtype {{:rust_erased false}} {{:bitWidth {n}}} {{:nativeType {carrier}}} Wrapped{n}=x:nat | x<{2**n}',
                f'function WrappedAnd{n}(a:Wrapped{n},b:Wrapped{n}):Wrapped{n}',
                f'{{ P{n}(); F.And({n},a as int,b as int); R.AndRange({n},a as int,b as int); a & b }}']
        lines[at:at] = declarations + [
            'module LocalTypes { type Natural = bv8 function Id(a:Natural):Natural { a } }',
            'module integer_bitwise { function andNat(a:int,b:int):int { 0 } }']
        at = next(i for i, line in enumerate(lines) if line.startswith('method Main()'))
        lines[at] = lines[at].replace('method Main() {', 'method Main() { print "ALIAS:",LocalTypes.Id(255),"\\n"; print "WRAPPED:",WrappedAnd8(5,3),":",WrappedAnd80(5,3),"\\n";')
    return lines


def inspect_rust(output, kind, text):
    if kind == 'boolean':
        assert 'NATIVECOUNT:2:2' in text.splitlines()
        assert 'ALIAS:255' in text.splitlines()
        assert 'WRAPPED:1:1' in text.splitlines()
    if kind == 'rotation':
        assert 'CARRIER:18446744073709551615:18446744073709551615:1:65536' in text.splitlines()
    files = list(output.with_name(output.name + '-rust').joinpath('src').glob('*.rs'))
    assert files
    source = '\n'.join(f.read_text() for f in files)
    for function in ('And128', 'RotateLeft128', 'Population128'):
        if 'pub fn ' + function + '(' in source:
            assert re.search(r'pub fn ' + function + r'\(a: u128[,)]', source)
    name = 'Once64' if kind == 'boolean' else 'Once'
    body = re.search(r'pub fn ' + name + r'\([^\n]*\).*?\{(.*?)\n    }', source, re.S).group(1)
    assert body.count('::Id64(' if kind == 'boolean' else '::Identity(') == 1
    if kind != 'population':
        assert body.count('::Count(') == 1
        assert body.index('::Id64(' if kind == 'boolean' else '::Identity(') < body.index('::Count(')
    assert '::dafny_runtime::integer_bitwise' in source
    assert 'integer_bitwise::' in body
    assert not re.search(r'IntegerBitwiseModel::(andNat|orNat|xorNat|notNat|shlNat|shrNat|rolNat|rorNat|popNat)\(', source)


if __name__ == '__main__':
    oracle.main(target='rs', inspect=inspect_rust, program=program,
                target_options=('--enforce-determinism',), verification_command=('translate', 'rs'))
