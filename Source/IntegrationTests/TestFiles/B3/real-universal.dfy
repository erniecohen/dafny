lemma FieldIdentity(x: real, y: real) {
  assert (x + y) - y == x;
  assert x * 1.0 == x;
}
lemma QuantifiedIdentity() {
  assert forall x: real :: x + 0.0 == x;
}
