// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 %s > "%t.k1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --release-verified-modules %s > "%t.r1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --release-verified-modules --verification-module-window 4 %s > "%t.r4"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --release-verified-modules --filter-symbol=Client %s > "%t.f1"
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=4 --filter-symbol=Client %s > "%t.f0"
// RUN: %exits-with 1 %baredafny verify --use-basename-for-filename --release-verified-modules --filter-position-batch=:30 %s > "%t.rejected"
// RUN: %exits-with 1 %baredafny verify --use-basename-for-filename --release-verified-modules --verification-coverage-report "%t.coverage" %s >> "%t.rejected"
// RUN: %diff "%s.expect" "%t.k1"
// RUN: %diff "%t.k1" "%t.r1"
// RUN: %diff "%t.k1" "%t.r4"
// RUN: %diff "%t.f0" "%t.f1"
// RUN: %OutputCheck --file-to-check "%t.rejected" "%s"
// CHECK: --release-verified-modules cannot be combined with --filter-position-batch
// CHECK: --release-verified-modules cannot be combined with --verification-coverage-report

// --release-verified-modules (issue 241) drops the bodies of a module's methods, lemmas,
// constructors and iterators once the module is verified and reported.  The modules after it
// call those methods, construct its class, use its iterator, refine it and use its lemmas,
// reading only their specifications, so the output is the same as without the option.

module Library {
  class Counter {
    var n: nat
    constructor (start: nat) ensures n == start { n := start; }
    method Bump() modifies this ensures n == old(n) + 1 { n := n + 1; }
  }

  method Twice(x: int) returns (y: int) ensures y == 2 * x { y := x + x; }

  lemma Square(x: int) ensures x * x >= 0 { }

  least predicate Even(n: nat) { n == 0 || (n >= 2 && Even(n - 2)) }
  least lemma EvenIsEven(n: nat) requires Even(n) ensures n % 2 == 0 { }

  iterator Countdown(start: nat) yields (k: nat) {
    var i := start;
    while i > 0 { yield i; i := i - 1; }
  }
}

abstract module Spec {
  method Choose(x: int) returns (y: int) ensures y > x
}

module Impl refines Spec {
  method Choose(x: int) returns (y: int) ensures y > x { y := x + 1; }
}

module Client {
  import L = Library
  import I = Impl

  method Use() returns (r: int) ensures r == 10 {
    var c := new L.Counter(3);
    c.Bump();
    assert c.n == 4;
    r := L.Twice(5);
    L.Square(r);
    var it := new L.Countdown(2);
    var more := it.MoveNext();
  }

  method Wrong() returns (r: int) ensures r > 0 {
    r := I.Choose(-5);
  }
}
