// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/241
// Counter, dependency and emission-order invariance is exercised by HistoryFreeTranslationTests.
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
