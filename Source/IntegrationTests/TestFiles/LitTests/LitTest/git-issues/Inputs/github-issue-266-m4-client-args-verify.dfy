// Controls for a client of github-issue-266-m4-args.dfy, as a .doo library: an assert false in a positive context,
// and a bound at an argument that does not go down.
module Controls {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened ListApi
  import ListImpl

  method IntersectsControl(a: ListObject, b: ListObject)
    decreases {:global} multiset{IntersectsW} + a.intersects.boundAt(b) by Bags()
  {
    MultisetAddOne<nat>(Gt, IntersectsW, a.intersects.boundAt(b));
    var r := a.intersects(b);
    assert false; // error: control
  }
}

module Rejected {
  import opened MultisetOrders
  import opened Levels
  import opened ListApi

  // The stored function of the library applied at its own bound at its argument
  function FlatIntersects(o: ListObject, other: ListObject): bool
    decreases {:global} o.intersects.boundAt(other)
  {
    o.intersects(other) // error: the bound of o.intersects at other is not below the caller's
  }
}
