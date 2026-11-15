ghost predicate F(x:int) requires x>0 { x>0 }
lemma L(x:int) requires x>0 ensures F(x) {}
