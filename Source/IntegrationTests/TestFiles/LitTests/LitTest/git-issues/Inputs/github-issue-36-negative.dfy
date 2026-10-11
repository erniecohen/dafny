ghost predicate P(a: int) { a == 7 }
ghost predicate Partial(a: int) requires a != 0 { a == 7 }
opaque ghost predicate Hidden(a: int) { a == 7 }

lemma Vacuity() {
  assert P(7);
  var a :| P(a);
  assert false;
}

lemma WrongValue() {
  assert P(7);
  var a :| P(a);
  assert a == 8;
}

lemma MissingPrecondition() {
  assert Partial(7);
  var a :| Partial(a);
}

lemma NoWitness() {
  var a :| a == 7 && a == 8;
}

lemma GuardFalse() {
  var a :| a == 0 || Partial(a);
  assert a != 0;
}

lemma ConditionalGuardFalse() {
  var a :| if a == 0 then true else Partial(a);
  assert a != 0;
}

lemma PreserveOpacity() {
  reveal Hidden();
  assert Hidden(7);
  // The predicate is intentionally hidden again in the separate declaration below.
}
lemma HiddenChoice() requires Hidden(7) {
  var a :| Hidden(a);
  assert a == 7;
}

lemma HigherOrderMissingPrecondition(p: int --> bool)
  requires p.requires(7) && p(7)
{
  var a :| p(a);
}
