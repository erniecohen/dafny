// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 0 %verify "%s" --filter-position :15 > "%t.filtered"
// RUN: %OutputCheck --file-to-check "%t.filtered" "%s"
// CHECK: Dafny program verifier finished with 1 assertions verified, 0 errors
// RUN: %exits-with 4 %verify "%s" --verification-module-window=2 > "%t.window"
// RUN: %diff "%t" "%t.window"
// RUN: %exits-with 4 %verify "%s" --translation-cache > "%t.cache"
// RUN: %diff "%t" "%t.cache"

// Native assertion counts and diagnostics retain their CLI spelling through the neutral aggregate.
module Reporting {
  lemma {:isolate_assertions} Good() {
    assert 1 + 1 == 2;
    assert 2 + 2 == 4;
  }
  lemma Bad() {
    assert false;
  }
}
