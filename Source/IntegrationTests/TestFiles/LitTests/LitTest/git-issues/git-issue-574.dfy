// The cache replays across fresh processes, so the external CLI runs each step.
// RUN: python3 "%repositoryRoot/.github/review/verification-cache-solver-digest.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/574
module M {
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) == x + 1 {}
  lemma Bad(x: int) { assert x == x + 1; }
}
module Other {
  lemma Good() { assert true; }
}
