type PositiveAtZero = f: int -> int | f(0) > 0 witness (x: int) => 1

lemma CannotIntroduceRawSubset(f: int -> int) {
  var value := f as PositiveAtZero; // Must fail the positive-at-zero predicate.
}
