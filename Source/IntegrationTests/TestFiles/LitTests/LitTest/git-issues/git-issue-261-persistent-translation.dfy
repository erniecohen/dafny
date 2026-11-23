// Fresh processes and stored-cache tampering require the external CLI rather than the singleton verdict planner.
// RUN: python3 "%repositoryRoot/.github/review/persistent-translation.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/261
module A {
  function F(x: int): int { x + 1 }
  lemma FPos(x: int) requires x >= 0 ensures F(x) > 0 { assert x + 0 == x; }
}
module B {
  import A
  method M(x: int) returns (y: int) requires x >= 0 ensures y > 0 { hide A.F; A.FPos(x); y := A.F(x); }
  lemma L(n: nat) ensures n * n >= 0 {}
}
module C {
  method N() { assert 1 + 1 == 2; }
}
module H {
  lemma Bad(x: int) ensures x > 0 {}
}
