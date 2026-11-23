// RUN: %exits-with 2 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): clause 5 of the extension condition for the
// values of a newtype's base type.  A literal of a newtype N with invariants, and a conversion or type test of a
// base-type value to N, check that value against N's invariants, as a conversion of a raw value does.  In the cone
// of N's invariants that refers to the set of values of N, so each is rejected.  Each module proved 'false' before
// (see Inputs/git-issue-105-newtype-base-values-vacuity.dfy, which runs them with the condition off).

module TypeTest {
  newtype N = x: int | true {
    invariant !((this as int) is N)  // error: tests a raw value against N
  }
}

module Literal {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  ghost predicate Inv(x: raw N) ensures !Inv(x) { var y: N := 0; true }  // error: the literal 0 of N
}

module Conversion {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  ghost predicate Inv(x: raw N) ensures !Inv(x) { var i: int := 0; var y := i as N; true }  // error: 'i as N'
}

module Constant {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  const C: N := 0  // error: the initializer is in the cone
  ghost predicate Inv(x: raw N) ensures !Inv(x) { C == C }
}

module Operation {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  ghost predicate Inv(x: raw N) ensures !Inv(x) { var y: N := 1 + 1; true }  // error (twice): both operands
}

module SubsetOfNewtype {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  type Small = y: N | y < 10 witness *  // error: the literal 10 of N, in the cone through Small
  ghost predicate Inv(x: raw N) ensures !Inv(x) { var s: Small := 0; true }  // error: the literal 0 of Small
}

module Characters {
  newtype Ch = c: char | true {
    invariant Inv(this)
  }
  ghost predicate Inv(c: raw Ch) ensures !Inv(c) { var s: seq<Ch> := "a"; true }  // error: a string of Ch
}

module Reals {
  newtype R = r: real | true {
    invariant Inv(this)
  }
  ghost predicate Inv(r: raw R) ensures !Inv(r) { var y := 0.5 as R; true }  // error: '0.5 as R'
}
