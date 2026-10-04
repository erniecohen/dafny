type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1
newtype Inner = PositiveAtZero witness *
newtype Outer = x: Inner | true witness *

lemma InhabitedAndConsistent() {
  var value := ((x: int) => 1) as Outer;
  assert (value as int -> int)(0) == 1;
  assert false; // Only this assertion is intended to fail.
}
