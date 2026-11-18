// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --general-traits=full --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of newtypes (section 3.5 of
// docs/dev/issue105-member-invariants-plan.md): generic newtypes, a header whose bound variable has no
// written type, and a header constraint whose type is a newtype based on bool.  The V1 controls are
// that the invariants do not make a type empty.  Each check has a negative control, marked "error".

module Generic {
  // The invariants of a value of Short<U> are those of Short, with U for T.
  newtype Short<T> = seq<T> {
    invariant |(this as seq<T>)| <= 1
    witness []
  }
  method One<U>(u: U) returns (s: Short<U>) { s := [u] as Short<U>; }
  method Two<U>(u: U) returns (s: Short<U>) { s := [u, u] as Short<U>; } // error: the invariant might not hold
  method Concat<U>(a: Short<U>, b: Short<U>) returns (c: Short<U>) { c := a + b; } // error: the invariant might not hold
  method Ints() returns (s: Short<int>) { s := [3] as Short<int>; }
  method Where<U>(s: Short<U>) { assert |(s as seq<U>)| <= 1; }
  method V1<U>(s: Short<U>) { assert false; } // error
  ghost method Test<U>(r: raw Short<U>) returns (b: bool) ensures b <==> |(r as seq<U>)| <= 1 { b := r is Short<U>; }
  ghost method FromRaw<U>(r: raw Short<U>) returns (s: Short<U>) { s := r; } // error: the invariant might not hold
  ghost method FromRawChecked<U>(r: raw Short<U>) returns (s: Short<U>) requires (r as seq<U>) == [] { s := r; }

  // The header of a generic newtype in the old syntax is one more conjunct, with U for T.
  newtype Pair<T> = s: seq<T> | |s| <= 2 witness [] { invariant |(this as seq<T>)| != 1 }
  method P2<U>(u: U) returns (p: Pair<U>) { p := [u, u] as Pair<U>; }
  method P1<U>(u: U) returns (p: Pair<U>) { p := [u] as Pair<U>; } // error: the invariant might not hold
  method P3<U>(u: U) returns (p: Pair<U>) { p := [u, u, u] as Pair<U>; } // error: the newtype constraint might not hold
  method PWhere<U>(p: Pair<U>) { assert |(p as seq<U>)| != 1 && |(p as seq<U>)| <= 2; }

  // An invariant of a generic trait is one more conjunct of a generic newtype that extends it.
  trait G<X> { const k: int := 1  invariant k > 0 }
  newtype Box<T> extends G<T> = s: seq<T> | |s| < 3 witness [] { invariant true }
  method UseBox<U>(b: Box<U>) { var g: G<U> := b; assert g.k > 0; }
  ghost method BoxFromRaw<U>(r: raw Box<U>) returns (g: G<U>) { g := r; } // error: the newtype constraint might not hold
}

module Inferred {
  // The base type is inferred from the constraint of the header, as without invariants, and so is the
  // native type of int8.
  newtype {:nativeType "sbyte"} int8 = x | -128 <= x < 128 { invariant this != 0  witness 1 }
  method Down(a: int8) returns (b: int8) requires a as int > 1 { b := a - 1; }
  method Zero(a: int8) returns (b: int8) requires a as int == 1 { b := a - 1; } // error: the invariant might not hold
  method Over(a: int8) returns (b: int8) requires a as int > 0 { b := a + 1; } // error: the newtype constraint might not hold
  method Where(a: int8) { assert -128 <= a as int < 128 && a as int != 0; }
  method V1(a: int8) { assert false; } // error

  newtype R = r | 0.0 <= r { invariant this != 1.5  witness 2.0 }
  method RDown(r: R) returns (s: R) requires r as real > 2.0 { s := r - 0.5; }
  method ROne(r: R) returns (s: R) requires r as real == 2.0 { s := r - 0.5; } // error: the invariant might not hold
  method RNeg(r: R) returns (s: R) requires r as real < 3.0 { s := r - 3.0; } // error: the newtype constraint might not hold
}

module BoolBased {
  // The constraint "b" of the header has type Truth, as without invariants.
  newtype Truth = bool
  newtype True = b: Truth | b witness true { invariant true }
  method Yes() returns (t: True) { t := true; }
  method No() returns (t: True) { t := false; } // error: the newtype constraint might not hold
  method Where(t: True) { assert t as Truth; }
  method V1(t: True) { assert false; } // error
}

// A generic newtype based on a generic newtype with invariants: the check against the base type is
// instantiated, whether or not the outer newtype has invariants of its own.
module Nested {
  newtype Singleton<T> = s: seq<T> | |s| == 1 witness * { invariant true }
  newtype Nested<U> = s: Singleton<seq<U>> | true witness * { invariant true }
  newtype Plain<U> = s: Singleton<seq<U>> | true witness *
  method Good(x: seq<seq<int>>) requires |x| == 1 { var n := x as Nested<int>; var p := x as Plain<int>; }
  method Bad() { var x: seq<seq<int>> := []; var n := x as Nested<int>; } // error: x is not a Singleton
  method BadPlain() { var x: seq<seq<int>> := []; var p := x as Plain<int>; } // error: x is not a Singleton
  method V1(n: Nested<int>) { assert false; } // error
}
