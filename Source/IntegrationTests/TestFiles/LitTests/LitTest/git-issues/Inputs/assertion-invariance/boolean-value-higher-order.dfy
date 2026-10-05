ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
lemma L() { var f := (b:bool) => b; assert f(exists n:int :: P(n)); }
