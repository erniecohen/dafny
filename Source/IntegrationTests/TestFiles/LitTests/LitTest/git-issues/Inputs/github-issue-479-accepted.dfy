// Stores that https://github.com/erniecohen/dafny/issues/479 keeps: no function in the stored value reads
// the heap.  The final lemma checks that the file is verified.
module Accepted {
  datatype T = T(f: int -> int, g: int --> int)
  datatype L = Nil | Cons(h: int, tail: L)
  datatype N<A> = Leaf(a: A) | Node(n: N<seq<A>>)
  class C { var x: int }
  datatype H = H(c: C?, s: set<C>)
  type Pos = n: nat | n > 0 witness 1
  type TF = f: int -> int | true witness x => x
  datatype Box<A> = Box(a: A)
  codatatype Co = Co(f: int -> int, next: Co)
  class Ref {
    var t: T  var l: L  var n: N<int>  var h: H  var p: Pos  var tf: TF  var b: Box<int -> int>  var co: Co
    constructor (t: T, l: L, n: N<int>, h: H, tf: TF, co: Co) {
      this.t := t; this.l := l; this.n := n; this.h := h; this.p := 1; this.tf := tf; this.b := Box(x => x); this.co := co;
    }
  }
  method M(r: Ref, t: T, l: L, n: N<int>, h: H, tf: TF, co: Co) modifies r {
    r.t := t; r.l := l; r.n := n; r.h := h; r.p := 1; r.tf := tf; r.b := Box(x => x); r.co := co;
  }
  method A(a: array<T>, t: T) requires a.Length > 0 modifies a ensures a[0] == t { a[0] := t; }
  lemma Control() ensures false { }
}
