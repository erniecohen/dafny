class C { var i:int } twostate lemma Use(c:C) {} method L(c:C) modifies c { label Before: c.i := c.i; Use@Before(c); }
