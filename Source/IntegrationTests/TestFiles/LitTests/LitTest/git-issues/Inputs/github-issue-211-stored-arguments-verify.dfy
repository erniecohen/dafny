// Controls for the intersects port, verified with it: an assert false in each positive context, and bounds at
// arguments that do not go down
module Controls {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened ListApi
  import ListImpl

  method ConsControl(head: int, tail: ListObject) returns (o: ListObject)
    requires tail.intersects.bound == Lvl(tail.depth)
    decreases {:global} multiset{Cell, Cell} + tail.contains.bound by Bags()
  {
    var intersects := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      Lvl(multiset{Cell} + tail.depth),
      (other: ListObject) =>
        (Lower(Cell, IntersectsW, tail.depth + other.contains.bound);
         assert multiset{Cell} + tail.depth + other.contains.bound == multiset{Cell} + (tail.depth + other.contains.bound);
         var r := Intersects(tail, other);
         assert false; // error: control
         r));
    o := new ListObject(multiset{Cell} + tail.depth, tail.contains, intersects);
  }

  method MainControl() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Cell});
    var a := ListImpl.CreateNil();
    var b := ListImpl.CreateNil();
    assert a.intersects.boundAt(b) == multiset{};
    UnderMain(multiset{IntersectsW});
    assert multiset{IntersectsW} + a.intersects.boundAt(b) == multiset{IntersectsW};
    var r := Intersects(a, b);
    assert false; // error: control
  }
}

module Rejected {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened ListApi

  // The wrapper at the level of the stored function at its argument
  function FlatIntersects(o: ListObject, other: ListObject): bool
    decreases {:global} o.intersects.boundAt(other) // the order of Level
  {
    o.intersects(other) // error: the bound at other is not below the caller's
  }

  // The bound at one argument does not cover an application at another
  function OtherArgument(o: ListObject, x: ListObject, y: ListObject): bool
    decreases {:global} multiset{IntersectsW} + o.intersects.boundAt(y) by Bags()
  {
    MultisetAddOne<nat>(Gt, IntersectsW, o.intersects.boundAt(y));
    o.intersects(x) // error: the bound at x is not known to be below the bound at y
  }

  // A cons cell whose intersects is at its tail's level: the tail's wrapper is above it
  method FlatCons(head: int, tail: ListObject) returns (o: ListObject)
    requires tail.intersects.bound == Lvl(tail.depth)
    decreases {:global} multiset{Cell, Cell} + tail.contains.bound by Bags()
  {
    var intersects := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      Lvl(tail.depth),
      (other: ListObject) => Intersects(tail, other)); // error: {IntersectsW} + the tail's level is above the tail's level
    o := new ListObject(tail.depth, tail.contains, intersects);
  }

  // main with too small a budget: the wrapper of a's intersects at b runs at {IntersectsW, Cell, Cell}
  method LowMain(a: ListObject, b: ListObject)
    requires a.intersects.boundAt(b) == multiset{Cell, Cell}
    decreases {:global} multiset{Cell, Cell} by Bags()
  {
    var r := Intersects(a, b); // error: {IntersectsW, Cell, Cell} is above {Cell, Cell}
  }
}
