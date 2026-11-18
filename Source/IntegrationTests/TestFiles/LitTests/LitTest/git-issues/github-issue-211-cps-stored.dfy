// https://github.com/erniecohen/dafny/issues/211
// Figs. 16-17 of Jacobs, Bosnacki and Kuiper, "Modular termination verification" (ECOOP 2015), section 4.4, with
// stored continuations: containsCPS and intersectsCPS take their continuation as a bounded value
// (https://github.com/erniecohen/dafny/issues/266), whose bound is its stock, a bag of levels each a bag of
// naturals, in the order type Stock of the multiset order applied twice.  The continuation that intersects the
// rest of a set holds the level {2} + its depth + the other's above its own continuation's stock.
// RUN: echo "Figs. 16-17 with stored continuations, in bags of bags of levels" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "controls (assert false in each positive context), and continuations whose stock does not go down" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-cps-stored-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The stock of a continuation: a bag of levels, each a bag of naturals, ordered by the multiset order applied twice
module StockOrders {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  type {:termination_closed} Lv = multiset<nat>
  type {:termination_closed} {:order BagsOfBags()} Stock = s: multiset<Lv> | true

  ghost predicate {:termination_closed} InnerLt(m: Lv, n: Lv) { MultisetGt<nat>(Gt, n, m) }

  // Replacing the element t of a stock by the elements of s, each lower, goes down
  lemma {:termination_closed} OuterReplace(s: multiset<Lv>, t: Lv, u: multiset<Lv>)
    requires forall a | a in s :: InnerLt(a, t)
    ensures MultisetGt<Lv>(MultisetRel<nat>(Gt), multiset{t} + u, s + u)
  {
    GtWellFounded();
    MultisetReplaceLower<Lv>(MultisetRel<nat>(Gt), t, s, u);
  }

  lemma {:termination_closed} InnerAdd(m: Lv, k: nat) ensures InnerLt(m, multiset{k} + m) {
    MultisetAddOne<nat>(Gt, k, m);
  }

  // A level at most d + od, or the bag {d + od} + ks, are at least the bag {od}
  lemma {:termination_closed} ReqOther(d: Lv, od: Lv, ks: multiset<Lv>)
    ensures multiset{od} == multiset{d + od} + ks || MultisetGt<Lv>(MultisetRel<nat>(Gt), multiset{d + od} + ks, multiset{od})
  {
    GtWellFounded();
    MultisetGeAdd<nat>(Gt, d, od);
    if d + od == od {
      MultisetGeAdd<Lv>(MultisetRel<nat>(Gt), ks, multiset{od});
      assert ks + multiset{od} == multiset{d + od} + ks;
    } else {
      MultisetDrop<Lv>(MultisetRel<nat>(Gt), d + od, multiset{d + od} + ks, multiset{od});
    }
  }

  // The preconditions of intersectsCPS: the Valid() of either set is not above its bound
  lemma {:termination_closed} ReqBoth(d: Lv, od: Lv, ks: multiset<Lv>)
    ensures multiset{d} == multiset{d + od} + ks || MultisetGt<Lv>(MultisetRel<nat>(Gt), multiset{d + od} + ks, multiset{d})
    ensures multiset{od} == multiset{d + od} + ks || MultisetGt<Lv>(MultisetRel<nat>(Gt), multiset{d + od} + ks, multiset{od})
  {
    ReqOther(od, d, ks);
    assert od + d == d + od;
    ReqOther(d, od, ks);
  }

  // The sets of a continuation of stock {l3} + ks, with l3 = {2} + id + od, run below it
  lemma {:termination_closed} ValidBelow(id: Lv, od: Lv, ks: multiset<Lv>)
    ensures var s := multiset{multiset{2} + id + od} + ks;
      MultisetGt<Lv>(MultisetRel<nat>(Gt), s, multiset{id}) && MultisetGt<Lv>(MultisetRel<nat>(Gt), s, multiset{od})
  {
    GtWellFounded();
    var l3 := multiset{2} + id + od;
    MultisetGeAdd<nat>(Gt, multiset{2} + od, id);
    assert multiset{2} + od + id == l3;
    assert id != l3 by { assert |l3| == 1 + |id| + |od|; }
    MultisetGeAdd<nat>(Gt, multiset{2} + id, od);
    assert od != l3 by { assert |l3| == 1 + |id| + |od|; }
    MultisetDrop<Lv>(MultisetRel<nat>(Gt), l3, multiset{l3} + ks, multiset{id});
    MultisetDrop<Lv>(MultisetRel<nat>(Gt), l3, multiset{l3} + ks, multiset{od});
  }

  // The levels below d == {3} + id + od at an intersection: od, and {2} + id + od
  lemma {:termination_closed} LevelsBelow(od: Lv, l3: Lv, id: Lv, d: Lv)
    requires d == multiset{3} + (id + od) && l3 == multiset{2} + id + od
    ensures InnerLt(od, d) && InnerLt(l3, d)
  {
    GtWellFounded();
    MultisetGeAdd<nat>(Gt, multiset{3} + id, od);
    assert multiset{3} + id + od == d;
    assert od != d by { assert |d| == 1 + |id| + |od|; }
    MultisetReplaceLower<nat>(Gt, 3, multiset{2}, id + od);
    assert multiset{2} + (id + od) == l3;
  }
}

// Figs. 16-17: sets with continuation-passing containsCPS and intersectsCPS, each continuation a stored bounded
// value whose bound is its stock: one level per frame it will resume
module CPS {
  import opened MultisetOrders
  import opened StockOrders

