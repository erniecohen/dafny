#!/usr/bin/env python3
"""Qualify Java integer-newtype operations with the shared arithmetic oracles."""
import importlib.util
from pathlib import Path
import re

spec = importlib.util.spec_from_file_location('runtime_oracle', Path(__file__).with_name('issue193-runtime-python.py'))
oracle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(oracle)


def inspect_java(output, kind, text):
    if kind == 'boolean':
        assert 'NATIVECOUNT:2:2' in text.splitlines()
    if kind == 'rotation':
        assert 'CARRIER:18446744073709551615:18446744073709551615:1:65536' in text.splitlines()
    files = list(output.with_name(output.name + '-java').rglob('*.java'))
    assert files
    source = '\n'.join(f.read_text() for f in files)
    name = 'Once64' if kind == 'boolean' else 'Once'
    bodies = re.findall(r'public static \S+ ' + name + r'\([^)]*\)\s*\{(.*?)\n\s*}', source, re.S)
    assert len(bodies) == 1, (kind, 'missing single-evaluation function')
    body = bodies[0]
    assert body.count('.Id64(' if kind == 'boolean' else '.Identity(') == 1
    if kind != 'population':
        assert body.count('.Count(') == 1
    assert '.apply(' in body
    assert body.index('.apply(') < body.index('.Id64(' if kind == 'boolean' else '.Identity(')
    assert not re.search(r'IntegerBitwiseModel\.(andNat|orNat|xorNat|notNat|shlNat|shrNat|rolNat|rorNat|popNat)\(', source)


def program(kind, module, widths):
    lines = module.program(widths)
    if kind == 'boolean':
        at = next(i for i, line in enumerate(lines) if line.startswith('method Main()'))
        lines[at] = lines[at].replace('method Main() {', 'method Main() { print "NATIVECOUNT:",BVCount64(1,1),":",LargeCount64(4,1),"\\n";')
    if kind == 'rotation':
        # A 64-bit unsigned carrier observed through the wide BigInteger path.
        at = next(i for i, line in enumerate(lines) if line.startswith('method Main()'))
        lines[at:at] = [
            'newtype {:bitWidth 80} {:nativeType "ulong"} Carrier=x:nat | x<18446744073709551616',
            'function CarrierRotate(a:Carrier,k:int):Carrier requires 0<=k<=80',
            ' requires 0<=IntegerBitwiseModel.rolNat(80,a as int,k)<18446744073709551616 { a.RotateLeft(k) }',
            'method {:isolate_assertions} CarrierCheck() {',
            ' P80(); P64(); P0(); F.LeftRotate(80,18446744073709551615,0); F.LeftRotate(80,18446744073709551615,80);',
            ' P17(); P63(); P16(); F.LeftRotate(80,9223372036854775808,17); F.LeftRotate(80,1,16);',
            ' reveal R.rolNat(); reveal R.norm();',
            ' var a:Carrier:=18446744073709551615;',
            ' var l:=CarrierRotate(a,0); var r:=CarrierRotate(a,80);',
            ' var w:=CarrierRotate(9223372036854775808,17); var q:=CarrierRotate(1,16);',
            ' print "CARRIER:",l,":",r,":",w,":",q,"\\n";',
            ' }']
        at = next(i for i, line in enumerate(lines) if line.startswith('method Main()'))
        lines[at] = lines[at].replace('method Main() {', 'method Main() { CarrierCheck();')
    return lines


if __name__ == '__main__':
    oracle.main(target='java', inspect=inspect_java, program=program)
