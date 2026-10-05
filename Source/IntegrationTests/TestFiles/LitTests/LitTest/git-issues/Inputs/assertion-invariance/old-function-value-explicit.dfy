twostate lemma Use(old f:int->int) {} lemma L(f:int->int) { assert old(allocated(f)); Use(f); }
