// RUN: %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --unchecked-invariant-extension "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the vacuity controls V5 and V5b
// of section 6.4 of docs/dev/issue105-member-invariants-plan.md.  The invariants of Liar and Liar2
// refer to the set of values of their own type, so the resolver rejects them (the extension condition,
// git-issue-105-extension.dfy).  Here the test-only --unchecked-invariant-extension turns the condition
// off, and the type's axioms are inconsistent: each 'assert false' verifies.  This records why the
// condition exists, and that the vacuity controls V1 and V4 (git-issue-105-datatypes.dfy) can tell the
// difference.  Each proof needs a value of the raw type to start from, and a case split on whether it
// satisfies the invariant.

// Whether Liar(0) is a Liar is the liar's paradox.
datatype Liar = Liar(f: int) {
  invariant f == 0 ==> !(exists e: Liar :: e.f == 0)
}

method V5() {
  ghost var r: raw Liar := Liar(0);
  if r is Liar {
    assert false;
  } else {
    assert exists e: Liar :: e.f == 0;
    assert false;
  }
}

// A value is a Liar2 exactly when it is not.
datatype Liar2 = Liar2(f: int) {
  ghost predicate No() { !(this is Liar2) }
  invariant No()
}

method V5b() {
  ghost var r: raw Liar2 := Liar2(0);
  if r is Liar2 { assert r.No(); } else { assert r.No(); }
  assert false;
}
