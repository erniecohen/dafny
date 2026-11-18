// Member invariants (https://github.com/erniecohen/dafny/issues/105): "raw" is parsed and printed
// for every type.  It is supported for a datatype, a class, a trait, an abstract type and a newtype
// with member invariants, but not yet for a newtype whose constraint is given in its header and which
// has no member invariant: "raw" of it would be its base type without the constraint, a type the
// program does not otherwise have.  An invariant body must be a boolean expression.
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

  trait Named extends object {
    const name: string
    invariant |name| > 0
  }

  method N(t: raw Named, u: seq<raw Named?>, v: raw Cell?) { }

  newtype Small = x: int | x < 10 {
    invariant this as int > 0
  }

  newtype Tiny = x: int | x < 10

  method S(s: raw Small, t: raw Tiny) { }
}

module NotBool {
  datatype NotBool = NotBool(x: int) {
    invariant x + 1
  }
}
