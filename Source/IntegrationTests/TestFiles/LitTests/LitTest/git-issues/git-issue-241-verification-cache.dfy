// Fresh processes and cache corruption require the external CLI rather than the singleton verdict planner.
// The imported-body early-cutoff driver runs in git-issue-241-verification-cache-cutoff.dfy
// (https://github.com/erniecohen/dafny/issues/389).
// RUN: python3 "%repositoryRoot/.github/review/verification-cache.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/241
module M {
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) == x + 1 {}
  lemma Bad(x: int) { assert x == x + 1; }
}
module Other {
  lemma Good() { assert true; }
}
