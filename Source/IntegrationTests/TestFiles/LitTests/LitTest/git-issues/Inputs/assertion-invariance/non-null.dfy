class C { var i:int } lemma L(c: C?) requires c != null { var v := c.i; }
