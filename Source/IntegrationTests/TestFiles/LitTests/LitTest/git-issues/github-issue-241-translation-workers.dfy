// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 "%s" > "%t.n4.out"
// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 1 --bprint "%t.n1.bpl" "%s" > "%t.n1.out"
// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 2 --bprint "%t.n2.bpl" "%s" > "%t.n2.out"
// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 --translation-workers-stress -1 --bprint "%t.rev.bpl" --pipeline-profile "%t.rev.jsonl" "%s" > "%t.rev.out"
// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 --translation-workers-stress 7 --bprint "%t.s7.bpl" "%s" > "%t.s7.out"
// RUN: %exits-with 4 %verify --type-system-refresh --show-snippets:false --use-basename-for-filename --cores 2 --verification-module-window 4 --translation-workers 4 --translation-workers-stress -1 --boogie /typeEncoding:m --pipeline-profile "%t.fallback.jsonl" "%s" > "%t.fallback.out"
// RUN: %exits-with 2 %verify --type-system-refresh:false --translation-workers 2 "%s" > "%t.legacy.out"
// RUN: %exits-with 1 %verify --type-system-refresh --translation-workers 0 "%s" 2> "%t.zero.out"
// RUN: %diff "%s.expect" "%t.n1.out"
// RUN: %diff "%t.n1.out" "%t.n4.out"
// RUN: %diff "%t.n1.out" "%t.n2.out"
// RUN: %diff "%t.n1.out" "%t.rev.out"
// RUN: %diff "%t.n1.out" "%t.s7.out"
// RUN: %diff "%t.n1.out" "%t.fallback.out"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" bpl "%t.n1" "%t.n2" "%t.rev" "%t.s7" > "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" parallel "%t.rev.jsonl" >> "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" serial "%t.fallback.jsonl" >> "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" first-line "%t.legacy.out" >> "%t"
// RUN: python3 "%repositoryRoot/.github/review/translation-workers.py" first-line "%t.zero.out" >> "%t"
// RUN: %diff "%s.check.expect" "%t"

// --translation-workers N (https://github.com/erniecohen/dafny/issues/241, F1) translates up to N
// of the modules the module window admits at once, and commits them in the window's order. The
// output and every module's Boogie text are those of N = 1, also when the stress schedule makes
// translations finish in reverse order (-1) or at random (7). The profile shows that translations
// overlapped and committed in order; a Boogie type encoding other than Arguments falls back to
// one worker with the same output, and the legacy type system and N = 0 are rejected. The modules form a chain, a wide fan, a diamond and a refinement, and use exports,
// opaque functions, let-such-that, labels, old@L and fuel; M4 has a verification error and M9 a
// translation error.

module A0 {
  function F(n: nat): nat { if n == 0 then 0 else F(n - 1) + 1 }
  lemma P(n: nat) ensures F(n) == n { }
}

module A1 {
  import A0
  function G(n: nat): nat { A0.F(n) + 1 }
  lemma Q(n: nat) ensures G(n) == n + 1 { A0.P(n); }
}

module A2 {
  import A1
  lemma R(n: nat) ensures A1.G(n) > n { A1.Q(n); }
}

module W1 {
  import A0
  lemma S(n: nat) ensures A0.F(n) >= 0 { }
}

module W2 {
  import A0
  datatype D = X | Y(n: nat)
  function Size(d: D): nat { match d case X => 0 case Y(n) => A0.F(n) }
}

module W3 {
  import A0
  lemma T(a: int, b: int) requires 0 < a && 0 < b ensures a * b > 0 { }
}

module Diamond {
  import W1
  import W2
  lemma U(n: nat) ensures W2.Size(W2.Y(n)) >= 0 { W1.S(n); }
}

abstract module R0 {
  function H(): int
  lemma HPositive() ensures H() > 0
}

module R1 refines R0 {
  function H(): int { 7 }
  lemma HPositive() ensures H() > 0 { }
}

module E {
  export provides Secret, Hidden reveals Public
  opaque function Secret(x: int): int { x + 1 }
  function Hidden(): int { 3 }
  function Public(x: int): int { x * 2 }
  lemma SecretWorks(x: int) ensures Secret(x) > x { reveal Secret(); }
}

module EUser {
  import E
  lemma V(x: int) ensures E.Public(x) == x + x { }
}

module Lets {
  ghost function Pick(s: set<int>): int requires s != {} { var x :| x in s; x }
  method Choose() returns (y: int) ensures y > 10 {
    y :| y > 10;
  }
}

module Labels {
  class Cell { var v: int }
  method Bump(c: Cell) modifies c ensures c.v == old(c.v) + 2 {
    label L:
    c.v := c.v + 1;
    assert c.v == old@L(c.v) + 1;
    c.v := c.v + 1;
    assert Fact: c.v > old(c.v);
    reveal Fact;
  }
}

module M4 {
  lemma Bad(x: int) ensures x > 4 { }
}

module M9 {
  function f(n: nat): nat { if n == 0 then 0 else f(n - 1) }
  lemma {:fuel f, 3} Fuel() ensures true { assert {:fuel f, 1} f(1) == 0; }
}
