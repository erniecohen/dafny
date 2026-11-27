// RUN: %exits-with 4 %verify --cores 2 --type-system-refresh --show-snippets:false --use-basename-for-filename "%s" > "%t.default"
// RUN: %diff "%s.expect" "%t.default"
// RUN: python3 "%repositoryRoot/.github/review/ready-modules.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.check.expect" "%t"
// Independent, imported and refined modules; Good needs a real proof and Bad must fail.
module A {
  function F(x: int): int { x + 1 }
  lemma Good(x: int) ensures F(x) > x { }
}
module B {
  import A
  lemma Good(x: int) ensures A.F(x) > x { A.Good(x); }
}
abstract module Spec {
  function F(x: int): int
  lemma Good(x: int) ensures F(x) > x
}
module Refined refines Spec {
  function F(x: int): int { x + 2 }
  lemma Good(x: int) ensures F(x) > x { }
}
module C {
  lemma Bad() { assert false; }
}
