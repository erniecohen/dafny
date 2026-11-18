// Rejected argument-dependent bounds: each is reported by the resolver
module OpenLevels {
  // A level that other modules implement: its code is not known here
  trait Leveled {
    ghost function Lv(): multiset<nat>
  }
}

module RejectedBounds {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened ListApi
  import opened OpenLevels

  ghost function ByDispatch(t: Leveled): ListObject -> Level {
    (other: ListObject) => t.Lv()
  }

  // The bound of o's intersects at o itself: an object whose intersects had this bound would have
  // B(o) == {Cell} + B(o)
  ghost function Knot(): ListObject -> Level {
    (o: ListObject) => multiset{Cell} + o.intersects.boundAt(o)
  }

  ghost function Helper(t: ListObject, o: ListObject): Level {
    t.intersects.boundAt(o)
  }

  method Bad(tail: ListObject, g: ListObject -> Level, t: Leveled, f: ListObject -> bool)
    decreases {:global} multiset{Cell} by Bags()
  {
    // The knot: b applies an argument-dependent bound, which could be the one being formed
    var k := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      (other: ListObject) => multiset{Cell} + tail.intersects.boundAt(other), (other: ListObject) => false); // error: applies an argument bound
    var k1 := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(Knot(), (other: ListObject) => false); // error: applies an argument bound
    var k2 := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      (other: ListObject) => Helper(tail, other), (other: ListObject) => false); // error: applies an argument bound
    // b is a function value whose code is not known
    var u := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(g, (other: ListObject) => false); // error: unknown code
    // b applies such a value
    var v := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      (other: ListObject) => g(other), (other: ListObject) => false); // error: reaches unknown code (and applies it)
    // b reaches dispatch to code that other modules provide
    var w := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(
      (other: ListObject) => t.Lv(), (other: ListObject) => false); // error: dispatch
    var x := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(ByDispatch(t), (other: ListObject) => false); // error: dispatch
    // The certified function is not a lambda expression
    var y := bounded<ListObject -> Level, ListObject -> bool>.certifyAt(Lvl(Empty), f); // error: not a lambda
  }
}
