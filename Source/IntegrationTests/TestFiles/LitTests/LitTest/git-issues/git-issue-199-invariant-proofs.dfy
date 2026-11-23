// RUN: %exits-with 2 %verify --type-system-refresh --lemma-bindings --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %verify --type-system-refresh --lemma-bindings --member-invariants --additional-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 199, with --member-invariants: a binding in the cone of an invariant also brings in the proofs that
// justify it.  Without these checks, each module below proves false (the lemma False, or a lemma that ensures
// false), except where noted.  Each one learns the binding's own results by learning that some value has type T.

// The result of a binding is a value of T.  Learning that it is in T contradicts the invariant.
module ResultType {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var y := Pick(); t.n != y.n }
  lemma Pick() returns (y: T) { y := T(0); }
  lemma False() ensures false { var y := Pick(); assert Ok(y); }
}

// The same, through a type argument, a collection and an argument converted to T.
module GenericResult {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var y := Pick<T>(T(1)); t.n != y.n }
  lemma Pick<X>(x: X) returns (y: X) { y := x; }
  lemma False() ensures false { var y := Pick<T>(T(1)); assert Ok(y); }
}

module SequenceResult {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var s := Pick(); |s| == 1 && t.n != s[0].n }
  lemma Pick() returns (s: seq<T>) ensures |s| == 1 { s := [T(0)]; }
  lemma False() ensures false { var s := Pick(); assert Ok(s[0]); }
}

// The specification says nothing about T, but the body tests a raw value against T.  That test
// unfolds the invariant, and with it the binding's postcondition, which the body is meant to justify.
module BodyTest {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var b := Pick(); b }
  lemma Pick() returns (b: bool) ensures false {
    var t: raw T := T(0);
    b := t is T;
  }
}

// The test of issue 522's controls, with a false postcondition.
module BodyTestOfArgument {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { var b := Decide(t); b }
  lemma Decide(t: raw T) returns (b: bool) ensures false {
    var ignored := t is T;
    b := true;
  }
}

// The body calls a lemma whose body tests against T.
module CalledLemma {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var b := Pick(); b }
  lemma Pick() returns (b: bool) ensures false { Helper(); b := true; }
  lemma Helper() ensures false { var r: raw T := T(0); if r is T {} }
}

// A proof hint establishes the binding's precondition, and the hint's lemma tests against T.
module Hint {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { H(); var b := Pick(0); b }
  lemma Pick(n: int) returns (b: bool) requires n != n ensures false { }
  lemma H() ensures 0 != 0 { var t: raw T := T(0); if t is T {} }
  lemma False() ensures false { H(); }
}

// The heap says that a field of type T holds a value of T.
module HeapRead {
  class C { var f: T  constructor (t: T) { f := t; } }
  datatype T = T(n: int, c: C) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var b := Pick(t.c); b }
  lemma Pick(c: C) returns (b: bool) ensures false { b := c.f.n == c.f.n; }
}

module HeapReadInFunction {
  class C { var f: T  constructor (t: T) { f := t; } }
  datatype T = T(n: int, c: C) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var b := Pick(t.c); b }
  lemma Pick(c: C) returns (b: bool) ensures false { var t := G(c); b := t.n == t.n; }
  ghost function G(c: C): T reads c { c.f }
}

// The same for array elements.
module HeapArray {
  datatype T = T(n: int, a: array<T>) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { t.a.Length > 0 && var b := Pick(t.a); b }
  lemma Pick(a: array<T>) returns (b: bool) requires a.Length > 0 ensures false { b := a[0].n == a[0].n; }
}

module HeapMatrix {
  datatype T = T(n: int, a: array2<T>) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { t.a.Length0 > 0 && t.a.Length1 > 0 && var b := Pick(t.a); b }
  lemma Pick(a: array2<T>) returns (b: bool) requires a.Length0 > 0 && a.Length1 > 0 ensures false {
    b := a[0, 0].n == a[0, 0].n;
  }
}

// Values of T that the proof chooses rather than computes.  Here, these alone do not prove false: T has no
// witness, so a choice needs some value of T to exist, and the checks above reject each way of getting one (but see
// the module Witness).  They are rejected anyway, as they are in a function in the cone, so that a proof in the
// cone follows the same rules as a function there.
module Choices {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool {
    var a := Uninitialized(); var b := SuchThat(); var c := Forall(); var d := Exists(); a && b && c && d
  }
  lemma Uninitialized() returns (b: bool) { var t: T; b := t.n == t.n; }
  lemma SuchThat() returns (b: bool) { var t: T :| true; b := true; }
  lemma Forall() returns (b: bool) { forall t: T ensures t.n == t.n {} b := true; }
  lemma Exists() returns (b: bool) { assert exists t: T :: t.n == 0 || t.n != 0; b := true; }
}

// The type of a parameter of a bound lemma is part of its specification.  As for a function in the cone, a total
// arrow type over T refers to the set of values of T.  This alone does not prove false either.
module ArrowParameter {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost function Ok(t: raw T): bool { var b := Pick(F); b }
  ghost function F(x: T): bool { true }
  lemma Pick(f: T -> bool) returns (b: bool) { b := true; }
}

// A binding's receiver that is a raw value of T, for an instance lemma of T.
module Receiver {
  datatype T = T(n: int) {
    invariant Ok(this)
    lemma Pick() returns (b: bool) ensures false { b := true; }
  }
  ghost function Ok(t: raw T): bool { var b := t.Pick(); b }
}

// With a witness, T has a value to choose.
module Witness {
  datatype T = T(n: int) {
    invariant Ok(this)
    ghost witness T(0)
  }
  ghost function Ok(t: raw T): bool { var b := Pick(); b }
  lemma Pick() returns (b: bool) ensures false { var t: T :| true; b := t.n == t.n; }
}

// The constraints of the types of the results and locals of a proof are in the cone, as those of a function's are.
// This alone does not prove false.
module ConstrainedTypes {
  datatype T = T(n: int) { invariant Ok(this) }
  ghost predicate Q1(x: int) { var r: raw T := T(x); r is T }
  ghost predicate Q2(x: int) { var r: raw T := T(x); r is T }
  type S1 = x: int | Q1(x) witness *
  type S2 = x: int | Q2(x) witness *
  ghost function Ok(t: raw T): bool { var b := Pick(); b }
  lemma Pick() returns (b: bool) {
    var n: int;
    n := Result();
    ghost var s: S2 := *;
    b := true;
  }
  lemma Result() returns (s: S1) requires false { }
}
