type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1
newtype Inner = PositiveAtZero witness *
newtype Outer = x: Inner | true witness *
newtype Impossible = f: PositiveAtZero | false witness *
lemma InvalidIntermediate(f: int -> int) {
  var value := f as Outer;
}
lemma InvalidNominal(f: PositiveAtZero) {
  var value := f as Impossible;
}
lemma InhabitedFalse() {
  var f: int -> int := (x: int) => 1;
  var value := f as Outer;
  assert false;
}
