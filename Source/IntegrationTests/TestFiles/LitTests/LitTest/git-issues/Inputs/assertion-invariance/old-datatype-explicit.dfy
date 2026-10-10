class C {} datatype D = D(c:C) twostate lemma Use(d:D) {} lemma L(d:D) { assert old(allocated(d)); Use(d); }
