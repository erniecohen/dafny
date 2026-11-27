// Source-bound terminal counterexample diagnostics from persistent module results.
// RUN: python3 "%repositoryRoot/.github/review/persistent-counterexamples.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
include "contract.dfy"
module Mixed {
  import C = Contracts
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) > x { assert x + 0 == x; }
  method Use(a: array<int>, i: int) requires 0 <= i < a.Length { var x := C.Read(a, i); assert x == x; }
  lemma {:resource_limit 1} Limited(x: int) ensures x * x >= 0 {}
}
module Other { lemma Good() { assert 1 + 1 == 2; } }
module Witness {
  import C = Contracts
  lemma Bad() { assert false; }
  lemma Post(x: int) ensures x > 1 { }
  method Pre(a: array<int>) { var x := C.Read(a, a.Length); }
}

module DependencyControls {
  function F(x: int): int { x + 1 }
  lemma Redundant(x: int)
    requires 0 <= x
    requires x >= 0
    ensures F(x) > x
  { assert x + 1 > x; assume {:axiom} true; }
  lemma Contradictory(x: int)
    requires x < 0 && x >= 0
    ensures x == 42
  { assert x == 19; assert {:contradiction} false; }
}
