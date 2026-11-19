// RUN: %exits-with 2 %verify --type-system-refresh --lemma-bindings --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %verify --type-system-refresh --lemma-bindings --member-invariants --additional-axioms "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// The program reported in https://github.com/erniecohen/dafny/issues/522.
module Issue {
  datatype T = T(n: int) {
    invariant Pick(this)
  }
  lemma Decide(t: raw T) returns (b: bool)
    ensures b == !(t is T)
  {
    b := !(t is T);
  }
  ghost function Pick(t: raw T): bool {
    var b := Decide(t); b
  }
  lemma False() ensures false {
    var t: raw T := T(0);
    var b := Pick(t);
    assert (t is T) == b;
  }
}

// The direct form must keep the same diagnostic.
module Direct {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { !(t is T) }
}

// The binding can be in another function that the invariant calls.
module Indirect {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { Helper(t) }
  ghost function Helper(t: raw T): bool { var b := Decide(t); b }
  lemma Decide(t: raw T) returns (b: bool)
    ensures b == !(t is T)
  { b := !(t is T); }
}

// Every requires clause is reached, even when the ensures does not mention T.
module Requires {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { var b := Decide(t); b }
  lemma Decide(t: raw T) returns (b: bool)
    requires true
    requires !(t is T)
    ensures b
  { b := true; }
}

// Every ensures clause and the functions it calls are reached.
module SpecificationCall {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { var b := Decide(t); b }
  lemma Decide(t: raw T) returns (b: bool)
    ensures true
    ensures b == Test(t)
  { b := Test(t); }
  ghost function Test(t: raw T): bool { !(t is T) }
}

// A specification can itself bind another lemma, including through requires.
module SpecificationBinding {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool { var b := Decide(t); b }
  lemma Decide(t: raw T) returns (b: bool)
    requires (var c := Test(t); c)
    ensures b
  { b := true; }
  lemma Test(t: raw T) returns (b: bool)
    ensures b == !(t is T)
  { b := !(t is T); }
}

// Reaching the same generic lemma first at an independent type must not hide a
// later dependent instantiation. The callee's own type parameters belong in the worklist key.
module Generic {
  datatype T = T(n: int) { invariant Pick(this) }
  ghost function Pick(t: raw T): bool {
    var a := Exists<bool>();
    var b := Exists<T>(); a && b
  }
  lemma Exists<X(!new)>() returns (b: bool)
    ensures b == (exists x: X :: true)
  { b := false; }
}
