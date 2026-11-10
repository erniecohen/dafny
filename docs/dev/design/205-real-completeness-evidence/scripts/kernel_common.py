from pathlib import Path
import subprocess,json,re,csv,sys,hashlib
ROOT=Path(__file__).parent
COMMON='''(set-option :auto_config false)
(set-option :smt.mbqi false)
(set-option :smt.qi.eager_threshold 44)
(set-option :smt.case_split 3)
(set-option :smt.qi.profile true)
(set-option :rlimit 2000000)
(set-option :timeout 10000)
(declare-sort ISet 0)
(declare-fun member (ISet Real) Bool)
(declare-fun upper (ISet Real) Bool)
(declare-fun sup (ISet) Real)
(declare-fun request (ISet Real Real) Bool)
(assert (forall ((s ISet) (u Real))
 (! (= (upper s u) (forall ((x Real)) (! (=> (member s x) (<= x u)) :pattern ((member s x)) :qid upper_point)))
 :pattern ((upper s u)) :qid upper_def)))
'''
AXIOM='''(assert (forall ((s ISet) (n Real) (u Real))
 (! (=> (and (member s n) (upper s u))
  (and (upper s (sup s))
   (forall ((b Real)) (! (=> (upper s b) (<= (sup s) b)) :pattern ((upper s b)) :qid sup_least))))
 :pattern (%s) :qid completeness)))
'''
PATTERNS={'broad':'(member s n) (upper s u)', 'sup':'(sup s) (member s n) (upper s u)', 'request':'(request s n u)'}
def scaffold(n=1):
 return '\n'.join(f'(declare-const s{i} ISet)' for i in range(n))+'\n'
def valid(s='s0',n='0.0',u='2.0'):
 return f'(assert (member {s} {n}))\n(assert (upper {s} {u}))\n(assert (request {s} {n} {u}))\n'
def mk_cases():
 c={}
 c['lub_upper']=scaffold()+valid()+'(declare-const x Real)\n(assert (member s0 x))\n(assert (> x (sup s0)))\n'
 c['lub_least']=scaffold()+valid()+'(declare-const b Real)\n(assert (upper s0 b))\n(assert (> (sup s0) b))\n'
 c['sup_interval']=scaffold()+valid()+'(assert (not (and (<= 0.0 (sup s0)) (<= (sup s0) 2.0))))\n'
 c['bounded_false_control']=scaffold()+valid()+'(assert (>= (sup s0) 0.0))\n'
 c['empty_false_control']=scaffold()+'(assert (forall ((x Real)) (! (not (member s0 x)) :pattern ((member s0 x)) :qid empty)))\n(assert (request s0 0.0 0.0))\n(assert (= (sup s0) 17.0))\n'
 c['unbounded_false_control']=scaffold()+'(assert (forall ((x Real)) (! (member s0 x) :pattern ((member s0 x)) :qid universal)))\n(assert (request s0 0.0 0.0))\n(assert (= (sup s0) 17.0))\n'
 c['passive_bounded_sets_20']=scaffold(20)+'\n'.join(f'(assert (member s{i} {i}.0))\n(assert (upper s{i} {i+1}.0))' for i in range(20))+'\n'
 c['unrelated_arithmetic']='(declare-const x Real)\n(assert (> x 0.0))\n(assert (<= (+ x 1.0) 1.0))\n'
 c['unrelated_bounded_sets_20']=scaffold(20)+'\n'.join(f'(assert (member s{i} {i}.0))\n(assert (upper s{i} {i+1}.0))' for i in range(20))+'\n(declare-const x Real)\n(assert (> x 0.0))\n(assert (<= (+ x 1.0) 1.0))\n'
 for n in (1,4,16,64):
  c[f'single_requested_members_{n}']=scaffold()+valid()+''.join(f'(declare-const x{i} Real)\n(assert (member s0 x{i}))\n' for i in range(n))+f'(assert (> x{n-1} (sup s0)))\n'
 # Abstracts the LUB facts used at the limit point of the IVT proof; continuity lemmas are hypotheses, not new axioms.
 c['ivt_limit_sign']=scaffold()+valid('s0','0.0','1.0')+'''(declare-fun p (Real) Real)
(declare-const eps Real)
(assert (> eps 0.0))
(assert (=> (> (p (sup s0)) 0.0) (upper s0 (- (sup s0) eps))))
(assert (=> (< (p (sup s0)) 0.0) (member s0 (+ (sup s0) eps))))
(assert (not (= (p (sup s0)) 0.0)))
'''
 return c
