// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// A downcast of a general trait's value to a datatype with invariants checks the invariants of the unboxed value
// (https://github.com/erniecohen/dafny/issues/550). It used to stop the verifier with a Boogie type error. The
// valid downcasts are in git-issue-550-trait-downcast-controls.dfy.

trait G { const n: int  invariant true }
datatype D extends G = D(m: int) {
  invariant m > 0
}
datatype E extends G = E(k: int) {
  invariant k < 0
}

// The reproducer: a raw value that breaks the invariant of D reaches G, and is cast down to D
lemma False() ensures false {
  var r: raw D := D(0);
  var rg: raw G := r;
  var g: G := rg as G;
  var d := g as D; // error: invariant might not hold
} // error: postcondition

// A value of another type that extends G is not a D
method Other(e: E) {
  var g: G := e;
  var d := g as D; // error: not a raw D
}
