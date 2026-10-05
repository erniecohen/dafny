ghost function pred(i:int):int { i-1 }
ghost predicate f(a:int,s:int) { a<=0 || exists s0:int :: f(pred(a),s0) }
lemma L() { assert !(f(0,0) <==> true); }
