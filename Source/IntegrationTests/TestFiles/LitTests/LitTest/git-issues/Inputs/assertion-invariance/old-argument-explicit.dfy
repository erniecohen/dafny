class C {}
twostate lemma Use(c: C) {}
lemma L(c: C) { assert old(allocated(c)); Use(c); }
