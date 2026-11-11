// RUN: %exits-with 4 %verify --cores 1 --show-snippets:false --use-basename-for-filename --resolution-threads 1 --bprint "%t.off.bpl" --filter-position-batch "%s" --filter-position-batch "%s" "%s" > "%t.off"
// RUN: %exits-with 4 %verify --cores 1 --show-snippets:false --use-basename-for-filename --translation-cache --bprint "%t.on.bpl" --pipeline-profile "%t.jsonl" --filter-position-batch "%s" --filter-position-batch "%s" "%s" > "%t.on"
// RUN: %diff "%t.off" "%t.on"
// RUN: %diff "%t.off_A.bpl" "%t.on_A.bpl"
// RUN: %diff "%t.off_B.bpl" "%t.on_B.bpl"
// RUN: python3 "%repositoryRoot/.github/review/translation-cache.py" "%t.jsonl" > "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/241
module A {
  function F(x: int): int { x + 1 }
  function Value(): int -> int { F }
}
module B {
  import X = A
  lemma Good(x: int) ensures X.Value()(x) == x + 1 {}
  lemma Bad() { assert false; }
}
