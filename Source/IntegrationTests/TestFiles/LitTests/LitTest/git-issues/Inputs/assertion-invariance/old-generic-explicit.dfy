twostate lemma Use<T>(t:T) {} lemma L<T>(t:T) { assert old(allocated(t)); Use(t); }
