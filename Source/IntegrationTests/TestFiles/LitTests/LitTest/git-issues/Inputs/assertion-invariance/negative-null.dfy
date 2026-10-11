class C { var i: int }
datatype D = Nil | Val(value: int)
lemma L() { var c: C? := null; var x := c.i; }
