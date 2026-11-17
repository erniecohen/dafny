// Member invariants (https://github.com/erniecohen/dafny/issues/105) are resolved but not yet
// verified, so no module of a program that declares one is verified. The verifier reports each
// invariant, and, for a module without invariants, its first declaration that it would verify.
// RUN: %exits-with 2 %verify --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Interval = Interval(lo: int, hi: int) {
  invariant lo <= hi
}

module Inner {
  class Cell {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
  }
}

lemma Unrelated() ensures 1 + 1 == 2 {}

module Plain {
  function F(x: int): int { x }
  lemma L() ensures F(1) == 1 {}
}

module NothingToVerify {
  type T = int
}
