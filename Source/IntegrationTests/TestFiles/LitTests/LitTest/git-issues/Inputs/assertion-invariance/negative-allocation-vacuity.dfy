class C {}
twostate lemma Use(c: C) {}
lemma L(c: C) { Use(c); assert false; }
