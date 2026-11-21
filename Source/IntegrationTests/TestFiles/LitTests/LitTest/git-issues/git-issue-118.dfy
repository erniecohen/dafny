// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 0 %verify "%s" --filter-symbol Boundary.Positive > "%t.positive"
// RUN: %OutputCheck --file-to-check "%t.positive" "%s"
// CHECK: Dafny program verifier finished with 1 verified, 0 errors

// Native task preparation, positive/negative results, modules and a filter share the adapter.
module Boundary {
  lemma Positive() {
    assert true;
  }

  lemma Negative() {
    assert false;
  }
}

module IndependentOther {
  lemma Positive() {
    assert 2 + 2 == 4;
  }
}

// RUN: %exits-with 0 %verify "%s" --filter-symbol Boundary.Positive --translation-cache > "%t.cached"
// RUN: %diff "%t.positive" "%t.cached"

// RUN: %exits-with 0 %verify "%s" --filter-symbol Boundary.Positive --boogie /randomSeed:73 > "%t.seeded"
// RUN: %diff "%t.positive" "%t.seeded"
