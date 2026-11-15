ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
ghost predicate Identity(b:bool) { b }
lemma L() requires exists n:int :: P(n) ensures Identity(exists n:int :: P(n)) { assert Identity(exists n:int :: P(n));  assert false; }
