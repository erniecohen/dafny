#!/usr/bin/env python3
"""Formula-identical trigger replay for pinned Dafny SMT logs (plain or gzip)."""
import argparse,copy,gzip,hashlib,json,re,subprocess
from pathlib import Path
from smt_helpers import parse,emit

def is_qid(q,prefix,lines):
 return any(re.search(re.escape(prefix)+r'\.'+str(n)+r':\d+',q) for n in lines)
def mutate(t,variant,box,counts,prefix,upperlines,leastlines):
 if not isinstance(t,list):return
 if len(t)==3 and t[0]=='forall' and isinstance(t[2],list) and t[2][0]=='!':
  bs=t[1];a=t[2]
  if len(bs)==1 and bs[0][1]=='Real' and ':qid' in a and ':pattern' in a:
   q=a[a.index(':qid')+1];upper=is_qid(q,prefix,upperlines);least=is_qid(q,prefix,leastlines)
   selected=(upper and variant in ['broad-upper','broad-both','auto']) or (least and variant in ['broad-least','broad-both','auto'])
   if selected:
    i=a.index(':pattern')
    if variant=='auto':del a[i:i+2]
    else:a[i+1]=[[box,bs[0][0]]]
    counts['upper' if upper else 'least']+=1
 for x in t:mutate(x,variant,box,counts,prefix,upperlines,leastlines)
def strip_patterns(t):
 if not isinstance(t,list):return t
 result=[];i=0
 while i<len(t):
  if t and t[0]=='!' and t[i]==':pattern':i+=2;continue
  result.append(strip_patterns(t[i]));i+=1
 return result
def main():
 p=argparse.ArgumentParser()
 for k in ['smt','z3','output','qid_prefix','upper_lines','least_lines']:p.add_argument('--'+k,required=True)
 p.add_argument('--variants',default='scoped,broad-upper,auto');p.add_argument('--resource_limit',type=int,default=2000000);p.add_argument('--timeout_ms',type=int,default=30000);p.add_argument('--filter',default='')
 a=p.parse_args();out=Path(a.output);out.mkdir(exist_ok=True,parents=True)
 src=Path(a.smt);original=gzip.decompress(src.read_bytes()).decode() if src.suffix=='.gz' else src.read_text()
 upperlines=[int(x) for x in a.upper_lines.split(',')];leastlines=[int(x) for x in a.least_lines.split(',')];variants=a.variants.split(',');rows=[]
 blocks=original.split('(set-option :print-success false)')[1:]
 for i,b in enumerate(blocks):
  b='(set-option :print-success false)\n(set-option :smt.qi.profile true)\n'+re.split(r'\(reset\)',b)[0]
  b=re.sub(r'\(set-option :rlimit \d+\)',f'(set-option :rlimit {a.resource_limit})',b)
  b=re.sub(r'\(set-option :timeout \d+\)',f'(set-option :timeout {a.timeout_ms})',b)
  name=re.search(r'\(set-info :boogie-vc-id (\S+)\)',b)[1]
  if a.filter and a.filter not in name:continue
  if '(check-sat)' not in b:
   raise RuntimeError('Unqueried VC in captured log: '+name)
  boxes=re.findall(r'\(declare-fun (\S+) \(Real\) T@U\)',b)
  # A proof with no native real boxing cannot contain our targeted upper/least quantifiers.
  assert len(boxes)<=1,boxes;box=boxes[0] if boxes else None;parsed=parse(b)
  fingerprint=hashlib.sha256('\n'.join(emit(strip_patterns(x)) for x in parsed).encode()).hexdigest()
  for variant in variants:
   ast=copy.deepcopy(parsed);counts={'upper':0,'least':0}
   for cmd in ast:mutate(cmd,variant,box,counts,a.qid_prefix,upperlines,leastlines)
   assert box is not None or counts=={'upper':0,'least':0}
   assert fingerprint==hashlib.sha256('\n'.join(emit(strip_patterns(x)) for x in ast).encode()).hexdigest()
   text='\n'.join(emit(x) for x in ast)+'\n(get-info :all-statistics)\n'
   path=out/f'{i:03}-{variant}.smt2';path.write_text(text)
   r=subprocess.run([a.z3,'-smt2',str(path)],text=True,capture_output=True,timeout=a.timeout_ms/1000+10)
   (out/f'{i:03}-{variant}.out').write_text(r.stdout+r.stderr)
   vals=re.findall(r'\(:rlimit (\d+)\)',r.stdout);qi=re.search(r':quant-instantiations\s+(\d+)',r.stdout)
   profiles={}
   for q,n in re.findall(r'\[quantifier_instances\]\s*(.*?)\s*:\s*(\d+)\s*:',r.stderr):profiles[q.strip()]=profiles.get(q.strip(),0)+int(n)
   row=dict(name=name,variant=variant,ru=int(vals[-1]) if vals else None,instances=int(qi[1]) if qi else 0,outcomes=re.findall(r'^(sat|unsat|unknown)$',r.stdout,re.M),mutated=counts,profiles=profiles,exit=r.returncode,formula_sha256=fingerprint)
   rows.append(row);print(i,variant,row['ru'],row['instances'],row['outcomes'],counts,flush=True)
 (out/'results.json').write_text(json.dumps(rows,indent=2))
 summary={v:{'contexts':len([x for x in rows if x['variant']==v]),'ru':sum(x['ru'] or 0 for x in rows if x['variant']==v),'instances':sum(x['instances'] for x in rows if x['variant']==v),'unsat_contexts':sum(x['outcomes']==['unsat'] for x in rows if x['variant']==v),'not_proved':[x['name'] for x in rows if x['variant']==v and x['outcomes']!=['unsat']]} for v in variants}
 (out/'summary.json').write_text(json.dumps(summary,indent=2))
 (out/'manifest.json').write_text(json.dumps({'solver_version':subprocess.check_output([a.z3,'--version'],text=True).strip(),'solver_sha256':hashlib.sha256(Path(a.z3).read_bytes()).hexdigest(),'input_sha256':hashlib.sha256(original.encode()).hexdigest(),'qid_prefix':a.qid_prefix,'upper_lines':upperlines,'least_lines':leastlines,'resource_limit':a.resource_limit,'timeout_ms':a.timeout_ms,'scope':'Exact native Dafny VCs with only explicit pattern annotations changed; formulas equal after removing pattern annotations. Profiling is enabled in standalone replay, not live Dafny.'},indent=2))
 print(json.dumps(summary),flush=True)
if __name__=='__main__':main()
