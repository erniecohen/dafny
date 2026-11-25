// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 1 --translation-workers-stress -1 --pipeline-profile "%t.n1.jsonl" "%s" > "%t.n1.out"
// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 --translation-workers-stress -1 --pipeline-profile "%t.rev.jsonl" "%s" > "%t.rev.out"
// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 --translation-workers-stress 7 --pipeline-profile "%t.s7.jsonl" "%s" > "%t.s7.out"
// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 2 --translation-workers-stress 3 --pipeline-profile "%t.n2.jsonl" "%s" > "%t.n2.out"
// RUN: rm -rf "%t.cache1" "%t.cache4"
// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 1 --translation-workers-stress -1 --persistent-translation --verification-cache "%t.cache1" --pipeline-profile "%t.p1.jsonl" "%s" > "%t.p1.out"
// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 --translation-workers-stress -1 --persistent-translation --verification-cache "%t.cache4" --pipeline-profile "%t.p4.jsonl" "%s" > "%t.p4.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" shifted "%s" "%t.shifted.dfy"
// RUN: %verify --type-system-refresh --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 1 --translation-workers-stress -1 --pipeline-profile "%t.shifted.jsonl" "%t.shifted.dfy" > "%t.shifted.out"
// RUN: %diff "%s.expect" "%t.n1.out"
// RUN: %diff "%t.n1.out" "%t.rev.out"
// RUN: %diff "%t.n1.out" "%t.s7.out"
// RUN: %diff "%t.n1.out" "%t.n2.out"
// RUN: %diff "%t.n1.out" "%t.p1.out"
// RUN: %diff "%t.n1.out" "%t.p4.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" tokens "%t.n1.jsonl" "%t.rev.jsonl" "%t.s7.jsonl" "%t.n2.jsonl" "%t.p1.jsonl" "%t.p4.jsonl" > "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" persistent "%t.p1.jsonl" "%t.p4.jsonl" >> "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" tokens-moved "%t.n1.jsonl" "%t.shifted.jsonl" >> "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" parallel "%t.rev.jsonl" >> "%t"
// RUN: %diff "%s.check.expect" "%t"

// https://github.com/erniecohen/dafny/issues/569: the witness check of a subset type or newtype
// substitutes its witness into the constraint and sets the origin of the result. A constraint
// that does not mention its bound variable, like "true", is returned by the substitution itself,
// so the check used to set the origin of the declaration's own constraint. Every module that
// imports S translates that constraint, and under --translation-workers it did so while S's
// translation could still be changing it: the tokens of U1..U4's Boogie graphs, which the printed
// Boogie text omits, depended on whether S's witness check had run. The check now clones a shared
// constraint first. A second instance of the same race: the termination check of a decreases
// clause over an iset compares it by "false", the shared Boogie literal, and used to set that
// literal's token, which every later module's graph shares; D's check now uses a fresh literal.
//
// The profile of a run under --translation-workers-stress records a digest of each module's tokens.
// Those digests are the same at N = 1, 2 and 4 and under the reverse-order (-1) and seeded (7)
// schedules, which delay S, the first ticket, so that its importers translate before it, and on the
// path that records translations for --persistent-translation (issue 261), which every module of
// the two persistent runs must take. Removing either fix makes some digests differ between
// N = 1 and N = 4, on both paths. The digests of a copy one line further down all differ (the
// digest sees source positions), and the reverse-order profile must show that translations
// overlapped.

module S {
  type T = v: int | true witness 1
  type Z = v: int | true
  newtype N = x: int | true witness 2
}

module D {
  method Loop(s: iset<int>, n: nat) decreases s, n {
    if n > 0 { Loop(s, n - 1); }
  }
}

module U1 {
  import S
  lemma L(t: S.T, z: S.Z) ensures t == t && z == z {}
}

module U2 {
  import S
  method M(y: int) returns (t: S.T) { t := y; }
}

module U3 {
  import S
  function F(n: S.N): S.N { n }
}

module U4 {
  import S
  lemma L4(t: S.T, n: S.N) ensures t as int == t as int {}
}
