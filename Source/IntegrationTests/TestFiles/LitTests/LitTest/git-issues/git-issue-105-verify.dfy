// Member invariants (https://github.com/erniecohen/dafny/issues/105) are resolved but not yet
// verified: the verifier reports each invariant of the modules it verifies.
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
