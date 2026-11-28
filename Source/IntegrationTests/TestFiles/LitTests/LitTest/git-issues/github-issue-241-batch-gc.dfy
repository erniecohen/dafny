// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /restartProver --boogie /deterministicLiteralHashes:1  --bprint "%t.default.bpl" --pipeline-profile "%t.default.jsonl" --log-format "json;LogFileName=%t.default.json" "%s" > "%t.default.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /restartProver --boogie /deterministicLiteralHashes:1 --batch-gc --bprint "%t.on.bpl" --pipeline-profile "%t.on.jsonl" --log-format "json;LogFileName=%t.on.json" "%s" > "%t.on.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /restartProver --boogie /deterministicLiteralHashes:1 --batch-gc=false --bprint "%t.off.bpl" --pipeline-profile "%t.off.jsonl" --log-format "json;LogFileName=%t.off.json" "%s" > "%t.off.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /restartProver --boogie /deterministicLiteralHashes:1  --bprint "%t.project.bpl" --pipeline-profile "%t.project.jsonl" --log-format "json;LogFileName=%t.project.json" "%S/Inputs/github-issue-241-batch-gc/dfyconfig.toml" > "%t.project.out"
// RUN: %diff "%s.expect" "%t.default.out"
// RUN: %diff "%t.default.out" "%t.on.out"
// RUN: %diff "%t.default.out" "%t.off.out"
// RUN: %diff "%t.default.out" "%t.project.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" bpl "%t.default" "%t.on" "%t.off" "%t.project" > "%t.check"
// RUN: python3 "%repositoryRoot/.github/review/batch-gc.py" "%t.default" "%t.on" "%t.off" "%t.project" >> "%t.check"
// RUN: %diff "%s.check.expect" "%t.check"

// Batch GC changes runtime scheduling, while the two modules retain their
// obligations, verdicts and per-VC resource counts. The profile proves the
// option actually ran, and assert false remains a reachable failing check.

module Library {
  function Increment(x: int): int { x + 1 }
  lemma Positive(x: int) ensures Increment(x) > x { }
}

module Client {
  import L = Library
  lemma Positive(x: int) ensures L.Increment(x) > x { L.Positive(x); }
  lemma Counterexample() { assert false; }
}
