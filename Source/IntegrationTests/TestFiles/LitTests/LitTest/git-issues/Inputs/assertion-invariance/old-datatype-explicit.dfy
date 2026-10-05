class C {} datatype D = D(c:C) twostate lemma Use(old d:D) {} lemma L(d:D) { assert old(allocated(d)); Use(d); }
