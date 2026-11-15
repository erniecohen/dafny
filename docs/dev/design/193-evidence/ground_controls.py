#!/usr/bin/env python3
"""Finite ground witnesses for #193 bridges; complements, never replaces, Dafny controls.

Recursive integer references are evaluated at concrete arguments. No quantified
definition is weakened or hidden: the instance is checked after that evaluation.
The native rotation expression uses shifts/or even on power-of-two widths.
"""
import argparse, csv, hashlib, json, re, subprocess
from pathlib import Path
from generate import OPS

def band(w,a,b,op):
    if not w:return 0
    x,y=a%2,b%2
    z={'and':x*y,'or':int(x==1 or y==1),'xor':int(x!=y),'and_not':int(x==1 and y==0)}[op]
    return z+2*band(w-1,a//2,b//2,op)

def value(op,w,a,b,k):
    m=2**w
    if op in ['and','or','xor','and_not']:return band(w,a,b,op)
    return {'not':m-1-a,'shl':a*2**k%m,'shr':a//2**k,
      'add':(a+b)%m,'sub':(a-b)%m,'mul':a*b%m,'div':a//b,'mod':a%b,
      'bit':(a//2**k)%2==1,'eq':a==b,'le':a<=b,
      'rotl':a*2**k%m+a//2**(w-k),'rotr':a//2**k+a*2**(w-k)%m}[op]

def expr(op,w,a,b,k):
    x=f'(_ bv{a} {w})';y=f'(_ bv{b} {w})';n=f'(_ bv{k} {w})';q=f'(_ bv{w-k} {w})'
    bins={'and':'bvand','or':'bvor','xor':'bvxor','shl':'bvshl','shr':'bvlshr',
      'add':'bvadd','sub':'bvsub','mul':'bvmul','div':'bvudiv','mod':'bvurem','eq':'=','le':'bvule'}
    if op in bins:z=f'({bins[op]} {x} {n if op in ["shl","shr"] else y})'
    elif op=='and_not':z=f'(bvand {x} (bvnot {y}))'
    elif op=='not':z=f'(bvnot {x})'
    elif op=='bit':z=f'(= (bvand (bvlshr {x} {n}) (_ bv1 {w})) (_ bv1 {w}))'
    elif op=='rotl':z=f'(bvor (bvshl {x} {n}) (bvlshr {x} {q}))'
    elif op=='rotr':z=f'(bvor (bvlshr {x} {n}) (bvshl {x} {q}))'
    return z if op in ['bit','eq','le'] else f'(bv2int {z})'

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--z3',required=True);ap.add_argument('--output',required=True);a=ap.parse_args()
    out=Path(a.output);out.mkdir(parents=True,exist_ok=True);rows=[]
    jobs=[(op,w,5,3,1,'regular') for w in [8,16,32,64] for op in OPS]
    jobs +=[(op,w,1,1,k,'boundary') for w in [1,3,8,64] for k in [0,w] for op in ['shl','shr','rotl','rotr']]
    for op,w,x,y,k,kind in jobs:
        expected=value(op,w,x,y,k);rhs=str(expected).lower() if isinstance(expected,bool) else str(expected)
        law=f'(= {expr(op,w,x,y,k)} {rhs})'
        key=f'{kind}-{op}-w{w}-k{k}'
        query=f'(set-option :produce-models true)\n(set-option :rlimit 200000)\n(assert {law})\n(assert (not false))\n(check-sat)\n(get-info :all-statistics)\n'
        path=out/(key+'.smt2');path.write_text(query)
        r=subprocess.run([a.z3,'-smt2',str(path)],text=True,capture_output=True,timeout=15)
        (out/(key+'.txt')).write_text(r.stdout+r.stderr)
        statuses=re.findall(r'^(sat|unsat|unknown)$',r.stdout,re.M);ru=re.search(r':rlimit-count\s+(\d+)',r.stdout)
        rows.append({'kind':kind,'op':op,'w':w,'a':x,'b':y,'k':k,'expected_result':rhs,'smt_status':statuses[-1] if statuses else 'error','ru':int(ru[1]) if ru else None,'sha256':hashlib.sha256(query.encode()).hexdigest()})
    (out/'results.json').write_text(json.dumps(rows,indent=2)+'\n')
    with (out/'results.csv').open('w') as f:
        wr=csv.DictWriter(f,fieldnames=list(rows[0]));wr.writeheader();wr.writerows(rows)
    print(json.dumps({'cases':len(rows),'sat':sum(r['smt_status']=='sat' for r in rows),'other':[r for r in rows if r['smt_status']!='sat']},indent=2))
    # Probe: report, don't fail a public diagnostic workflow for expected failures.

if __name__=='__main__':main()
