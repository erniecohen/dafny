// RUN: %exits-with 4 %verify --type-system-refresh --solver-path "%review-z3" "%s" > "%t.first.out"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --verification-module-window 4 --translation-workers 1 --bprint "%t.n1.bpl" "%s" > "%t.n1.out"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --verification-module-window 4 --translation-workers 2 --bprint "%t.n2.bpl" "%s" > "%t.n2.out"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --verification-module-window 4 --translation-workers 4 --translation-workers-stress -1 --bprint "%t.rev.bpl" --pipeline-profile "%t.rev.jsonl" "%s" > "%t.rev.out"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --verification-module-window 4 --translation-workers 4 --translation-workers-stress 7 --bprint "%t.seed.bpl" "%s" > "%t.seed.out"
// RUN: %diff "%s.expect" "%t.n1.out"
// RUN: %diff "%t.n1.out" "%t.n2.out"
// RUN: %diff "%t.n1.out" "%t.rev.out"
// RUN: %diff "%t.n1.out" "%t.seed.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" bpl "%t.n1" "%t.n2" "%t.rev" "%t.seed" > "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" parallel "%t.rev.jsonl" >> "%t"
// RUN: %diff "%s.check.expect" "%t"
// A witness check must not set the origin of the shared constraint when substitution
// returns it unchanged. Importers translate that constraint too. Reverse scheduling
// lets them translate before the declaring module, whereas one worker translates it
// afterward. Compare all Boogie text, not just successful verification summaries.
module S {
  type Explicit = v: int | true witness 1
  type Inferred = v: int | true
  type Dependent = v: int | v > 0 witness 1
}
module U {
  import S
  lemma Positive(x: S.Explicit, y: S.Inferred, z: S.Dependent)
    ensures x == x && y == y && z > 0 {}
}
module V {
  import S
  lemma Positive(x: S.Explicit, y: S.Inferred) ensures x == x && y == y {}
}
module W {
  import S
  lemma Nonvacuous(x: S.Explicit, y: S.Inferred) { assert false; }
}
module Rejected {
  type Explicit = v: int | false witness 1
  type Inferred = v: int | false
  type Dependent = v: int | v > 0 witness 0
}
