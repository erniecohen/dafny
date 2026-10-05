twostate lemma Use<T>(old t:T) {} lemma L<T>(t:T) { assert old(allocated(t)); Use(t); }
