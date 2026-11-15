// Each target lacks a type characteristic that the abstract type promised.
module AEq { type O(==) { function F(): int } }
module BEq refines AEq {
  datatype D = D(f: int -> int) { function F(): int { 0 } }
  type O = D
}

module AInit { type O(0) { function F(): int } }
module BInit refines AInit {
  class C { function F(): int { 0 } }
  type O = C
}

module ANonempty { type O(00) { function F(): int } }
module BNonempty refines ANonempty {
  newtype N = x: int | x > 0 witness * { function F(): int { 0 } }
  type O = N
}

module ANoNew { type O(!new) { function F(): int } }
module BNoNew refines ANoNew {
  class C { function F(): int { 0 } }
  type O = C
}
