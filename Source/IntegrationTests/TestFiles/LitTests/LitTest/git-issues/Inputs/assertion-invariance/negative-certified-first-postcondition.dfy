ghost predicate F(x:int) requires x>0 { true }
lemma L(x:int) requires x>0 ensures false ensures F(x) {}
