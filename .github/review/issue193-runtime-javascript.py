#!/usr/bin/env python3
"""Qualify JavaScript integer-newtype operations with shared arithmetic oracles."""
import importlib.util
from pathlib import Path
import re

spec = importlib.util.spec_from_file_location('java_gate', Path(__file__).with_name('issue193-runtime-java.py'))
java = importlib.util.module_from_spec(spec)
spec.loader.exec_module(java)


def program(kind, module, widths):
    # Explicit C#/Java native hints are not JavaScript storage names. Native
    # selection chooses Number only when the entire predicate fits its safe range.
    lines = [re.sub(r'\{\:nativeType "[^"]+"\} ?', '', s)
             for s in java.program(kind, module, widths)]
    if kind == 'boolean':
        at = next(i for i, s in enumerate(lines) if s.startswith('method Main()'))
        declarations = []
        for n in (53, 54):
            declarations += [f'newtype {{:bitWidth {n}}} Precision{n}=x:nat | x<{2**n}',
                             f'function PrecisionXor{n}(a:Precision{n},b:Precision{n}):Precision{n}',
                             f'{{ P{n}(); F.Xor({n},a as int,b as int); R.BooleanRange({n},a as int,b as int); a ^ b }}']
        lines[at:at] = declarations
        at = next(i for i, s in enumerate(lines) if s.startswith('method Main()'))
        lines[at] = lines[at].replace('method Main() {', 'method Main() { print "PRECISION:",PrecisionXor53(9007199254740991,1),":",PrecisionXor54(18014398509481983,1),"\\n";')
    return lines


def inspect_javascript(output, kind, text):
    if kind == 'boolean':
        assert 'NATIVECOUNT:2:2' in text.splitlines()
        assert 'PRECISION:9007199254740990:18014398509481982' in text.splitlines()
    if kind == 'rotation':
        assert 'CARRIER:18446744073709551615:18446744073709551615:1:65536' in text.splitlines()
    source = output.with_suffix('.js').read_text()
    name = 'Once64' if kind == 'boolean' else 'Once'
    body = re.search(r'static ' + name + r'\([^)]*\)\s*\{(.*?)\n    }', source, re.S).group(1)
    assert body.count('.Id64(' if kind == 'boolean' else '.Identity(') == 1
    if kind != 'population':
        assert body.count('.Count(') == 1
    assert '=>' in body
    assert body.index('=>') < body.index('.Id64(' if kind == 'boolean' else '.Identity(')
    assert 'BigInt(' in body
    assert not re.search(r'IntegerBitwiseModel\.(andNat|orNat|xorNat|notNat|shlNat|shrNat|rolNat|rorNat|popNat)\(', source)


if __name__ == '__main__':
    java.oracle.main(target='js', inspect=inspect_javascript, program=program)
