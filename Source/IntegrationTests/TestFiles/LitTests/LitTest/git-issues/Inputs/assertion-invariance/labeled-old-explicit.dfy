class C { var i:int } twostate lemma Use(old c:C) {} method L(c:C) modifies c { label Before: c.i := c.i; assert old@Before(allocated(c)); Use@Before(c); }
