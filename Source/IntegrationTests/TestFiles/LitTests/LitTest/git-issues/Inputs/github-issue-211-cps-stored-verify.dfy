// Controls for github-issue-211-cps-stored.dfy, which is verified with this file: an assert false in each
// positive context, and continuations whose stock does not go down.

module Controls {
  import opened MultisetOrders
  import opened StockOrders
  import opened CPS
  import Impl

  function ContainsControl(s: IntSet, x: int, k: bounded<Stock, bool -> bool>): bool
    requires s.Valid()
    decreases {:global} multiset{s.depth} + k.bound by BagsOfBags(), 2
  {
    var r := s.ContainsCPS(x, k);
    assert false; // error: control
    r
  }

  function ContControl(s: IntSet, other: IntSet, k: bounded<Stock, bool -> bool>): (c: bounded<Stock, bool -> bool>)
    requires (ValidBelow(s.depth, other.depth, k.bound); s.Valid() && other.Valid())
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
        assert false; // error: control
        s.IntersectsCPS(other, k))
  }

  method MainControl() {
    var e := new Impl.Empty();
    var s1 := new Impl.Insert(1, e);
    var done := bounded<Stock, bool -> bool>.certify(multiset{}, (b: bool) => b);
    var r := s1.IntersectsCPS(e, done);
    assert false; // error: control
  }
}

module Rejected {
  import opened MultisetOrders
  import opened StockOrders
  import opened CPS

  // A continuation that does not add the level l3 to its own continuation's stock: neither k nor the rest of the
  // intersection is below it
  function FlatCont(s: IntSet, other: IntSet, k: bounded<Stock, bool -> bool>): (c: bounded<Stock, bool -> bool>)
    requires (ValidBelow(s.depth, other.depth, k.bound); s.Valid() && other.Valid())
  {
    bounded<Stock, bool -> bool>.certify(k.bound, (b: bool) =>
      if b then
        k(true) // error: k's stock is the continuation's
      else
        s.IntersectsCPS(other, k)) // error: {s.depth + other.depth} + k's stock is above it
  }

  // A continuation whose stock counts the depth of one set only: the intersection it resumes runs at the depths of
  // both, which is not below it when the other set is not empty.  (One at the intersection's own stock is accepted:
  // a value certified at b runs at (b, TOP), above the intersection's (b, 1), and below any caller that applies it.)
  function OneSidedCont(s: IntSet, other: IntSet, k: bounded<Stock, bool -> bool>): (c: bounded<Stock, bool -> bool>)
    requires (ReqBoth(s.depth, other.depth, k.bound); s.Valid() && other.Valid())
  {
    bounded<Stock, bool -> bool>.certify(multiset{s.depth} + k.bound, (b: bool) =>
      s.IntersectsCPS(other, k)) // error: {s.depth + other.depth} + k's stock is not below {s.depth} + k's
  }
}
