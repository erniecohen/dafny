class C { var i:int ghost function Use():int reads this requires i>0 { i } } lemma L(c:C) requires c.i==0 { var v := c.Use(); }
