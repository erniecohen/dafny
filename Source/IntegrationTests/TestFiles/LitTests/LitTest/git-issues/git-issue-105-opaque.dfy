// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) that a proof must invoke explicitly:
// an invariant that calls an opaque predicate (question 15 of section 8 of
// docs/dev/issue105-member-invariants-plan.md).  The conjunct is the atom P(v): it is checked wherever a
// value of the type is formed and assumed wherever one is used, and P's body unfolds only under
// `reveal P()`.  The invariant itself is never opaque (git-issue-105-opacity).

module Opaque {
  opaque predicate Even(x: int) { x % 2 == 0 }

  datatype D = D(n: int) {
    invariant Even(n)
    witness (reveal Even(); D(0))
  }

  method MakeHidden() returns (d: D) {
    d := D(4); // error: the body of Even is hidden, so the atom cannot be proved
  }
  method MakeRevealed() returns (d: D) {
    reveal Even();
    d := D(4);
  }
  method MakeOdd() returns (d: D) {
    reveal Even();
    d := D(3); // error: the body is false
  }
  function FromExpr(): D {
    reveal Even(); D(6)
  }
  method UseAtom(d: D) {
    assert Even(d.n); // the atom, without the body
  }
  method UseHidden(d: D) {
    assert d.n % 2 == 0; // error: the body is hidden
  }
  method UseRevealed(d: D) {
    reveal Even();
    assert d.n % 2 == 0;
  }
}

// An opaque member predicate: the same, with the member's reveal.
module Member {
  datatype D = D(n: int) {
    opaque predicate Valid() { n % 2 == 0 }
    invariant Valid()
  }
  method Make() returns (d: D) {
    reveal D.Valid();
    d := D(4);
  }
  method MakeHidden() returns (d: D) {
    d := D(4); // error: the body of Valid is hidden
  }
  method UseHidden(d: D) {
    assert d.n % 2 == 0; // error: the body is hidden
  }
  method UseRevealed(d: D) {
    reveal d.Valid();
    assert d.n % 2 == 0;
  }
}

// An opaque predicate that an object's invariant calls is a raw member, so the constructor can reveal it
// before the implicit new;.
module RawMember {
  class C {
    const k: int
    opaque predicate Pos() { k > 0 }
    invariant Pos()
    constructor () { k := 1; reveal Pos(); }
    constructor Bad() { k := 0; reveal Pos(); } // error at the implicit new;: the body is false
  }
  method Use(c: C) {
    reveal c.Pos();
    assert c.k > 0;
  }
}

// hide: an ordinary predicate hidden at a site behaves as an opaque one there.
module Hide {
  predicate Even(x: int) { x % 2 == 0 }
  datatype E = E(n: int) {
    invariant Even(n)
  }
  method MakeHidden() returns (e: E) {
    hide Even;
    e := E(4); // error: the body of Even is hidden
  }
  method MakeShown() returns (e: E) {
    e := E(4);
  }
  method UseHidden(e: E) {
    hide Even;
    assert e.n % 2 == 0; // error: the body is hidden
  }
  method UseShown(e: E) {
    assert e.n % 2 == 0;
  }
}

// A newtype, whose witness reveals the body; and variance, which looks through an opaque body (the body is
// the predicate's meaning; opacity concerns only proofs).
module NewtypesAndVariance {
  opaque predicate Even(x: int) { x % 2 == 0 }
  newtype N = int {
    invariant Even(this as int)
    witness (reveal Even(); 0)
  }
  method Make() returns (n: N) {
    reveal Even();
    n := 6;
  }
  method Use(n: N) {
    reveal Even();
    assert (n as int) % 2 == 0;
  }

  opaque predicate NonEmpty<X>(xs: seq<X>) { |xs| > 0 }
  datatype S<+X> = S(xs: seq<X>) {
    invariant NonEmpty(xs)
  }
  method Up(s: S<nat>) returns (t: S<int>) {
    t := s;
  }
}
