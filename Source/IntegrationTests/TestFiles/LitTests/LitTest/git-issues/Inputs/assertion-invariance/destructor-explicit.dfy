datatype D = Nil | Cons(value:int) lemma L(d:D) requires d.Cons? { assert d.Cons?; var v := d.value; }
