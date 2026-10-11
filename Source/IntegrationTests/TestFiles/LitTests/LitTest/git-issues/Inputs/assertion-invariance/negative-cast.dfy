ghost predicate Nonnegative(i: int) { i >= 0 }
type Nonneg = i: int | Nonnegative(i) witness 0
lemma L() { var x := -1 as Nonneg; }