  trait {:termination_closed} IntSet {
    ghost const {:termination_closed} depth: Lv
    ghost predicate Valid() decreases {:global} multiset{depth} by BagsOfBags(), 0
    function ContainsCPS(x: int, k: bounded<Stock, bool -> bool>): bool requires Valid()
      decreases {:global} multiset{depth} + k.bound by BagsOfBags(), 1
    function IntersectsCPS(other: IntSet, k: bounded<Stock, bool -> bool>): bool requires (ReqBoth(depth, other.depth, k.bound); Valid() && other.Valid())
      decreases {:global} multiset{depth + other.depth} + k.bound by BagsOfBags(), 1
      decreases multiset{depth + other.depth} + k.bound by BagsOfBags(), 1
  }
}

module Impl {
  import opened MultisetOrders
  import opened StockOrders
  import opened CPS

  class {:termination_closed} Empty extends IntSet {
    constructor () ensures Valid() && depth == multiset{} { depth := multiset{}; }
    ghost predicate Valid() decreases {:global} multiset{depth} by BagsOfBags(), 0 { depth == multiset{} }
    function ContainsCPS(x: int, k: bounded<Stock, bool -> bool>): bool requires Valid()
      decreases {:global} multiset{depth} + k.bound by BagsOfBags(), 1
    {
      OuterReplace(multiset{}, depth, k.bound);
      k(false)
    }
    function IntersectsCPS(other: IntSet, k: bounded<Stock, bool -> bool>): bool requires (ReqBoth(depth, other.depth, k.bound); Valid() && other.Valid())
      decreases {:global} multiset{depth + other.depth} + k.bound by BagsOfBags(), 1
      decreases multiset{depth + other.depth} + k.bound by BagsOfBags(), 1
    {
      OuterReplace(multiset{}, depth + other.depth, k.bound);
      k(false)
    }
  }

  class {:termination_closed} Insert extends IntSet {
    const elem: int
    const inner: IntSet
    constructor (e: int, s: IntSet) requires s.Valid() ensures Valid() && depth == multiset{3} + s.depth {
      elem := e; inner := s; depth := multiset{3} + s.depth;
    }
    ghost predicate Valid() decreases {:global} multiset{depth} by BagsOfBags(), 0 {
      depth == multiset{3} + inner.depth &&
      (InnerAdd(inner.depth, 3); OuterReplace(multiset{inner.depth}, depth, multiset{}); inner.Valid())
    }
    function ContainsCPS(x: int, k: bounded<Stock, bool -> bool>): bool requires Valid()
      decreases {:global} multiset{depth} + k.bound by BagsOfBags(), 1
    {
      if x == elem then
        OuterReplace(multiset{}, depth, k.bound);
        k(true)
      else
        InnerAdd(inner.depth, 3);
        OuterReplace(multiset{inner.depth}, depth, k.bound);
        inner.ContainsCPS(x, k)
    }
    // other.containsCPS(elem, c) runs at {other.depth} + c's stock, where c, the continuation that goes on to
    // intersect the rest, holds l3 = {2} + inner.depth + other.depth above k's stock: both are below depth + other.depth
    function IntersectsCPS(other: IntSet, k: bounded<Stock, bool -> bool>): bool
      requires (ReqBoth(depth, other.depth, k.bound); Valid() && other.Valid())
      decreases {:global} multiset{depth + other.depth} + k.bound by BagsOfBags(), 1
      decreases multiset{depth + other.depth} + k.bound by BagsOfBags(), 1
    {
      var d := depth + other.depth;
      var l3 := multiset{2} + inner.depth + other.depth;
      assert d == multiset{3} + (inner.depth + other.depth);
      LevelsBelow(other.depth, l3, inner.depth, d);
      OuterReplace(multiset{l3}, d, k.bound);
      var c := IntersectsCont(inner, other, k);
      OuterReplace(multiset{other.depth, l3}, d, k.bound);
      assert multiset{other.depth} + c.bound == multiset{other.depth, l3} + k.bound;
      other.ContainsCPS(elem, c)
    }
  }

  // The continuation of intersectsCPS (IntersectsCont of Fig. 17): with true, k(true); with false, the rest of the
  // set intersected with other.  Its stock is {l3} + k's, with l3 = {2} + s.depth + other.depth.
  function IntersectsCont(s: IntSet, other: IntSet, k: bounded<Stock, bool -> bool>): (c: bounded<Stock, bool -> bool>)
    requires (ValidBelow(s.depth, other.depth, k.bound); s.Valid() && other.Valid())
    ensures c.bound == multiset{multiset{2} + s.depth + other.depth} + k.bound
    decreases {:global} multiset{multiset{2} + s.depth + other.depth} + k.bound by BagsOfBags(), 0
    decreases multiset{multiset{2} + s.depth + other.depth} + k.bound by BagsOfBags(), 0
  {
    var l3 := multiset{2} + s.depth + other.depth;
    bounded<Stock, bool -> bool>.certify(multiset{l3} + k.bound, (b: bool) =>
      if b then
        OuterReplace(multiset{}, l3, k.bound);
        k(true)
      else
        InnerAdd(s.depth + other.depth, 2);
        OuterReplace(multiset{s.depth + other.depth}, l3, k.bound);
        ValidBelow(s.depth, other.depth, k.bound);
        s.IntersectsCPS(other, k))
  }

  method Main() {
    var e := new Empty();
    var s1 := new Insert(1, e);
    var s2 := new Insert(2, e);
    var done := bounded<Stock, bool -> bool>.certify(multiset{}, (b: bool) => b);
    var r := s1.IntersectsCPS(s2, done);
  }
}
