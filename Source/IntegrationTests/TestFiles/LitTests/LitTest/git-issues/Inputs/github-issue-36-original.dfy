ghost predicate P(a: int) { a == 7 }

lemma SuchThat() {
  assert P(7);
  var a :| P(a);
  assert a == 7;
}

lemma SuchThatThenRestate() {
  assert P(7);
  var a :| P(a);
  assert P(a);
  assert a == 7;
}

lemma Assume(a: int) {
  assume {:axiom} P(a);
  assert a == 7;
}

lemma Requires(a: int) requires P(a) {
  assert a == 7;
}
