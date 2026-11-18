// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) are verified for every kind of type
// that can declare one.  Until stage 6 of section 7.2 of docs/dev/issue105-member-invariants-plan.md,
// the invariants of a newtype were resolved but not verified, and no module of a program that declared
// one was verified.  That gate is gone, so every module here is verified, and each has a negative
// control, marked "error".

datatype Interval<X> = Interval(lo: int, hi: int, tag: X) {
  invariant lo <= hi
}

function Backwards<X>(x: X): Interval<X> { Interval(1, 0, x) } // error: the invariant might not hold

module Inner {
  class Cell {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
    constructor Zero() { k := 0; } // error: the invariant might not hold
  }
  newtype Small = x: int | 0 <= x < 100 witness 7 {
    invariant this != 13
  }
  method Twelve() returns (s: Small) { s := 12; }
  method Thirteen() returns (s: Small) { s := 13; } // error: the invariant might not hold
}

lemma Unrelated() ensures 1 + 1 == 2 {}

module Plain {
  function F(x: int): int { x }
  lemma L() ensures F(1) == 1 {}
  lemma Wrong() ensures F(1) == 2 {} // error: the postcondition might not hold
}

module NothingToVerify {
  type T = int
}
