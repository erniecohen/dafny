#!/usr/bin/env python3
"""Replay each captured VC in a fresh solver, retaining complete exit-time profiles."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

def commands(text):
    result=[];start=None;depth=0;quoted=False;bar=False;comment=False
    for i,c in enumerate(text):
        if comment:
            if c=='\n':comment=False
            continue
        if quoted:
            if c=='"':quoted=False
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
            if depth==0:result.append(text[start:i+1])
    assert depth==0
    return result

def queries(text):
    current=[];vc='';result=[]
    for cmd in commands(text):
        if cmd=='(reset)':current=[]
        if cmd.startswith('(set-info :boogie-vc-id '):vc=cmd[len('(set-info :boogie-vc-id '):-1]
        if cmd=='(check-sat)':
            # Captures used here reset before every query. Reject reuse rather than
            # accidentally replay a previous check or forget stack restoration.
            assert not any(c=='(check-sat)' for c in current)
            result.append((vc,current.copy()))
        if cmd.startswith('(get-info') or cmd.startswith('(pop '):continue
        current.append(cmd)
    return result

def replay(z3, body, dest, label, limit):
    dest.mkdir(parents=True,exist_ok=True)
    # Keep the existing options; replace the existing resource cap only with the
    # comparison's unchanged 20M cap. Profiling is observation, not solver tuning.
    body=[c for c in body if not c.startswith('(set-option :rlimit')]
    body+=['(set-option :smt.qi.profile true)',f'(set-option :rlimit {limit})','(check-sat)',
           '(get-info :reason-unknown)','(get-info :all-statistics)','(exit)']
    source='\n'.join(body)+'\n';path=dest/'query.smt2';path.write_text(source)
    cmd=[z3,'-smt2',str(path)]
    timed_out=False
    try:
        proc=subprocess.run(cmd,capture_output=True,text=True,timeout=2400)
        stdout,stderr,exit_code=proc.stdout,proc.stderr,proc.returncode
    except subprocess.TimeoutExpired as e:
        stdout=e.stdout or b'';stderr=e.stderr or b''
        if isinstance(stdout,bytes):stdout=stdout.decode()
        if isinstance(stderr,bytes):stderr=stderr.decode()
        exit_code=None;timed_out=True
    (dest/'stdout.txt').write_text(stdout);(dest/'stderr.txt').write_text(stderr)
    (dest/'command.json').write_text(json.dumps(cmd,indent=2))
    inventory=re.findall(r':qid\s+(\|[^|]+\||[^\s)]+)',source)
    profile_lines=[line for line in stderr.splitlines() if '[quantifier_instances]' in line]
    (dest/'quantifier-profile.txt').write_text('\n'.join(profile_lines)+'\n')
    (dest/'quantifier-inventory.json').write_text(json.dumps(inventory,indent=2))
    counters=re.findall(r'\[quantifier_instances\]\s+(.+?)\s+:\s+(\d+)\s+:\s+(\d+)\s+:\s+(\d+)\s+:\s+(\d+)\s+:\s+([\d.]+)',stderr)
    profiles=[dict(qid=c[0],instances=int(c[1]),simplify_true=int(c[2]),checker_sat=int(c[3]),max_generation=int(c[4]),max_cost=float(c[5])) for c in counters]
    (dest/'quantifier-profile.json').write_text(json.dumps(profiles,indent=2))
    status=re.findall(r'^(sat|unsat|unknown)$',stdout,re.M)
    ru=re.search(r':rlimit-count\s+(\d+)',stdout)
    instances=re.search(r':quant-instantiations\s+(\d+)',stdout)
    total_instances=int(instances[1]) if instances else 0
    profile_total=sum(c['instances'] for c in profiles)
    reason=re.search(r':reason-unknown "([^"]*)"',stdout)
    result=dict(label=label,status=status[-1] if status else None,exit_code=exit_code,
                resource_count=int(ru[1]) if ru else None,
                quantifier_instantiations=total_instances,
                profile_instance_total=profile_total,
                profile_complete=not timed_out and exit_code==0 and profile_total==total_instances,
                reason_unknown=reason[1] if reason else None,
                profile_records=len(profile_lines),quantifiers=len(inventory),
                wall_timeout=timed_out,sha256=hashlib.sha256(source.encode()).hexdigest())
    (dest/'result.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result),flush=True)
    return result

def literal_variants(body):
    ax=next(c for c in body if ':qid additional_axioms_bv32_int_round_trip' in c)
    match=re.search(r'\(= \((\S+) \(\(_ int2bv 32\) (\S+)\)\) \2\)',ax)
    wrapper,x=match.groups()
    lit_ax=next(c for c in body if ':qid |DafnyPreludebpl.112:29|' in c)
    lit=re.search(r'\(= \((\S+) (\S+)\) \2\)',lit_ax)[1]
    vc=body[-1]
    variable=re.search(r'\('+re.escape(lit)+r' 0\) ([^\s()]+)\)',vc)[1]
    assert f'(declare-fun {variable} () Int)' in body
    literal=f'(assert (= ({lit} 0) 0))'
    ground=f'(assert (=> (and (<= 0 {variable}) (< {variable} 4294967296)) (= ({wrapper} ((_ int2bv 32) {variable})) {variable})))'
    old=f':pattern ( ({wrapper} ((_ int2bv 32) {x})))'
    assert old in ax
    broad=ax.replace(old,f':pattern (((_ int2bv 32) {x}))')
    return [('unchanged',body),('ground-literal-identity',body+[literal]),
            ('conversion-trigger-diagnostic',[broad if c==ax else c for c in body]),
            ('ground-round-trip-instance',body+[ground])]

p=argparse.ArgumentParser();p.add_argument('--smt',required=True);p.add_argument('--z3',required=True);p.add_argument('--output',required=True)
p.add_argument('--literal',action='store_true');p.add_argument('--limit',type=int,default=20000000)
a=p.parse_args();out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
version=subprocess.run([a.z3,'-version'],capture_output=True,text=True,check=True).stdout
(out/'solver-version.txt').write_text(version)
(out/'solver-sha256.txt').write_text(hashlib.sha256(Path(a.z3).read_bytes()).hexdigest()+'\n')
selected=queries(Path(a.smt).read_text());report=[]
if a.literal:
    selected=[(name,body) for name,body in selected if 'Impl$' in name and 'ShiftRightByZero' in name]
    assert len(selected)==1
    for label,body in literal_variants(selected[0][1]):report.append(replay(a.z3,body,out/label,label,a.limit))
else:
    for i,(name,body) in enumerate(selected):report.append(replay(a.z3,body,out/f'{i:04}',name,a.limit))
(out/'report.json').write_text(json.dumps(report,indent=2))
