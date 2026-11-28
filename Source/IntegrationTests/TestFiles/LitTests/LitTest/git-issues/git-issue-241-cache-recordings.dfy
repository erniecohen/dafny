// RUN: %exits-with 4 %verify --type-system-refresh --cores 2 --verification-time-limit 60 --show-snippets:false --use-basename-for-filename --boogie /deterministicLiteralHashes:1 --boogie /restartProver --pipeline-profile "%t.default.jsonl" --log-format "json;LogFileName=%t.default.json" --bprint "%t.default.bpl" "%s" > "%t"
// RUN: python3 "%repositoryRoot/.github/review/cache-recordings.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.default" "%t.dir" > "%t.check"
// RUN: %diff "%s.check.expect" "%t.check"
// RUN: %diff "%s.expect" "%t"
// Fresh processes establish default/opt-in identity and persistent cache replay.
// UNSUPPORTED: windows

module Library {
  function Increment(x: int): int { x + 1 }
  lemma Positive(x: int) ensures Increment(x) > x { }
}
module Client {
  import L = Library
  lemma Positive(x: int) ensures L.Increment(x) > x { L.Positive(x); }
}
module Counterexample {
  lemma ReachableFalse() { assert false; }
}
// RUN: python3 "%repositoryRoot/.github/review/cache-recordings-models.py" "%binaryDir/Dafny.dll" "%review-z3" "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-261-persistent-models.dfy" "%t.models.dir" > "%t.models"
// RUN: %diff "%s.models.expect" "%t.models"
