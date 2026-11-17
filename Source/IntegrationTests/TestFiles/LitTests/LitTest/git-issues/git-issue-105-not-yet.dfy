// Member invariants (https://github.com/erniecohen/dafny/issues/105): raw types are parsed and
// printed but not yet supported, and an invariant body must be a boolean expression.
// RUN: %exits-with 2 %resolve --member-invariants --print "%t.dfy" "%s" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%t.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module RawTypes {
  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
  }

  method M(r: raw Interval) returns (s: seq<raw Interval>, m: map<raw Interval, int>) {
    s, m := [r], map[r := 0];
  }
}

module NotBool {
  datatype NotBool = NotBool(x: int) {
    invariant x + 1
  }
}
