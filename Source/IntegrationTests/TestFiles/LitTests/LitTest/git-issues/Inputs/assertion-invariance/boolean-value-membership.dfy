ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
lemma L() { assert true in (set b:bool | b == (exists n:int :: P(n)));  }
