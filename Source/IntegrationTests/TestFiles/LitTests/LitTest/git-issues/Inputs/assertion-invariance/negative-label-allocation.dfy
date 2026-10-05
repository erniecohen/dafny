class C {} twostate lemma Use(old c:C) {} method L() { label Before: var c := new C; Use@Before(c); }
