// Fresh processes and stored-cache controls require the external CLI.
// RUN: python3 "%repositoryRoot/.github/review/persistent-module-resources.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

module Mixed {
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) > x { assert x + 0 == x; }
  lemma {:resource_limit 1} Limited(x: int) ensures x * x >= 0 {}
}
module Other { lemma Good() { assert 1 + 1 == 2; } }
module Witness { lemma Bad() { assert false; } }
