// RUN: %verify --type-system-refresh --member-invariants --recursive-modules --resolution-cache:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/recursive-module-persistent-cache.py" "%binaryDir/Dafny.dll" "%review-z3" "%S/Inputs/git-issue-466-stage6-library.dfy" "%t.dir" > "%t"
// RUN: %diff "%s.cache.expect" "%t"
// UNSUPPORTED: windows
module A {
  import B
  function Count(n: nat): nat { if n == 0 then 0 else B.Count(n - 1) }
}
module B {
  import A
  function Count(n: nat): nat { if n == 0 then 0 else A.Count(n - 1) }
}
module Client {
  import A
  lemma Facts() { assert A.Count(0) == 0; }
}
