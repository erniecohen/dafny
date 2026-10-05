ghost predicate P(n:int) decreases n { n<=0 || P(n-1) }
lemma L() { assert (var b := exists n:int :: P(n); !b); assert false; }
