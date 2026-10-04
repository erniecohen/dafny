type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1
newtype Inner = PositiveAtZero witness *
newtype Outer = x: Inner | true witness *

lemma CannotIntroduceNominal(f: int -> int) {
  var value := f as Outer; // Must fail the intermediate PositiveAtZero predicate.
}
