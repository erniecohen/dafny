ghost predicate Nonnegative(i: int) { i >= 0 }
type Nonneg = i: int | Nonnegative(i) witness 0
lemma Use(i: int) requires Nonnegative(i) {}
lemma L() { Use(-1); }
