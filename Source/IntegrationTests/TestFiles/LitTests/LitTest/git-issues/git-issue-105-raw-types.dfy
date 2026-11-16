// Member invariants (https://github.com/erniecohen/dafny/issues/105): the raw type of a datatype.
// The name of a datatype with invariants, written as a type, denotes the values that satisfy
// them; 'raw D' denotes all values of the datatype.  A constructor application and an update
// have type 'raw D', and an unannotated variable gets the type of its initializer, as with
// any subset type.  The resolved program shows the inferred types.
// RUN: %resolve --member-invariants --print-mode NoIncludes --rprint:- "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Basics {
  datatype Interval = Interval(lo: int, hi: int) {
    invariant lo <= hi
    function Width(): nat { hi - lo }
  }

  method Values(i: Interval) returns (j: Interval) {
    var r := Interval(i.hi, i.lo);
    var u := i.(lo := i.lo - 1);
    var v: Interval := r;
    var w := i;
    var s := [r, i];
    var t := [i, v];
    var m := map[r := i];
    var x := if r.lo < 0 then r else i;
    var b := r == i && r.Interval? && r.lo == i.hi;
    var c := match r case Interval(a, _) => a;
    j := r;
  }

  function Pick(b: bool, r: raw Interval, i: Interval): raw Interval {
    if b then r else i
  }

  // 'raw' of a datatype without invariants is the datatype itself.
  datatype Plain = Plain(x: int)

  method Plains(r: raw Plain) returns (p: Plain) {
    p := r;
    var k := Plain(3);
  }
}

module Clients {
  import B = Basics

  method Use(r: raw B.Interval, s: seq<raw B.Interval>) returns (i: B.Interval) {
    var k := B.Interval(0, 1);
    i := if |s| == 0 then r else s[0];
  }
}

module Generics {
  datatype Pair<A(==)> = Pair(a: A, b: A) {
    invariant a != b
    function Swap(): Pair<A> { Pair(b, a) }
  }

  datatype Box<+T> = Box(t: T)

  method M<X(==)>(p: Pair<X>, q: raw Pair<int>, b: Box<raw Pair<X>>, c: Box<Pair<X>>) {
    var r := Pair(p.b, p.a);
    var rb := Box(r);
    var pb := Box(p);
    var s := p.Swap();
    var b2: Box<raw Pair<X>> := c;
    var c2: Box<Pair<X>> := b;
    var q2 := q.(a := 3);
  }
}

module RawMembers {
  // A member is raw if an invariant applies it to 'this' or to another raw value, or if another
  // raw member does.  In a raw member, 'this' has type 'raw Tree'.
  datatype Tree = Leaf | Node(left: Tree, key: int, right: Tree) {
    // raw: the invariant applies it to 'this'
    predicate Ordered() {
      Node? ==> (forall k <- left.Keys() :: k < key) && (forall k <- right.Keys() :: key < k)
    }
    invariant Ordered()
    // not raw: it is applied only to the components, which are finished values
    function Keys(): set<int> { if Leaf? then {} else left.Keys() + {key} + right.Keys() }
    // raw: Small, a raw member, applies it to a variable that holds 'this'
    function Size(): nat { if Leaf? then 0 else 1 + left.Size() + right.Size() }
    predicate Small() { var t := this; t.Size() < 100 }
    invariant Small()
    // not raw: no invariant uses it
    function Min(): int requires Node? { if left.Leaf? then key else left.Min() }
  }

  datatype Chain = Chain(n: nat) {
    invariant A()
    predicate A() { B() }
    predicate B() { n < 10 && C() }
    predicate C() { n != 7 }
    // not raw: it applies a raw member, but no raw member applies it
    predicate D() { A() && C() }
  }

  // Mutual recursion: Even and Odd apply each other to constructor applications, which are raw.
  datatype Parity = Parity(n: nat) {
    invariant Even() || Odd()
    predicate Even() decreases n { n == 0 || Parity(n - 1).Odd() }
    predicate Odd() decreases n { n != 0 && Parity(n - 1).Even() }
  }

  // An invariant may apply a member to another raw value.
  datatype Span = Span(lo: int, hi: int) {
    invariant Span(lo, hi).Ok()
    invariant Finished().Len() >= 0
    predicate Ok() { lo <= hi }
    function Finished(): Span { this }
    // not raw: it is applied to a Span
    function Len(): int { hi - lo }
  }

  // Constants, functions with a by-method body, lemmas and extreme predicates.
  datatype Rect = Rect(w: int, h: int) {
    const area := w * h
    const perimeter := 2 * (w + h)
    invariant area >= 0
    function Diagonal(): int { w + h } by method { return w + h; }
    invariant Diagonal() >= 0
    lemma Square() ensures w * w >= 0 {}
    predicate P() { Square(); w * w >= 0 }
    invariant P()
  }

  datatype Num = Zero | Succ(pred: Num) {
    invariant Fin()
    least predicate Fin() { Zero? || pred.Fin() }
    greatest predicate Inf() { Succ? && pred.Inf() }
  }
}
