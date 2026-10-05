ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
lemma L() { assert [exists n:int :: P(n)][0]; }
