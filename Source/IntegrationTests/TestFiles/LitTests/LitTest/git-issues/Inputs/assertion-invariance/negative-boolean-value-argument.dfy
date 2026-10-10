ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
ghost predicate Not(b:bool) { !b }
lemma L() requires exists n:int :: P(n) { assert Not(exists n:int :: P(n)); assert false; }
