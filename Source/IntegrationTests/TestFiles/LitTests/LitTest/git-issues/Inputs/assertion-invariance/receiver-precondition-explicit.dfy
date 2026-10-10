class C { var i:int ghost predicate P() reads this { i>=0 } ghost function Use():int reads this requires P() { i } } lemma L(c:C) requires c.i>=0 { assert c.P(); var v := c.Use(); }
