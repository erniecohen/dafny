// Member invariants (https://github.com/erniecohen/dafny/issues/105): where 'raw' applies.  It
// applies to a type that can declare an invariant, and so far is supported only for a datatype.
// A datatype with invariants is possibly empty (until witnesses are supported), but its raw type
// is as inhabited as the datatype is without them.
// RUN: %exits-with 2 %resolve --member-invariants "%s" > "%t"
// RUN: %exits-with 2 %resolve --member-invariants "%S/Inputs/git-issue-105-raw-codatatype.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Kinds {
  datatype D = D(x: int) {
    invariant x > 0
  }
  class C { }
  trait Tr { }
  newtype N = int
  type Abstract
  type Synonym = D
  type Subset = d: D | d.x > 1 witness *

  method Supported(d: raw D, s: seq<raw D>, m: map<int, raw D>) { }

  method NotYet(c: raw C, d: raw C?, t: raw Tr, n: raw N, a: raw Abstract) { }

  method Never(s: raw Synonym, u: raw Subset) { }

  method TypeParameter<T>(t: raw T) { }
}

module Emptiness {
  datatype D = D(x: int) {
    invariant x > 0
  }

  datatype Auto<T(0)> = Auto(t: T)
  datatype Nonempty<T(00)> = Nonempty(t: T)

  method M(a: Auto<raw D>, b: Auto<D>, c: Nonempty<raw D>, d: Nonempty<D>) { }

  method Z<T(0)>() { }

  method Calls() {
    Z<raw D>();
    Z<D>();
  }
}
