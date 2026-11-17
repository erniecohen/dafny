// RUN: %exits-with 2 %resolve "%s" > "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-479-newtype.dfy" >> "%t"
// RUN: %exits-with 4 %verify "%S/Inputs/github-issue-479-accepted.dfy" >> "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-479-accepted.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-reading-functions-in-datatypes "%S/Inputs/github-issue-479-knot.dfy" >> "%t"
// RUN: %exits-with 0 %verify --legacy-reading-functions-in-datatypes --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-479-knot.dfy" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-479-knot.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-479-knot.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/479): a function that reads the heap may not be
// stored in a field or an array element, because it could then read itself through that field, and
// applying a function value has no termination check.  The check looked for such a function only in
// the type of the stored value and its type arguments, so a function hidden in a datatype was stored,
// and the reproducer (Inputs/github-issue-479-knot.dfy) proved false.  The check now also looks inside
// the arguments of the constructors of inductive and coinductive datatypes and the base types of
// newtypes (Inputs/github-issue-479-newtype.dfy) and subset types, with the type arguments
// substituted.  Values without such a function are still stored (Inputs/github-issue-479-accepted.dfy).
// The hidden --legacy-reading-functions-in-datatypes restores the old check, under which the reproducer
// verifies.

module Codatatype {
  codatatype S = S(f: () ~> bool, next: S)
  class Ref { ghost var s: S }
  method M(r: Ref, k: () ~> bool) modifies r { r.s := S(k, r.s); }
}

module GenericDatatype {
  datatype Box<A> = Box(a: A, f: () ~> A)
  class Ref { ghost var b: Box<int> }
  method M(r: Ref, k: () ~> int) modifies r { r.b := Box(0, k); }
}

module Collections {
  datatype W = W(f: () ~> bool)
  datatype O = O(ws: seq<W>, m: map<int, W>)
  class Ref { ghost var o: O }
  method M(r: Ref, w: W) modifies r { r.o := O([w], map[]); }
}

module Recursive {
  datatype L = Nil | Cons(f: () ~> bool, tail: L)
  class Ref { ghost var l: L }
  method M(r: Ref, l: L) modifies r { r.l := l; }
}

// The search ends although each level of the datatype has a new instantiation
module NonRegular {
  datatype N<A> = Leaf(a: A) | Node(n: N<seq<A>>) | F(f: () ~> bool)
  class Ref { ghost var n: N<int> }
  method M(r: Ref, n: N<int>) modifies r { r.n := n; }
}

// The second store was rejected before
module SubsetType {
  datatype W = W(f: () ~> bool)
  type SW = w: W | true
  type SF = f: () ~> bool | true
  class Ref { ghost var w: SW  ghost var g: SF }
  method M(r: Ref, w: SW, g: SF) modifies r { r.w := w; r.g := g; }
}

module ArrayElement {
  datatype W = W(f: () ~> bool)
  method M(a: array<W>, w: W) requires a.Length > 0 modifies a { a[0] := w; }
}

module Tuple {
  datatype W = W(f: () ~> bool)
  class Ref { ghost var t: (W, int) }
  method M(r: Ref, w: W) modifies r { r.t := (w, 0); }
}

module Constructor {
  datatype W = W(f: () ~> bool)
  class Ref {
    ghost var w: W
    constructor (ghost w: W) { this.w := w; }
  }
}
