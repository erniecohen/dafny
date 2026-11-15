predicate P(a: int) { a == 7 }
predicate Pair(a: int, b: int) { a == 7 && b == 9 }
predicate Partial(a: int) requires a != 0 { a == 7 }

method Compiled() {
  assert P(7);
  var a :| 0 <= a <= 10 && P(a);
  assert a == 7;
}

lemma Multiple() {
  assert Pair(7, 9);
  var a, b :| Pair(a, b);
  assert a == 7 && b == 9;
}

lemma Existing() {
  var a := 0;
  assert P(7);
  a :| P(a);
  assert a == 7;
}

lemma ShortCircuit() {
  assert Partial(7);
  var a :| a != 0 && Partial(a);
  assert a == 7;
}

lemma Conditional() {
  assert Partial(7);
  var a :| if a == 0 then false else Partial(a);
  assert a == 7;
}

lemma Disjunction() {
  assert Partial(7);
  var a :| a == 0 || Partial(a);
  assert a == 0 || a == 7;
}

lemma HigherOrder(p: int --> bool)
  requires forall a :: a != 0 ==> p.requires(a)
  requires p(7)
  requires forall a | a != 0 && p(a) :: a == 7
{
  var a :| a != 0 && p(a);
  assert a == 7;
}

class Cell {
  var value: int
  predicate At(a: int) reads this { a == value }
  lemma Choose() {
    assert At(value);
    var a :| At(a);
    assert a == value;
  }
}

lemma Implication() {
  var a :| a != 0 ==> Partial(a);
  assert a == 0 || a == 7;
}

class GhostCell {
  ghost var value: int
  ghost method MixedAssumed() modifies this {
    var a: int;
    a, value :| assume {:axiom} P(a) && value == a;
    assert a == 7 && value == 7;
  }
}
