// Front-end events of --pipeline-profile (https://github.com/erniecohen/dafny/issues/241, milestone D1):
// parsing per file, the module graph, each module's resolution with its phases, the global passes
// and translation per module, for `dafny resolve` and `dafny verify`.  Output is byte-identical
// with and without the profile, and with the heap and GC-event modes.  Boogie's no-op solver
// keeps the solver out: every proof is inconclusive, and the output does not depend on one.
// RUN: %resolve --show-snippets:false --use-basename-for-filename "%s" > "%t.off"
// RUN: %resolve --show-snippets:false --use-basename-for-filename --pipeline-profile "%t.resolve.jsonl" "%s" > "%t.on"
// RUN: %diff "%t.off" "%t.on"
// RUN: python3 "%repositoryRoot/.github/review/frontend-profile.py" check "%t.resolve.jsonl" > "%t"
// RUN: %exits-with 4 %verify --show-snippets:false --use-basename-for-filename --solver-option SOLVER=noop "%s" > "%t.voff"
// RUN: %exits-with 4 %verify --show-snippets:false --use-basename-for-filename --solver-option SOLVER=noop --pipeline-profile "%t.verify.jsonl" --pipeline-profile-heap --pipeline-profile-gc-events "%s" > "%t.von"
// RUN: %diff "%t.voff" "%t.von"
// RUN: python3 "%repositoryRoot/.github/review/frontend-profile.py" check "%t.verify.jsonl" >> "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %resolve --show-snippets:false --use-basename-for-filename "%S/Inputs/github-issue-241-frontend-exports.dfy" > "%t.exports.off"
// RUN: %resolve --show-snippets:false --use-basename-for-filename --pipeline-profile "%t.exports.jsonl" "%S/Inputs/github-issue-241-frontend-exports.dfy" > "%t.exports.on"
// RUN: %diff "%t.exports.off" "%t.exports.on"
// RUN: python3 "%repositoryRoot/.github/review/frontend-profile.py" check-phases "%t.exports.jsonl"

module A {
  function Sum(n: nat): nat { if n == 0 then 0 else n + Sum(n - 1) }
}

module B {
  import A
  module Inner {
    predicate P(x: int) { x == x }
  }
  lemma L(n: nat) ensures A.Sum(n) >= n { }
}

abstract module C {
  import I : B
}
