// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify "%s" --verification-module-window=2 > "%t.window"
// RUN: %diff "%t" "%t.window"
// RUN: %exits-with 4 %verify "%s" --additional-axioms > "%t.axioms"
// RUN: %diff "%t" "%t.axioms"
// RUN: %exits-with 0 %verify "%s" --filter-symbol Streams.Positive > "%t.positive"
// RUN: %exits-with 0 %verify "%s" --filter-symbol Streams.Positive --translation-cache > "%t.cached"
// RUN: %diff "%t.positive" "%t.cached"

// Each native stream closes after one result, including isolated assertion batches.
module Streams {
  lemma {:isolate_assertions} Positive() {
    assert 1 + 1 == 2;
    assert 2 + 2 == 4;
  }

  lemma Negative() {
    assert false;
  }
}

module OtherStream {
  lemma Positive() {
    assert true;
  }
}

// Filtered requests select identities from the whole module, including isolated batches.
// RUN: %exits-with 0 %verify "%s" --filter-symbol Streams.Positive --boogie /randomSeed:73 > "%t.seeded"
// RUN: %diff "%t.positive" "%t.seeded"
// RUN: %exits-with 0 %verify "%s" --filter-position :14 > "%t.part"
// RUN: %exits-with 0 %verify "%s" --filter-position :14 --translation-cache > "%t.part.cached"
// RUN: %diff "%t.part" "%t.part.cached"
