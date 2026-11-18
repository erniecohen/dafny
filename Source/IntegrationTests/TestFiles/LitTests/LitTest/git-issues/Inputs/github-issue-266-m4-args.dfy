// A library under --modular-termination with argument-dependent bounds (https://github.com/erniecohen/dafny/issues/266,
// M4): intersects of Fig. 15, from github-issue-211-stored-arguments.dfy.  Built with github-issue-211-multiset-orders.dfy.

module Levels {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  type {:termination_closed} {:order Bags()} Level = m: multiset<nat> | true
  // The levels of the wrappers and of a cons cell: a cell is above both wrappers
  const {:termination_closed} ContainsW: nat := 1
  const {:termination_closed} IntersectsW: nat := 2
  const {:termination_closed} Cell: nat := 3
  const {:termination_closed} MainL: nat := 4
  const {:termination_closed} Empty: multiset<nat> := multiset{}

  lemma {:termination_closed} Lower(a: nat, b: nat, d: multiset<nat>)
    requires a > b
    ensures MultisetGt<nat>(Gt, multiset{a} + d, multiset{b} + d)
  {
    GtWellFounded();
    MultisetReplaceLower<nat>(Gt, a, multiset{b}, d);
  }

  // A permission is replaced by a lower one, and others are given up: {a} + e + d is above {b} + d
  lemma {:termination_closed} LowerPart(a: nat, b: nat, d: multiset<nat>, e: multiset<nat>)
    requires a > b
    ensures MultisetGt<nat>(Gt, multiset{a} + e + d, multiset{b} + d)
  {
    GtWellFounded();
    GtTransitive();
    MultisetReplaceLower<nat>(Gt, a, multiset{b}, d);
    MultisetGeAdd<nat>(Gt, e, multiset{a} + d);
    assert e + (multiset{a} + d) == multiset{a} + e + d;
    MultisetGeGt<nat>(Gt, multiset{a} + e + d, multiset{a} + d, multiset{b} + d);
  }

  // main is above any bag of lower levels
  lemma {:termination_closed} UnderMain(t: multiset<nat>)
    requires forall y :: y in t ==> y < MainL
    ensures MultisetGt<nat>(Gt, multiset{MainL}, t)
  {
    GtWellFounded();
    MultisetDrop<nat>(Gt, MainL, multiset{MainL}, t);
  }
}



module ListApi {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels

  // struct list_object: contains at a fixed level, intersects at a level that depends on its argument.  The depth is
  // the data its intersects level is computed from (VeriFast's levels are data too): o.intersects.boundAt(other) is
  // o.depth + other.contains.bound
  class {:termination_closed} ListObject {
    ghost const {:termination_closed} depth: multiset<nat>
    const {:termination_closed} contains: bounded<Level, int -> bool>
    const {:termination_closed} intersects: bounded<ListObject -> Level, ListObject -> bool>
    constructor (ghost depth: multiset<nat>, contains: bounded<Level, int -> bool>, intersects: bounded<ListObject -> Level, ListObject -> bool>)
      ensures this.depth == depth && this.contains == contains && this.intersects == intersects
      decreases {:global} Empty by Bags()
    {
      this.depth, this.contains, this.intersects := depth, contains, intersects;
    }
  }

  // The level of the intersects of an object of depth d, at its argument: d + the level of the argument's contains.
  // A function rather than a quantified property, since a property of every object holds only of those allocated
  // when it is stated, and the argument of intersects may be allocated later.
  ghost function {:termination_closed} Lvl(d: multiset<nat>): ListObject -> Level
    decreases {:global} Empty by Bags()
  {
    (other: ListObject) => d + other.contains.bound
  }

  // list_object_contains: one level above the stored function
  function Contains(o: ListObject, x: int): bool
    decreases {:global} multiset{ContainsW} + o.contains.bound by Bags()
  {
    MultisetAddOne<nat>(Gt, ContainsW, o.contains.bound);
    o.contains(x)
  }

  // list_object_intersects: one level above the stored function at its argument
  function Intersects(o: ListObject, other: ListObject): bool
    decreases {:global} multiset{IntersectsW} + o.intersects.boundAt(other) by Bags()
  {
    MultisetAddOne<nat>(Gt, IntersectsW, o.intersects.boundAt(other));
    o.intersects(other)
  }
}

module ListImpl {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened ListApi

  method CreateNil() returns (o: ListObject)
    ensures o.contains.bound == Empty && o.depth == Empty
    ensures o.intersects.bound == Lvl(o.depth)
    decreases {:global} multiset{Cell} by Bags()
  {
    MultisetAddOne<nat>(Gt, Cell, Empty);
    o := new ListObject(Empty, bounded<Level, int -> bool>.certify(Empty, (x: int) => false),
                        bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
                          Lvl(Empty), (other: ListObject) => false));
  }

  // cons: intersects(other) asks other whether it contains the head, and then the tail whether it intersects other,
  // at {Cell} + the tail's depth + the level of other's contains: append(d, d_other) of cons.c
  method CreateCons(head: int, tail: ListObject) returns (o: ListObject)
    requires tail.intersects.bound == Lvl(tail.depth)
    ensures o.contains.bound == multiset{Cell} + tail.contains.bound && o.depth == multiset{Cell} + tail.depth
    ensures o.intersects.bound == Lvl(o.depth)
    decreases {:global} multiset{Cell, Cell} + tail.contains.bound by Bags()
  {
    Lower(Cell, ContainsW, tail.contains.bound);
    var contains := bounded<Level, int -> bool>.certify(multiset{Cell} + tail.contains.bound, (x: int) => x == head || Contains(tail, x));
    var intersects: bounded<ListObject -> Level, ListObject -> bool> := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      Lvl(multiset{Cell} + tail.depth),
      (other: ListObject) =>
        (LowerPart(Cell, ContainsW, other.contains.bound, tail.depth);
         Contains(other, head)) ||
        (Lower(Cell, IntersectsW, tail.depth + other.contains.bound);
         assert multiset{Cell} + tail.depth + other.contains.bound == multiset{Cell} + (tail.depth + other.contains.bound);
         Intersects(tail, other)));
    MultisetAddOne<nat>(Gt, Cell, multiset{Cell} + tail.contains.bound);
    o := new ListObject(multiset{Cell} + tail.depth, contains, intersects);
  }
}
