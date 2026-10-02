module UnrelatedError {
  function Bad(): bool { missing }
}
module Cycle {
  newtype A = b | P(b)
  newtype B = a: A | true
  predicate P(b: B)
}
module Independent {
  newtype N = n: int | 0 <= n
  function Twice(n: N): N { n + n }
}
