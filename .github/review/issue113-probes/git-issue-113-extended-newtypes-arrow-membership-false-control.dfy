// Private anti-vacuity draft; no execution yet.
// Use refresh/general-newtypes/extended-newtype-bases.
newtype Consume<-T> = T -> int witness *
lemma InhabitedControl() {
  var f := ((x: int) => x) as Consume<int>;
  var narrowed: Consume<nat> := f;
  var zero: nat := 0;
  assert narrowed(zero) == 0;
  assert false; // ERROR: valid arrow membership cannot prove false
}
