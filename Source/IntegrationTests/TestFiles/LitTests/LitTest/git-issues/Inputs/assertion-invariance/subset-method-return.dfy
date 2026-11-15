ghost predicate Nonnegative(i: int) { i >= 0 }
type Nonneg = i: int | Nonnegative(i) witness 0
lemma L(i: nat) returns (x: Nonneg) { x := i; }
