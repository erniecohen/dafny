// F4-M: profile observations must cover the complete run and actually release bodies.
// RUN: %exits-with 4 %verify --cores 2 --show-snippets:false --use-basename-for-filename --pipeline-profile "%t.baseline.jsonl" --pipeline-profile-heap --bprint "%t.baseline.bpl" --boogie /restartProver --log-format "json;LogFileName=%t.baseline.log.json" "%s" > "%t.baseline.out"
// RUN: %exits-with 4 %verify --cores 2 --show-snippets:false --use-basename-for-filename --pipeline-profile "%t.released.jsonl" --pipeline-profile-heap --bprint "%t.released.bpl" --boogie /restartProver --log-format "json;LogFileName=%t.released.log.json" --release-verified-modules "%s" > "%t.released.out"
// RUN: %diff "%t.baseline.out" "%t.released.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" bpl "%t.baseline" "%t.released" > "%t"
// RUN: python3 "%repositoryRoot/.github/review/retention-profile.py" check "%t.baseline.jsonl" "%t.released.jsonl" --verification-logs "%t.baseline.log.json" "%t.released.log.json" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Library {
  method Twice(x: int) returns (r: int) ensures r == 2 * x {
    r := x + x;
  }
}

module Client {
  import L = Library
  method Positive() {
    var r := L.Twice(3);
    assert r == 6;
  }
}

module Tail {
  import C = Client
  method Counterexample() {
    C.Positive();
    assert false;
  }
}
