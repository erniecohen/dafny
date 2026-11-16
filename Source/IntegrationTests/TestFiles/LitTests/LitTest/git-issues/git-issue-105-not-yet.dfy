// Member invariants (https://github.com/erniecohen/dafny/issues/105): "raw" is parsed and printed
// for every type, but supported only for a datatype so far, and an invariant body must be a
// boolean expression.
// RUN: %exits-with 2 %resolve --member-invariants --print "%t.dfy" "%s" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%t.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module RawTypes {
  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
  }

  class Cell {
    const k: int
    invariant k > 0
    constructor () { k := 1; }
  }

  method M(r: raw Interval, c: raw Cell) returns (s: seq<raw Interval>, m: map<raw Cell, raw Interval>) {
    s, m := [r], map[c := r];
  }
}

module NotBool {
  datatype NotBool = NotBool(x: int) {
    invariant x + 1
  }
}
