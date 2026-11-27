// Fresh processes and independent report/cache oracles require the external CLI.
// RUN: python3 "%repositoryRoot/.github/review/persistent-translation-csv.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

module A {
  function F(x: int): int { x + 1 }
  lemma L(x: int) ensures F(x) > x { assert x + 0 == x; }
}
module B {
  import A
  method M(x: int) returns (y: int) ensures y > x { A.L(x); y := A.F(x); }
}
module H {
  lemma Witness() { assert false; }
}
