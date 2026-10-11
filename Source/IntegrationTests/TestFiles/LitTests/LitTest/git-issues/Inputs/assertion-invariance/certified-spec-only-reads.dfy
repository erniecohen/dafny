class C { var i:int }
method Set(c:C) reads {} modifies c ensures c.i==0 { c.i:=0; }
method L(c:C) reads {} modifies c ensures c.i==0 { Set(c); }
