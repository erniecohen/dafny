ghost predicate Nonnegative(i: int) { i >= 0 }
type Nonneg = i: int | Nonnegative(i) witness 0
type Positive = i: Nonneg | i > 0 witness 1
ghost function F(i: nat): Positive { i + 1 }
