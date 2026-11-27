// Fresh processes and complete report comparisons require the external CLI.
// RUN: python3 "%repositoryRoot/.github/review/persistent-module-json.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

include "contract.dfy"
module Mixed {
  import C = Contracts
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) > x { assert x + 0 == x; }
  method Use(a: array<int>, i: int) requires 0 <= i < a.Length { var x := C.Read(a, i); assert x == x; }
  lemma {:resource_limit 1} Limited(x: int) ensures x * x >= 0 {}
}
module Other { lemma Good() { assert 1 + 1 == 2; } }
module Witness { lemma Bad() { assert false; } }
