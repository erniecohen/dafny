// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 %s > "%t.k1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --verification-module-window 4 %s > "%t.k4"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --verification-module-window 16 %s > "%t.k16"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --verification-module-window 4 --verification-module-window-memory 1 %s > "%t.m1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --verification-module-window 4 --filter-position-batch=:43 --filter-position-batch=:27 --filter-position-batch=:22 %s > "%t.b4"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --filter-position-batch=:43 --filter-position-batch=:27 --filter-position-batch=:22 %s > "%t.b1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --warn-redundant-assumptions --warn-contradictory-assumptions --suggest-proof-refactoring %s > "%t.d1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --warn-redundant-assumptions --warn-contradictory-assumptions --suggest-proof-refactoring --verification-module-window 4 %s > "%t.d4"
// RUN: %diff "%s.expect" "%t.k1"
// RUN: %diff "%t.k1" "%t.k4"
// RUN: %diff "%t.k1" "%t.k16"
// RUN: %diff "%t.k1" "%t.m1"
// RUN: %diff "%t.b1" "%t.b4"
// RUN: %diff "%t.d1" "%t.d4"

// --verification-module-window K (issue 241) overlaps the verification of up to K
// modules, and reports what K = 1 reports, in the same order: here, verification
// errors in M1 and M4, which finish after later modules, and a translation error
// in M3, which is translated while M1 and M2 are still being verified.

module M0 {
  function F(n: nat): nat { if n == 0 then 0 else F(n - 1) + 1 }
  lemma P(n: nat) ensures F(n) == n { }
}

module M1 {
  lemma Slow(a: int, b: int, c: int) requires 0 < a && 0 < b ensures a * b * c * c >= 0 && a * b > c { }
  lemma Fine(x: int) ensures x + 1 > x { }
}

module M2 {
  lemma Nonlinear(a: int, b: int, c: int) requires 0 < a && 0 < b && 0 < c ensures a * b * c > 0 {
    assert a * b > 0;
  }
}

module M3 {
  function f(n: nat): nat { if n == 0 then 0 else f(n - 1) }
  lemma {:fuel f, 3} Fuel() ensures true { assert {:fuel f, 1} f(1) == 0; }
}

module M4 {
  lemma Bad(x: int) ensures x > 4 { }
}

module M5 { }

module M6 {
  datatype D = A | B
  lemma Cases(d: D) ensures d == A || d == B { }
}
