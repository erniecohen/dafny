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

// D<X> has the variance that D declares, since resolution checks that the invariants are monotone in
// each covariant type parameter and antitone in each contravariant one (git-issue-105-variance.dfy).
// So L<nat> is a subtype of L<int> and N<int> of N<Empty>, and a conversion between them is not
// checked.  Each of Up, InSeq and Contra verifies only because of that: the conversions would otherwise
// be checked as conversions to a subset type are, and proving Inhabited<int>(n) from Inhabited<nat>(n)
// (or Uninhabited<Empty>(n) from Uninhabited<int>(n)) needs induction on n.  A conversion against the
// variance is checked.
module Variance {
  type Empty = x: int | false witness *
  ghost predicate F<X(!new)>(y: X) { false }
  ghost predicate Inhabited<X(!new)>(n: nat) { n == 0 || ((exists y: X :: true) && Inhabited<X>(n - 1)) }
  ghost predicate Uninhabited<X(!new)>(n: nat) { n == 0 || ((forall y: X :: F(y)) && Uninhabited<X>(n - 1)) }

  datatype L<+X(!new)> = L(n: nat) { invariant Inhabited<X>(n) }
  datatype N<-X(!new)> = N(n: nat) { invariant Uninhabited<X>(n) }

  lemma UseL(l: L<int>) ensures Inhabited<int>(l.n) {}
  lemma UseN(m: N<Empty>) ensures Uninhabited<Empty>(m.n) {}

  method Up(l: L<nat>) returns (r: L<int>) ensures Inhabited<int>(r.n) {
    r := l;
    UseL(l);
  }
  method InSeq(l: L<nat>) returns (s: seq<L<int>>) {
    s := [l];
  }
  method Contra(m: N<int>) returns (r: N<Empty>) ensures Uninhabited<Empty>(r.n) {
    r := m;
    UseN(m);
  }
  method Down(l: L<int>) returns (r: L<nat>) {
    r := l;  // error: the invariant might not hold (it does, but proving it needs induction)
  }
  method ToEmpty(l: L<int>) returns (r: L<Empty>) {
    r := l;  // error: the invariant might not hold
  }
  method ContraDown(m: N<Empty>) returns (r: N<int>) {
    r := m;  // error: the invariant might not hold
  }
  method RawUp(l: raw L<nat>) returns (r: raw L<int>) {
    r := l;
  }
  method ViaRaw(l: L<int>) returns (r: L<Empty>) {
    var w: raw L<Empty> := l;
    r := w;  // error: the invariant might not hold
  }

  // A conversion against the variance checks that the value is in the raw type, as well as the
  // invariants: P's invariant holds of p, but its components are not nats
  datatype P<+X(==)> = P(a: X, b: X) { invariant a == b }
  method Components() {
    var p: P<int> := P(-1, -1);
    var q: P<nat> := p;  // error: p is not a raw P<nat>
  }
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

// A bound variable that ranges over the elements of a collection does not use its type (stage 7 of
// section 7.2 of the plan), so B may be covariant (git-issue-105-variance.dfy), and B<nat> is a subtype
// of B<int>: UpBounded verifies only because of that, as Up does.
module BoundedVariance {
  import opened Variance

  datatype B<+X(!new)> = B(xs: set<X>, n: nat) { invariant forall y <- xs :: Inhabited<X>(n) }

  lemma UseB(b: B<int>) ensures forall y <- b.xs :: Inhabited<int>(b.n) {}

  method UpBounded(b: B<nat>) returns (r: B<int>) ensures forall y <- r.xs :: Inhabited<int>(r.n) {
    r := b;
    UseB(b);
  }
  method DownBounded(b: B<int>) returns (r: B<nat>) requires forall y <- b.xs :: y >= 0 {
    r := b;  // error: the invariant might not hold (it does, but proving it needs induction)
  }
}
