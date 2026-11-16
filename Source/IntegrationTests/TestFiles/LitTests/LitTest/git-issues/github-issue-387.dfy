// The persistent verification cache under --filter-position-batch: fresh processes need the external CLI.
// RUN: python3 "%repositoryRoot/.github/review/verification-cache-batch.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/387
module M {
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) == x + 1 {}
  lemma Bad(x: int) { assert x == x + 1; }
}
module Other {
  lemma Good() { assert true; }
}
