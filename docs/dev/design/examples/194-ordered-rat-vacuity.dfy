include "194-ordered-rat-explicit.dfy"
lemma false_control(a: Rat) {
  a.reflexive();
  a.eq_reflexive();
  assert false;
}
