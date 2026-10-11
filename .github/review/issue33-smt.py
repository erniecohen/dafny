#!/usr/bin/env python3
"""Audit and exercise the bounded axioms extracted from the actual generated SMT."""
import argparse
import json
from pathlib import Path
import re
import subprocess

p=argparse.ArgumentParser();p.add_argument('--smt',required=True);p.add_argument('--z3',required=True);p.add_argument('--output',required=True)
a=p.parse_args();out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
text=Path(a.smt).read_text()
# Scan balanced top-level commands, preserving their actual solver syntax.
commands=[];start=None;depth=0;quoted=False;bar=False;comment=False;escape=False
for i,c in enumerate(text):
    if comment:
        if c=='\n':comment=False
        continue
    if quoted:
        if c=='"' and not escape:quoted=False
        escape=c=='\\' and not escape
        continue
    if bar:
        if c=='|':bar=False
        continue
    if c==';':comment=True;continue
    if c=='"':quoted=True;continue
    if c=='|':bar=True;continue
    if c=='(':
        if depth==0:start=i
        depth+=1
    elif c==')':
        depth-=1
        if depth==0 and start is not None:commands.append(text[start:i+1])

families={}
for cmd in commands:
    if not cmd.startswith('(assert (forall') or ':pattern' not in cmd:continue
    match=re.search(r'\(= \((\S+) \(\(_ int2bv (\d+)\) (\S+)\)\) \3\)',cmd)
    if not match:continue
    wrapper,width,x=match.groups();width=int(width)
    if width not in [32,64] or width in families:continue
    bound=str(1<<width)
    assert re.search(r'\(\('+re.escape(x)+r' Int\)',cmd), 'non-native integer binder'
    assert f'(<= 0 {x})' in cmd and f'(< {x} {bound})' in cmd
    assert ':pattern ( ('+wrapper+' ((_ int2bv '+str(width)+') '+x+')))' in cmd
    bridges=[c for c in commands if c.startswith('(assert (forall') and 'bv2int' in c and '('+wrapper+' ' in c and '(_ BitVec '+str(width)+')' in c]
    assert bridges, 'missing existing wrapper bridge'
    rename=lambda value:re.sub(re.escape(wrapper)+r'(?![\w@])','roundTrip'+str(width),value)
    families[width]=(rename(cmd),rename(bridges[0]),x)
assert set(families)=={32,64}
common='(set-logic ALL)\n(set-option :smt.qi.profile true)\n'
model_common=common
for width,(axiom,bridge,x) in families.items():
    common+=f'(declare-fun roundTrip{width} ((_ BitVec {width})) Int)\n'+bridge+'\n'+axiom+'\n'
    model_common+=f'(define-fun roundTrip{width} ((b (_ BitVec {width}))) Int (bv2int b))\n'+bridge+'\n'+axiom+'\n'
report=[]
for width,(axiom,bridge,x) in families.items():
    bound=1<<width;term=lambda v:f'(roundTrip{width} ((_ int2bv {width}) {v}))'
    checks=[('bounded','unsat',f'(declare-const a Int)\n(assert (<= 0 a))\n(assert (< a {bound}))\n(assert (not (= {term("a")} a)))'),('zero','sat',f'(assert (= {term("0")} 0))'),('high-bit','sat',f'(assert (= {term(str(bound//2))} {bound//2}))'),('negative','sat',f'(assert (= {term("(- 1)")} {bound-1}))'),('upper','sat',f'(assert (= {term(str(bound))} 0))')]
    for label,expected,body in checks:
        filename=out/f'bv{width}-{label}.smt2';filename.write_text((model_common if expected == 'sat' else common)+body+'\n(check-sat)\n')
        result=subprocess.run([a.z3,'-smt2',str(filename)],capture_output=True,text=True,timeout=60)
        (filename.with_suffix('.output')).write_text(result.stdout+result.stderr)
        actual=result.stdout.strip();report.append([width,label,expected,actual])
        assert result.returncode==0 and actual==expected,(label,actual)
    for label,old,new,body in [('remove-lower',f'(<= 0 {x})','true',f'(assert (= {term("(- 1)")} {bound-1}))'),('inclusive-upper',f'(< {x} {bound})',f'(<= {x} {bound})',f'(assert (= {term(str(bound))} 0))')]:
        changed=common.replace(axiom,axiom.replace(old,new))
        mutated = axiom.replace(old, new)
        quantified_body = mutated.split('(!', 1)[1].split(':pattern', 1)[0].strip()
        value = '(- 1)' if label == 'remove-lower' else str(bound)
        ground = re.sub(re.escape(x) + r'(?![\w@])', value, quantified_body)
        filename=out/f'bv{width}-mutation-{label}.smt2';filename.write_text(changed+body+'\n(assert '+ground+')\n(check-sat)\n')
        result=subprocess.run([a.z3,'-smt2',str(filename)],capture_output=True,text=True,timeout=60)
        (filename.with_suffix('.output')).write_text(result.stdout+result.stderr)
        report.append([width,'mutation-'+label,'unsat',result.stdout.strip()])
        assert result.returncode==0 and result.stdout.strip()=='unsat'
(out/'report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
