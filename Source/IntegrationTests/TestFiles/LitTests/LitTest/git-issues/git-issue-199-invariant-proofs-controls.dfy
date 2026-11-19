// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings --member-invariants --additional-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 199, with --member-invariants: what the proofs in the cone of an invariant may still do.

// A proof that justifies a binding may use values, tests and heap locations of types that do not
// depend on T, choose values of such types, and call lemmas and proof hints that do the same.
module Independent {
  datatype U = U(n: int) { invariant n >= 0 }
  class C {
    var g: int
    var u: U
    constructor () { g := 0; u := U(0); }
  }
  datatype T = T(n: int, c: C) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { Hint(t.n); var b := Pick(t.n, t.c); b }
  lemma Hint(n: int) ensures n == n { var r: raw U := U(1); assert r is U; }
  lemma Pick(n: int, c: C) returns (b: bool) ensures b == (n == n) {
    var k: int;
    var j: int :| j == n;
    forall i: int ensures P(i) {}
    assert P(n);
    assert exists i: int :: P(i);
    var x := c.g;
    var u := c.u;
    var r: raw U := U(n);
    var isU := r is U;
    var y := Id<int>(n);
    Helper(n);
    b := true;
  }
  lemma Id<X>(x: X) returns (y: X) ensures y == x { y := x; }
  lemma Helper(n: int) ensures n == n {}
  ghost predicate P(i: int) { true }
  method Inhabited() {
    var c := new C();
    var t: T := T(0, c);
    assert Ok(t);
    assert false; // error: the invariant is satisfiable, so this must still fail
  }
}

// A lemma with a result of type T may be bound where the invariant does not reach.
module OutsideCone {
  datatype T = T(n: int) { invariant n >= 0 }
  lemma Pick() returns (y: T) { y := T(0); }
  ghost function Elsewhere(): int { var y := Pick(); y.n }
}

// Without a binding in the cone, proof hints in it are not followed, as before.
module NoBinding {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { H(); t.n >= 0 }
  lemma H() ensures true { var r: raw T := T(0); if r is T {} }
}
