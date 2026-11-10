#!/usr/bin/env python3
"""Generate/replay real-completeness trigger kernels. Not a Dafny implementation."""
import argparse,csv,json,re,subprocess,hashlib
from pathlib import Path
from kernel_common import COMMON,AXIOM,mk_cases
PATTERNS={'broad':'(member s n) (upper s u)', 'sup-flat':'(sup s) (member s n) (upper s u)', 'request':'(request s n u)'}
NESTED='''(assert (forall ((s ISet))
 (! (=> (and (exists ((n Real)) (member s n)) (exists ((u Real)) (upper s u)))
  (and (upper s (sup s))
   (forall ((b Real)) (! (=> (upper s b) (<= (sup s) b)) :pattern ((upper s b)) :qid sup_least))))
 :pattern ((sup s)) :qid completeness)))
'''
LOCAL='''(assert (=> (and (member {s} {n}) (upper {s} {u}))
 (and (upper {s} (sup {s}))
  (forall ((b Real)) (! (=> (upper {s} b) (<= (sup {s}) b)) :pattern ((upper {s} b)) :qid sup_least)))))
'''
VARIANTS=['none','broad','sup-flat','nested-sup','request','local']
def main():
 p=argparse.ArgumentParser();p.add_argument('--z3',required=True);p.add_argument('--output',required=True);a=p.parse_args()
 out=Path(a.output);out.mkdir(parents=True,exist_ok=True)
 cases=mk_cases()
 cases['empty_false_control']+='(assert (upper s0 0.0))\n'
 cases['unbounded_false_control']+='(assert (member s0 (+ (sup s0) 1.0)))\n'
 rows=[]
 for case,body in cases.items():
  for variant in VARIANTS:
   if variant in PATTERNS: ax=AXIOM%PATTERNS[variant]
   elif variant=='nested-sup':ax=NESTED
   elif variant=='local':ax=''.join(LOCAL.format(s=s,n=n,u=u) for s,n,u in re.findall(r'\(assert \(request (\w+) ([\d.]+) ([\d.]+)\)\)',body))
   else:ax=''
   # Local axioms follow constant declarations; all variants are in the same position.
   txt=COMMON+body+ax+'(check-sat)\n(get-info :reason-unknown)\n(get-info :all-statistics)\n'
   f=out/f'{case}-{variant}.smt2';f.write_text(txt)
   r=subprocess.run([a.z3,'-smt2',str(f)],capture_output=True,text=True,timeout=20)
   (out/f'{case}-{variant}.out').write_text(r.stdout+r.stderr)
   st=re.search(r'^(sat|unsat|unknown)$',r.stdout,re.M); ru=re.search(r':rlimit-count\s+(\d+)',r.stdout);qi=re.search(r':quant-instantiations\s+(\d+)',r.stdout)
   profiles={}
   for qid,n in re.findall(r'\[quantifier_instances\]\s*(.*?)\s*:\s*(\d+)\s*:',r.stdout+r.stderr):
    profiles[qid.strip()]=profiles.get(qid.strip(),0)+int(n)
   row=dict(case=case,variant=variant,result=st[1] if st else 'error',ru=int(ru[1]) if ru else None,instances=int(qi[1]) if qi else 0,completeness=profiles.get('completeness',0),profiles=profiles,exit=r.returncode)
   rows.append(row);print(case,variant,row['result'],row['ru'],row['instances'],row['completeness'],flush=True)
 (out/'results.json').write_text(json.dumps(rows,indent=2))
 with (out/'results.csv').open('w') as f:
  keys=['case','variant','result','ru','instances','completeness','exit'];w=csv.DictWriter(f,fieldnames=keys,extrasaction='ignore');w.writeheader();w.writerows(rows)
 z=Path(a.z3)
 (out/'manifest.json').write_text(json.dumps({'solver_version':subprocess.check_output([a.z3,'--version'],text=True).strip(),'solver_sha256':hashlib.sha256(z.read_bytes()).hexdigest(),'method':'fresh solver process per generated SMT input; mbqi=false, auto_config=false, eager_threshold=44, case_split=3; 2,000,000 RU, 10s cap; exact remaining flags are in each SMT file','scope':'abstract real/set kernel only; IVT case is final sign contradiction conditional on analytic lemmas, not complete arbitrary-degree IVT'},indent=2))
if __name__=='__main__':main()
