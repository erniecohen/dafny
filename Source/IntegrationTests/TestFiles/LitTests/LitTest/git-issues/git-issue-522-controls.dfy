// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh --lemma-bindings --member-invariants --additional-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Tests of an independent type in a bound lemma are allowed. The invariant also
// has a concrete witness, and an inhabited false assertion must still fail.
module Independent {
  datatype U = U(n: int) { invariant n >= 0 }
  datatype T = T(n: int) { invariant Pick(this) }
  lemma Decide(u: raw U) returns (b: bool)
    requires u.n >= 0
    ensures b == (u is U)
  { b := u is U; }
  ghost function Pick(t: raw T): bool { var b := Decide(U(0)); b }
  lemma Witness() {
    var t: T := T(0);
    assert t.n == 0;
    assert Pick(t);
  }
}

// A member invariant with no binding behaves as before, even with both options.
module NoBinding {
  datatype T = T(n: int) { invariant Nonnegative(this) }
  ghost function Nonnegative(t: raw T): bool { t.n >= 0 }
  lemma Witness() {
    var t: T := T(0);
    assert t.n == 0;
  }
}

// A raw test only in the proof body does not define the binding's value.
module ProofHint {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { var b := Decide(t); b }
  lemma Decide(t: raw T) returns (b: bool)
    ensures b
  {
    var ignored := t is T;
    b := true;
  }
  lemma Witness() {
    var t: T := T(0);
    assert Pick(t);
  }
}

// Bindings with no invariant must not prove a false contract.
module NoInvariant {
  lemma Find(n: nat) returns (m: nat) ensures m > n { m := n + 1; }
  ghost function Above(n: nat): nat { var m := Find(n); m }
  lemma Positive(n: nat) ensures Above(n) > n {}
  lemma False() ensures false {
    var m := Above(0);
    assert m > 0;
  }
}
