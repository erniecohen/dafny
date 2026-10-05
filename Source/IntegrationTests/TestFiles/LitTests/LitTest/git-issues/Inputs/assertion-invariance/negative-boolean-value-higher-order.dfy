ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
lemma L() requires exists n:int :: P(n) { var f := (b:bool) => !b; assert f(exists n:int :: P(n)); assert false; }
