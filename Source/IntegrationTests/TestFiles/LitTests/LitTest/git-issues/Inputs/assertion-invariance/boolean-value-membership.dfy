ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
ghost predicate Identity(b:bool) { b }
lemma L() requires exists n:int :: P(n) { assert true in (set b:bool | Identity(b) == (exists n:int :: P(n)));  }
