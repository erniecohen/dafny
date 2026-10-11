class C { var i: int }
datatype D = Nil | Val(value: int)
lemma L() { var d := Nil; var x := d.value; }
