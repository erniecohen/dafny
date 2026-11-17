// RUN: %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --unchecked-invariant-extension "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): the vacuity controls V5 and V5b
// of section 6.4 of docs/dev/issue105-member-invariants-plan.md, and V5c, a generic form of V5.  The
// invariants of Liar, Liar2 and Liar3 refer to the set of values of their own type, so the resolver
// rejects them (the extension condition, git-issue-105-extension.dfy).  Here the test-only
// --unchecked-invariant-extension turns the condition off, and the type's axioms are inconsistent: each
// 'assert false' verifies.  This records why the condition exists, and that the vacuity controls V1 and
// V4 (git-issue-105-datatypes.dfy and git-issue-105-generics.dfy) can tell the difference.  Each proof
// needs a value of the raw type to start from, and a case split on whether it satisfies the invariant.

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

// V5c, generic: P(true) is a P<X> exactly where X is empty, and a Liar3 needs a P<Liar3> holding true,
// so whether Liar3(P(true)) is a Liar3 is the liar's paradox again, through a type argument.
ghost predicate Yes<X>(x: X) { true }

datatype P<X(!new)> = P(b: bool) {
  invariant b <==> forall x: X :: !Yes(x)
}

datatype Liar3 = Liar3(p: P<Liar3>) {
  invariant p.b
}

method V5c() {
  ghost var r: raw Liar3 := Liar3(P(true));
  if r is Liar3 {
    ghost var w: Liar3 := r;
    assert w.p.b;
    assert Yes(w);
  } else {
    assert !(P(true) is P<Liar3>);
    ghost var x: Liar3 :| Yes(x);
    assert x.p.b;
  }
  assert false;
}
