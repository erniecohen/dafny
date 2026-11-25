// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): statements inside expressions
// (an assert, a lemma call, a dynamically bound lemma call) are proof hints.  They are not part of an
// invariant's meaning, but the extension condition looks at them, and at the lemmas they call, because
// the proof of a function in the cone justifies its axioms (issue 549, section 10 of
// docs/dev/issue105-member-invariants-plan.md).  Each statement below refers to the set of values of
// its own type in a way the condition rejects: a quantifier without a bound, a test of the raw 'this',
// a lemma that does both, a dynamically bound call.

module Statements {
  // A quantifier over D without a bound, proved from the type's axioms
  datatype D = D(x: int) {
    invariant x >= 0
    invariant (assert forall e: D :: e.x >= 0; x < 1000)  // error: refers to the set of values of D
  }

  // A test of the raw 'this' against E, proved from the type's axioms
  datatype E = E(x: int) {
    invariant x != 7
    invariant (assert this is E ==> x != 7; true)  // error: tests a raw value against E
  }

  // A lemma whose specification quantifies over F and tests a raw value against it
  lemma AllNonnegative(f: raw F)
    ensures f is F ==> f.x >= 0 && forall e: F :: e.x >= 0  // error (twice): a raw test, and the set of values of F
  {
  }
  datatype F = F(x: int) {
    invariant x >= 0
    invariant (AllNonnegative(this); true)
  }

  // Section 3.3's Liar, as a statement
  datatype Liar = Liar(f: int) {
    invariant f == 0 ==> (assert !(exists e: Liar :: e.f == 0); true)  // error: refers to the set of values of Liar
  }

  // Liar2, as a statement
  datatype Liar2 = Liar2(f: int) {
    invariant (assert !(this is Liar2); true)  // error: tests a raw value against Liar2
  }

  // Liar3 of section 3.3, as a dynamically bound lemma call
  trait Oracle extends object {
    lemma Ok(n: int)
  }
  datatype Liar3 = Liar3(f: int, o: Oracle) {
    invariant (o.Ok(f); true)  // error: calls a dynamically bound lemma
  }
  class Teller extends Oracle {
    lemma Ok(n: int)
      ensures !(Liar3(n, this) is Liar3)
    {
    }
  }

  method Use(d: D, e: E, f: F, g: Liar, h: Liar2) {
    assert 0 <= d.x < 1000 && e.x != 7 && f.x >= 0;
    var l: Liar := Liar(0);
    var m: Liar2 := Liar2(0);
  }

  lemma Vacuity()
    ensures false
  {
  }
}
