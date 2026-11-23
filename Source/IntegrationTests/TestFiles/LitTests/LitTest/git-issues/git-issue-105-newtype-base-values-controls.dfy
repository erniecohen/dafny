// RUN: %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants --unchecked-invariant-extension "%S/Inputs/git-issue-105-newtype-base-values-vacuity.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): what clause 5 still accepts around the values
// of a newtype's base type (git-issue-105-newtype-base-values.dfy has what it rejects).  A literal of the raw type,
// a conversion out of the newtype, a conversion to a type that does not depend on the owner, a literal or conversion
// outside the cone, and 'null' in the cone of a class.  The second RUN line is the vacuity control: without the
// condition, each rejected program proves 'false'.

module RawLiterals {
  newtype N = x: int | true {
    invariant this + 1 > this && this as int < 100
  }
  lemma Use() {
    var y: N := 5;      // a literal of N outside the cone, checked here
    var z := y as int;  // a conversion out of N
    assert z == 5;
  }
}

module OtherNewtype {
  newtype M = x: int | x >= 0
  newtype N = x: int | true {
    invariant Small(this)
  }
  ghost predicate Small(x: raw N) { var i := x as int; i >= 0 ==> (i as M) as int < 100 }
}

module Null {
  class C {
    const k: int
    invariant k > 0 && var x: C? := null; x == null
    constructor () { k := 1; }
  }
}
