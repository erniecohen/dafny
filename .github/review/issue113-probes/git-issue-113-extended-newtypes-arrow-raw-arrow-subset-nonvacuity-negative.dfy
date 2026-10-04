type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1

lemma InhabitedAndConsistent() {
  var value := ((x: int) => 1) as PositiveAtZero;
  assert value(0) == 1;
  assert false; // Only this assertion is intended to fail.
}
