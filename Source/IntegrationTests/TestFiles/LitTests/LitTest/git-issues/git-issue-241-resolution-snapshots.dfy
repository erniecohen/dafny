// RUN: %exits-with 4 %verify --type-system-refresh --cores 1 --solver-path "%review-z3" "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/resolution-snapshots.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t.cache"
// RUN: %diff "%s.cache.expect" "%t.cache"
// https://github.com/erniecohen/dafny/issues/241
module Dependency {
  function F(x: int): int { x + 1 }
}
module Client {
  import D = Dependency
  lemma Good(x: int) ensures D.F(x) == x + 1 {}
  lemma Bad() {
    assert false;
  }
}
