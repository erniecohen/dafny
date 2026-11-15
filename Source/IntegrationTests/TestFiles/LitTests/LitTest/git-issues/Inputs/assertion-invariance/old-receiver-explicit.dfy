class C { var i:int twostate lemma Use() {} } lemma L(c:C) { assert old(allocated(c)); c.Use(); }
