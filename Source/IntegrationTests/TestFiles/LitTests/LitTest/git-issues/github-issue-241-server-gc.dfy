// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --bprint "%t.default.bpl" --pipeline-profile "%t.default.jsonl" --log-format "json;LogFileName=%t.default.json" "%s" > "%t.default.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --batch-gc --bprint "%t.batch.bpl" --pipeline-profile "%t.batch.jsonl" --log-format "json;LogFileName=%t.batch.json" "%s" > "%t.batch.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --batch-gc --server-gc-memory 1024 --verification-module-window 4 --verification-module-window-memory 1024 --bprint "%t.on.bpl" --pipeline-profile "%t.on.jsonl" --log-format "json;LogFileName=%t.on.json" "%s" > "%t.on.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --batch-gc --server-gc-memory 0 --bprint "%t.off.bpl" --pipeline-profile "%t.off.jsonl" --log-format "json;LogFileName=%t.off.json" "%s" > "%t.off.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --boogie /restartProver --bprint "%t.project.bpl" --pipeline-profile "%t.project.jsonl" --log-format "json;LogFileName=%t.project.json" "%S/Inputs/github-issue-241-server-gc/dfyconfig.toml" > "%t.project.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --batch-gc --verification-module-window 4 --verification-module-window-memory 1024 --bprint "%t.pooled-base.bpl" --pipeline-profile "%t.pooled-base.jsonl" --log-format "json;LogFileName=%t.pooled-base.json" "%s" > "%t.pooled-base.out"
// RUN: %exits-with 4 %verify-process --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --boogie /deterministicLiteralHashes:1 --batch-gc --server-gc-memory 1024 --verification-module-window 4 --verification-module-window-memory 1024 --bprint "%t.pooled-on.bpl" --pipeline-profile "%t.pooled-on.jsonl" --log-format "json;LogFileName=%t.pooled-on.json" "%s" > "%t.pooled-on.out"
// RUN: %diff "%s.expect" "%t.default.out"
// RUN: %diff "%t.default.out" "%t.batch.out"
// RUN: %diff "%t.default.out" "%t.on.out"
// RUN: %diff "%t.default.out" "%t.off.out"
// RUN: %diff "%t.default.out" "%t.project.out"
// RUN: %diff "%t.default.out" "%t.pooled-base.out"
// RUN: %diff "%t.default.out" "%t.pooled-on.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" bpl "%t.default" "%t.batch" "%t.on" "%t.off" "%t.project" "%t.pooled-base" "%t.pooled-on" > "%t.check"
// RUN: python3 "%repositoryRoot/.github/review/server-gc.py" "%t.default" "%t.batch" "%t.on" "%t.off" "%t.project" "%t.pooled-base" "%t.pooled-on" >> "%t.check"
// RUN: %diff "%s.check.expect" "%t.check"

// The positive proofs and reachable false assertion retain identical obligations,
// verdicts and resource counts. Runtime events establish bounded activation.

module Library {
  function Increment(x: int): int { x + 1 }
  lemma Positive(x: int) ensures Increment(x) > x { }
}

module Client {
  import L = Library
  lemma Positive(x: int) ensures L.Increment(x) > x { L.Positive(x); }
  lemma Counterexample() { assert false; }
}
