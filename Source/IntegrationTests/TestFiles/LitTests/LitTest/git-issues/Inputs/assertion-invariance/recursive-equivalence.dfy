ghost function pred(i:int):int { i-1 }
ghost predicate f(a:int,s:int) { a<=0 || exists s0:int :: f(pred(a),s0) }
lemma L(a:int,s:int) { assert f(a,s) <==> (a<=0 || exists s0:int :: f(pred(a),s0)); }
