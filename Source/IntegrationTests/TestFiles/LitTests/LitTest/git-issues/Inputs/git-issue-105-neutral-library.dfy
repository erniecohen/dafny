module Neutral {
  datatype Pair = Pair(a: int, b: int)
  function Sum(p: Pair): int { p.a + p.b }
}
