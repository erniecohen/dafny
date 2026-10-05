ghost predicate Nonnegative(i: int) { i >= 0 }
type Nonneg = i: int | Nonnegative(i) witness 0
ghost function Use(i: int): int requires Nonnegative(i) { i }
ghost function F(i: nat): int { Use(i) }
