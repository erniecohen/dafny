// RUN: %exits-with 4 %verify --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of generic datatypes.  D<X> is a
// subset type of raw D<X> whose constraint is the conjunction of the invariants instantiated with X, so
// it is verified as the non-generic datatypes of git-issue-105-datatypes.dfy are.  Here are the
// vacuity controls V1 and V4 of section 6.4 of docs/dev/issue105-member-invariants-plan.md over a type
// parameter, conversions between instantiations, and witnesses.  The errors that resolution reports
// are in git-issue-105-variance.dfy, git-issue-105-emptiness-errors.dfy (characteristics) and
// git-issue-105-extension.dfy.

module Intervals {
  datatype Interval<X> = Interval(lo: int, hi: int, tag: X) {
    invariant lo <= hi
    function Len(): nat { hi - lo }
  }

  method M() {
    var i: Interval<bool> := Interval(0, 1, true);
    assert i.Len() == 1;
    var j: Interval<int> := Interval(2, 1, 0);  // error: the invariant might not hold
  }

  // V1, over a type parameter: every value satisfies the invariant, and that does not prove false.
  lemma V1<X>(i: Interval<X>) ensures i.lo <= i.hi {}
  lemma V1False<X>(i: Interval<X>) ensures false {}  // error

  // V4, a recursive invariant.
  datatype Keyed<X> = Nil | Cons(key: int, val: X, tail: Keyed<X>) {
    invariant Cons? && tail.Cons? ==> key < tail.key
  }
  lemma V4<X>(k: Keyed<X>) requires k.Cons? && k.tail.Cons? && k.tail.tail.Cons? ensures k.key < k.tail.tail.key {}
  lemma V4False<X>(k: Keyed<X>) ensures false {}  // error

  method Build<X>(x: X) returns (k: Keyed<X>) {
    k := Cons(1, x, Cons(2, x, Nil));
    k := Cons(3, x, k);  // error: the invariant might not hold
  }

  // Instantiated with a type with invariants, each component of type X satisfies them too.
  lemma Nested(k: Keyed<Interval<int>>) requires k.Cons? ensures k.val.Len() == k.val.hi - k.val.lo {}
}

// D<A> is a subtype of D<B> only where A and B are the same type, whatever the declared variance, since
// an invariant may quantify over a type parameter.  A conversion between them is checked as a conversion
// to any subset type is.  raw D keeps the declared variance.
module Variance {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }

  // Box(true) is a Box<X> exactly where X is empty.
  datatype Box<+X(!new)> = Box(b: bool) {
    invariant b ==> forall y: X :: F(y)
  }

  method Raw(s: Box<Empty>) returns (r: raw Box<int>) {
    r := s;
  }
  method Direct(s: Box<Empty>) returns (t: Box<int>) {
    t := s;  // error: the invariant might not hold
  }
  method InSeq(s: Box<Empty>) returns (u: seq<Box<int>>) {
    u := [s];  // error: the invariant might not hold
  }
  method ViaRaw(s: Box<Empty>) returns (t: Box<int>) {
    var r: raw Box<int> := s;
    t := r;  // error: the invariant might not hold
  }
  method Proved(s: Box<nat>) returns (t: Box<int>) requires !s.b {
    t := s;
  }

  // Without the check, Box(true) would be a Box<int>, and this would prove false.
  lemma Unsound(t: Box<int>) requires t.b ensures false { assert F(0); }
}

// The witness of a generic datatype is a static const of type D<X>, so it is checked for every X.
module Witnesses {
  datatype Opt<X> = None | Some(x: X, n: int) {
    invariant Some? ==> n > 0
    witness None
  }
  datatype Tagged<X> = Tagged(xs: seq<X>, n: int) {
    invariant n > |xs|
    witness Tagged([], 1)
  }
  datatype Ghostly<X> = G(n: int) {
    invariant n > 0
    ghost witness G(1)
  }
  datatype Bad<X> = Bad(n: int) {
    invariant n > 0
    witness Bad(0)  // error: the invariant might not hold
  }
  // A datatype without invariants is auto-initializable or nonempty where its components are.
  datatype Wrap<X> = Wrap(o: Opt<X>, t: Tagged<X>)
  datatype WrapG<X> = WrapG(g: Ghostly<X>)

  method Zero<T(0)>() returns (t: T) { t := *; }
  method Nonempty<T(00)>() { }

  method Defaults() {
    var o := Zero<Opt<real>>();
    assert o.Some? ==> o.n > 0;
    var t := Zero<Tagged<int>>();
    assert t.n > |t.xs|;
    var w := Zero<Wrap<bool>>();
    assert w.t.n > |w.t.xs|;
    Nonempty<Ghostly<int>>();
    Nonempty<WrapG<int>>();
  }
}
