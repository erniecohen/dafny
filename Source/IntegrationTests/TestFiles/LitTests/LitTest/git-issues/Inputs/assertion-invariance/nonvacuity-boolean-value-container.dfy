ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
lemma L() requires exists n:int :: P(n) { assert [exists n:int :: P(n)][0];  assert false; }
