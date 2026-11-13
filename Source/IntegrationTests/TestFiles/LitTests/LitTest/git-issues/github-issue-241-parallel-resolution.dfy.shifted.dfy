// RUN: %baredafny resolve --resolution-threads 8 %s > "%t"
// Source-map shift and filename stability fixture for parallel resolution.
module Before { function F(): bv33 { 0 } }
module Base {
  datatype D = X | Y(v: int)
  function F(x: int): int { x + 1 }
  function Value(): int -> int { F }
}
module M {
  import B = Base
  function F(x: int): int { var t := (x, true, 3); B.Value()(t.0) }
  function Match(d: B.D): int { match d case X => 0 case Y(_) => 1 }
  lemma Good(x: int) ensures F(x) == x + 1 { }
  method Work(x: int) returns (y: int) { var z := x; y := z; assert y == x; }
}
module W {
  import B = Base
  function F(x: bv13): bv13 { x << 2 }
  lemma Shift(x: bv13) ensures F(x) == x << 2 { }
}
