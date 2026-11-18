// Controls for a client of github-issue-266-m4-lib.dfy, as a .doo library: an assert false in each positive context,
// bounds that do not go down, and an order type of the client's that does not mix with the library's.
module Controls {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi
  import FuncImpl

  method ApplyControl() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Twice});
    var plusOne := FuncImpl.CreatePlusOne();
    UnderMain(multiset{Invoke});
    assert multiset{Invoke} + plusOne.invoke.bound == multiset{Invoke};
    var r := Apply(plusOne, 10);
    assert false; // error: control
  }

  // The bound of the stored function is the library's
  method BoundControl(f: IntFuncObject) decreases {:global} multiset{Twice, Twice} + f.invoke.bound by Bags() {
    MultisetAddOne<nat>(Gt, Twice, multiset{Twice} + f.invoke.bound);
    assert multiset{Twice} + (multiset{Twice} + f.invoke.bound) == multiset{Twice, Twice} + f.invoke.bound;
    var o := FuncImpl.CreateTwice(f);
    assert o.invoke.bound == multiset{Twice} + f.invoke.bound;
    assert false; // error: control
  }
}

module Rejected {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi

  // The stored function of the library applied at its own bound
  function FlatApply(o: IntFuncObject, x: int): int
    decreases {:global} o.invoke.bound
  {
    o.invoke(x) // error: the bound of o.invoke is not below the caller's
  }

  // A stored function of the client, at the level of the one it applies: the library's Apply runs at {invoke} + f's
  method FlatTwice(f: IntFuncObject) returns (o: IntFuncObject)
    decreases {:global} multiset{Twice} + f.invoke.bound by Bags()
  {
    o := new IntFuncObject(bounded<Level, int -> int>.certify(f.invoke.bound, (x: int) => 2 * Apply(f, x))); // error: Apply's bound is above f's
  }
}

// An order type of the client with the carrier of the library's Level and another order
module Levels2 {
  import opened Std.Termination.Orders
  ghost predicate {:termination_closed} Never(a: multiset<nat>, b: multiset<nat>) { false }
  lemma {:termination_closed} NeverWellFounded() ensures wellFounded<multiset<nat>>(Never) {
    forall c: Chain<multiset<nat>> ensures stops<multiset<nat>>(Never, c) { assert !Never(c(0), c(1)); }
  }
  ghost function {:termination_closed} Flat(): Wfo<multiset<nat>> { NeverWellFounded(); Of(Never) }
  type {:termination_closed} {:order Flat()} Level2 = m: multiset<nat> | true
}

module Mix {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened Levels2
  import opened FuncApi

  // A stored function of the library is not one of the client's order type
  method Convert(o: IntFuncObject) returns (g: bounded<Level2, int -> int>) {
    g := o.invoke; // error: not a bounded<Level2, int -> int>
  }
}
