// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --additional-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 549, controls: the proofs in the cone of an invariant may still do everything that does not depend on T.

module Independent {
  datatype U = U(n: int) {
    invariant n >= 0
    ghost witness U(0)
  }
  class C {
    var g: int
    constructor () { g := 0; }
  }
  trait Oracle extends object {
    lemma Ok(n: int) ensures n == n
  }
  datatype T = T(n: int, c: C) {
    invariant Inv(this)
  }
  // Hints that use values, tests, choices, quantifiers and heap reads of types that do not depend on T, and lemmas
  // that do the same.  A raw value of T may be used without testing it against T.
  ghost predicate Inv(t: raw T) ensures Inv(t) ==> t.n >= 0 {
    assert t.n * t.n >= 0 by { Square(t.n); }
    var r: raw U := U(t.n);
    assert r is U ==> r.n >= 0;
    var k: int :| k == t.n;
    assert forall u: U :: u.n >= 0;
    calc { t.n + 0; == t.n; }
    Pick(t.c);
    var raw0: raw T := t;
    assert raw0.n == t.n;
    t.n >= 0
  }
  lemma Square(n: int) ensures n * n >= 0 { }
  lemma Pick(c: C) {
    var u: U :| true;
    var x := c.g;
    forall i: int ensures Q(i) { }
  }
  ghost predicate Q(i: int) { true }
  method Inhabited() {
    var c := new C();
    var t: T := T(0, c);
    assert t.n >= 0;
    assert false; // error: the invariant is satisfiable, so this must still fail
  }
  // A function outside the cone may use T in its hints, and call dynamically bound lemmas.
  ghost function Outside(t: T, o: Oracle): int { assert t is T; o.Ok(t.n); var x: T :| x == t; x.n }
}
