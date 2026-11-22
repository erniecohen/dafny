// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --legacy-unchecked-member-invariant-proofs "%s" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --legacy-unchecked-member-invariant-proofs "%S/git-issue-549-invariant-hints.dfy" >> "%t"
// RUN: %exits-with 2 %verify --type-system-refresh --lemma-bindings --member-invariants --legacy-unchecked-member-invariant-proofs "%S/git-issue-199-invariant-proofs.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 549, vacuity control.  The reproducers of the issue: in each module, a proof hint in a function of the
// invariant's cone learns T's invariant from a value of type T, and so assumes the axiom it is meant to justify.  By
// default the extension condition rejects each hint (the first run).  The hidden
// --legacy-unchecked-member-invariant-proofs restores the former condition, which does not follow the hints, and then
// every lemma False below verifies (the second run): the rejections are what keeps 'ensures false' from verifying.
// The last two runs pin the legacy output of the issue's other reproducers, and of issue 199's, to that of the
// former condition: with the option, git-issue-549-invariant-hints.dfy verifies every module but DynamicLemma, and
// git-issue-199-invariant-proofs.dfy reports only the error that the former condition reported.
// The newtype reproducer, where the value is a literal of the base type, is rejected because such a literal is a raw
// value (https://github.com/erniecohen/dafny/pull/548).

// The function's postcondition.
module Ensures {
  datatype T = T(n: int) {
    invariant Inv(this)
  }
  ghost predicate Inv(t: raw T) ensures !Inv(t) { G(T(0)); true }
  lemma G(x: T) ensures false { }
  lemma False() ensures false { G(T(0)); }
}

// The function's termination.
module Termination {
  datatype T = T(n: int) {
    invariant F(this) == 0
  }
  ghost function F(t: raw T): int { G(T(0)); F(t) + 1 }
  lemma G(x: T) ensures false { }
  lemma False() ensures false { G(T(0)); }
}

// The precondition of a function that it calls.
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
  ghost predicate Inv(t: raw T) ensures !Inv(t) {
    assert false by { var x: T :| true; }
    true
  }
  lemma False() ensures false { var x: T :| true; }
}

// A literal of the base type, converted to the newtype in a hint.
module Newtype {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  ghost predicate Inv(x: raw N) ensures !Inv(x) { G(0); true }
  lemma G(x: N) ensures false { }
  lemma False() ensures false { G(0); }
}
