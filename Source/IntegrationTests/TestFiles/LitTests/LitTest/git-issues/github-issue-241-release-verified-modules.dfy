// RUN: ! %verify --cores=4 %s > "%t.k1"
// RUN: ! %verify --cores=4 --release-verified-modules %s > "%t.r1"
// RUN: ! %verify --cores=4 --release-verified-modules --verification-module-window 4 --pipeline-profile "%t.r4.jsonl" %s > "%t.r4"
// RUN: ! %verify --cores=4 --release-verified-modules --verification-module-window 4 --translation-workers 4 --pipeline-profile "%t.t4.jsonl" %s > "%t.t4"
// RUN: ! %verify --cores=4 --release-verified-modules --filter-symbol=Client %s > "%t.f1"
// RUN: ! %verify --cores=4 --filter-symbol=Client %s > "%t.f0"
// RUN: %exits-with 1 %baredafny verify --use-basename-for-filename --release-verified-modules --filter-position-batch=:30 %s > "%t.rejected"
// RUN: %exits-with 1 %baredafny verify --use-basename-for-filename --release-verified-modules --verification-coverage-report "%t.coverage" %s >> "%t.rejected"
// RUN: %diff "%s.expect" "%t.k1"
// RUN: %diff "%t.k1" "%t.r1"
// RUN: %diff "%t.k1" "%t.r4"
// RUN: %diff "%t.k1" "%t.t4"
// RUN: python3 "%S/Inputs/github-issue-241-release-verified-modules.py" "%t.r4.jsonl"
// RUN: python3 "%S/Inputs/github-issue-241-release-verified-modules.py" "%t.t4.jsonl" parallel
// RUN: %diff "%t.f0" "%t.f1"
// RUN: %OutputCheck --file-to-check "%t.rejected" "%s"
// CHECK: --release-verified-modules cannot be combined with --filter-position-batch
// CHECK: --release-verified-modules cannot be combined with --verification-coverage-report

// --release-verified-modules (issue 241) drops the bodies of a module's methods, lemmas,
// constructors and iterators once the module is verified and reported.  The modules after it
// call those methods, construct its class, use its iterator, refine it and use its lemmas,
// reading only their specifications, so the output is the same as without the option.
// The modules Late1 to Late4 come after more modules than a window of 4 admits, so they are
// translated after earlier modules are released; the profile check makes sure of it.

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

module Late1 {
  import L = Library
  import I = Impl

  method Use1() returns (r: int) ensures r == 2 {
    var c := new L.Counter(1);
    c.Bump();
    r := L.Twice(1);
    L.Square(r);
    var it := new L.Countdown(1);
    var more := it.MoveNext();
    var y := I.Choose(1);
    assert y > 1;
  }

  lemma Even1(n: nat) requires L.Even(n) ensures n % 2 == 0 {
    L.EvenIsEven(n);
  }
}

module Late2 {
  import L = Library
  import I = Impl

  method Use2() returns (r: int) ensures r == 4 {
    var c := new L.Counter(2);
    c.Bump();
    r := L.Twice(2);
    L.Square(r);
    var it := new L.Countdown(2);
    var more := it.MoveNext();
    var y := I.Choose(2);
    assert y > 2;
  }

  lemma Even2(n: nat) requires L.Even(n) ensures n % 2 == 0 {
    L.EvenIsEven(n);
  }
}

module Late3 {
  import L = Library
  import I = Impl

  method Use3() returns (r: int) ensures r == 6 {
    var c := new L.Counter(3);
    c.Bump();
    r := L.Twice(3);
    L.Square(r);
    var it := new L.Countdown(3);
    var more := it.MoveNext();
    var y := I.Choose(3);
    assert y > 3;
  }

  lemma Even3(n: nat) requires L.Even(n) ensures n % 2 == 0 {
    L.EvenIsEven(n);
  }
}

module Late4 {
  import L = Library
  import I = Impl

  method Use4() returns (r: int) ensures r == 8 {
    var c := new L.Counter(4);
    c.Bump();
    r := L.Twice(4);
    L.Square(r);
    var it := new L.Countdown(4);
    var more := it.MoveNext();
    var y := I.Choose(4);
    assert y > 4;
  }

  lemma Even4(n: nat) requires L.Even(n) ensures n % 2 == 0 {
    L.EvenIsEven(n);
  }
}
