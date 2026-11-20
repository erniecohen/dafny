// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --additional-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 549: the proofs inside the functions of a member invariant's cone justify the axioms of those functions (their
// postconditions, their termination and the preconditions of their calls), so they are in the cone, with or without a
// lemma binding.  Without that, each module below proves false (the lemma False, or a lemma that ensures false),
// except where noted.  Each hint learns that some value has type T, and so T's invariant, which the axioms it is meant
// to justify are about.  The newtype form of the issue, where the value is a literal of the base type, also needs the
// rule that such a literal is a raw value.

// A lemma call in a hint establishes the function's postcondition.
module Ensures {
  datatype T = T(n: int) {
    invariant Inv(this)
  }
  ghost predicate Inv(t: raw T) ensures !Inv(t) { G(T(0)); true }
  lemma G(x: T) ensures false { }
  lemma False() ensures false { G(T(0)); }
}

// ... the function's termination
module Termination {
  datatype T = T(n: int) {
    invariant F(this) == 0
  }
  ghost function F(t: raw T): int { G(T(0)); F(t) + 1 }
  lemma G(x: T) ensures false { }
  lemma False() ensures false { G(T(0)); }
}

// ... the precondition of a function it calls
module CalleePrecondition {
  datatype T = T(n: int) {
    invariant P(this)
  }
  ghost predicate P(t: raw T) { G(T(1)); Q(t) }
  ghost predicate Q(t: raw T) requires false ensures false { true }
  lemma G(x: T) ensures false { }
  lemma False() ensures false { G(T(1)); }
}

// No lemma at all: a ":|" in an "assert by" chooses a value of T, which has a witness.
module SuchThatInHint {
  datatype T = T(n: int) {
    invariant Inv(this)
    ghost witness T(0)
  }
  ghost predicate Inv(x: raw T) ensures !Inv(x) {
    assert false by { var x: T :| true; }
    true
  }
  lemma False() ensures false { var x: T :| true; }
}

// The same through an "assert by" whose proof tests a raw value against T.
module AssertBy {
  datatype T = T(n: int) {
    invariant Inv(this)
  }
  ghost predicate Inv(t: raw T) ensures !Inv(t) {
    assert false by { var r: raw T := T(0); assert r is T; }
    true
  }
}

// A hint in a function that the invariant reaches only through another function.
module Indirect {
  datatype T = T(n: int) {
    invariant Inv(this)
  }
  ghost predicate Inv(t: raw T) { Helper(t.n) }
  ghost predicate Helper(n: int) ensures !Helper(n) { G(T(0)); true }
  lemma G(x: T) ensures false { }
}

// A dynamically bound lemma: the proofs of its overrides are not all known.  This alone does not prove false: an
// override that uses the invariant closes a cycle that the termination checks reject, in the module and across
// modules.  It is rejected anyway, as a dynamically bound function is (clause 4).
module DynamicLemma {
  trait Oracle extends object {
    lemma Ok() ensures false
  }
  datatype T = T(n: int, o: Oracle) {
    invariant Inv(this)
  }
  ghost predicate Inv(t: raw T) ensures !Inv(t) { t.o.Ok(); true }
  class Teller extends Oracle {
    lemma Ok() ensures false { var t: T := T(0, this); }
  }
}
