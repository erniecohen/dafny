#!/usr/bin/env python3
"""Generate design-only #193 bridge clients; never a product implementation."""
import argparse, json
from pathlib import Path

OPS = ['and','or','xor','and_not','not','shl','shr','add','sub','mul','div','mod','bit','eq','le','rotl','rotr']
PREFIX = '''// #193 DESIGN PROBE. Candidate bridge contracts are explicitly TRUSTED.
// This tests client cost, not verification of the bridge or implementation.
ghost function p(w: nat): nat
  ensures 1 <= p(w)
  decreases w
{ if w == 0 then 1 else 2 * p(w - 1) }

ghost function andNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (a % 2) * (b % 2) + 2 * andNat(w - 1, a / 2, b / 2) }

ghost function orNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 == 1 || b % 2 == 1 then 1 else 0) + 2 * orNat(w - 1, a / 2, b / 2) }

ghost function xorNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 != b % 2 then 1 else 0) + 2 * xorNat(w - 1, a / 2, b / 2) }

ghost function andNotNat(w: nat, a: nat, b: nat): nat
  decreases w
{ if w == 0 then 0 else (if a % 2 == 1 && b % 2 == 0 then 1 else 0) + 2 * andNotNat(w - 1, a / 2, b / 2) }

'''

def formula(op,w,a,b,k,native=False):
    m=2**w; t=f'bv{w}'
    aa=a if native else f'({a} as {t})'
    bb=b if native else f'({b} as {t})'
    ai=f'({a} as int)' if native else a
    bi=f'({b} as int)' if native else b
    kk=k
    bvops={'and':'&','or':'|','xor':'^','add':'+','sub':'-','mul':'*','div':'/','mod':'%'}
    if op in bvops: lhs=f'(({aa} {bvops[op]} {bb}) as int)'
    elif op=='and_not': lhs=f'(({aa} & !{bb}) as int)'
    elif op=='not': lhs=f'((!{aa}) as int)'
    elif op=='shl': lhs=f'(({aa} << {kk}) as int)'
    elif op=='shr': lhs=f'(({aa} >> {kk}) as int)'
    elif op=='bit': lhs=f'((({aa} >> {kk}) & (1 as {t})) == (1 as {t}))'
    elif op=='eq': lhs=f'({aa} == {bb})'
    elif op=='le': lhs=f'({aa} <= {bb})'
    elif op=='rotl': lhs=f'(({aa}.RotateLeft({k})) as int)'
    elif op=='rotr': lhs=f'(({aa}.RotateRight({k})) as int)'
    rhs={
      'and':f'andNat({w}, {ai}, {bi})',
      'or':f'orNat({w}, {ai}, {bi})',
      'xor':f'xorNat({w}, {ai}, {bi})',
      'and_not':f'andNotNat({w}, {ai}, {bi})',
      'not':f'{m-1} - {ai}',
      'shl':f'({ai} * p({k})) % {m}',
      'shr':f'{ai} / p({k})',
      'add':f'({ai} + {bi}) % {m}',
      'sub':f'({ai} - {bi}) % {m}',
      'mul':f'({ai} * {bi}) % {m}',
      'div':f'{ai} / {bi}',
      'mod':f'{ai} % {bi}',
      'bit':f'({ai} / p({k}) % 2 == 1)',
      'eq':f'({ai} == {bi})',
      'le':f'({ai} <= {bi})',
      'rotl':f'(({ai} * p({k})) % {m}) + {ai} / p({w} - {k})',
      'rotr':f'{ai} / p({k}) + (({ai} * p({w} - {k})) % {m})',
    }[op]
    return f'{lhs} == ({rhs})'

def pre(op,w,native=False):
    out=[] if native else [f'  requires a < {2**w} && b < {2**w}']
    if op in ['div','mod']: out.append('  requires b != 0')
    if op in ['shl','shr','rotl','rotr']: out.append(f'  requires k <= {w}')
    if op=='bit': out.append(f'  requires k < {w}')
    return '\n'.join(out)

def build(w,mode):
    text=PREFIX
    t=f'bv{w}'
    for op in OPS:
        if mode in ['fused','fused_vacuity']:
            text+=f'lemma {{:axiom}} bridge_{op}(a: nat, b: nat, k: nat)\n{pre(op,w)}\n  ensures {formula(op,w,"a","b","k")}\n\n'
        if mode in ['candidate','vacuity']:
            text+=f'lemma {{:axiom}} bridge_{op}(a: {t}, b: {t}, k: nat)\n{pre(op,w,True)}\n  ensures {formula(op,w,"a","b","k",True)}\n\n'
        if mode in ['vacuity','fused_vacuity']:
            actual=f'5 as {t}, 3 as {t}, 1' if mode=='vacuity' else '5, 3, 1'
            text+=f'lemma {{:induction false}} control_{op}()\n{{\n  bridge_{op}({actual});\n  assert false;\n}}\n\n'
            continue
        text+=f'lemma {{:induction false}} client_{op}(a: nat, b: nat, k: nat)\n{pre(op,w)}\n  ensures {formula(op,w,"a","b","k")}\n{{\n'
        if mode=='assume_false': text+='  assume false;\n'
        if mode=='candidate': text+=f'  bridge_{op}(a as {t}, b as {t}, k);\n'
        if mode=='fused': text+='  bridge_'+op+'(a, b, k);\n'
        text+='}\n\n'
    return text

def main():
    ap=argparse.ArgumentParser();ap.add_argument('output');a=ap.parse_args()
    out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
    manifest=[]
    for w in [8,16,32,64]:
        for mode in ['assume_false','native','candidate','vacuity','fused','fused_vacuity']:
            name=f'w{w}-{mode}.dfy';(out/name).write_text(build(w,mode))
            manifest.append({'file':name,'width':w,'mode':mode,'operations':OPS})
        t=f'bv{w}'
        helper='// Pure native-bitvector identities: no new axiom contracts.\n'
        helper+=f'lemma {{:induction false}} client_or(a: {t}, b: {t}) ensures (a | b) == !(!a & !b) {{}}\n'
        helper+=f'lemma {{:induction false}} client_xor(a: {t}, b: {t}) ensures (a ^ b) == ((a | b) & !(a & b)) {{}}\n'
        helper+=f'lemma {{:induction false}} client_and_not(a: {t}, b: {t}) ensures (a & !b) == ((a | b) ^ b) {{}}\n'
        name=f'w{w}-native_helpers.dfy';(out/name).write_text(helper)
        manifest.append({'file':name,'width':w,'mode':'native_helpers','operations':['or','xor','and_not']})
        integer=PREFIX
        for op in OPS:
            rhs=formula(op,w,'a','b','k').rsplit(' == (',1)[1][:-1]
            result='bool' if op in ['bit','eq','le'] else 'int'
            integer+=f'ghost function ref_{op}(a: nat, b: nat, k: nat): {result}\n{pre(op,w)}\n{{ {rhs} }}\n\n'
            integer+=f'lemma {{:induction false}} client_{op}(a: nat, b: nat, k: nat)\n{pre(op,w)}\n  ensures ref_{op}(a,b,k) == ({rhs})\n{{}}\n\n'
        name=f'w{w}-int_reference.dfy';(out/name).write_text(integer)
        manifest.append({'file':name,'width':w,'mode':'int_reference','operations':OPS})
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')

if __name__=='__main__':main()
