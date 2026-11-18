// Controls for github-issue-211-order-types.dfy, which is verified with this file: an assert false in each positive
// context, bounds that do not go down, and two order types with the same carrier and different orders.

module Controls {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi
  import FuncImpl

  method TwiceControl(f: IntFuncObject) returns (o: IntFuncObject)
    decreases {:global} multiset{MainL} + f.invoke.bound by Bags()
  {
    Lower(MainL, Twice, f.invoke.bound);
    o := FuncImpl.CreateTwice(f);
    assert false; // error: control
  }

  method MainControl() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Twice});
    var plusOne := FuncImpl.CreatePlusOne();
    UnderMain(multiset{Invoke});
    assert multiset{Invoke} + plusOne.invoke.bound == multiset{Invoke};
    var r := Apply(plusOne, 10);
    assert false; // error: control
  }

  method LambdaControl(f: IntFuncObject) returns (g: bounded<Level, int -> int>)
  {
    g := bounded<Level, int -> int>.certify(multiset{Twice} + f.invoke.bound,
      (x: int) => (Lower(Twice, Invoke, f.invoke.bound); assert false; Apply(f, x))); // error: control
  }
}

module Rejected {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi

  // The wrapper at the level of the stored function
  function FlatApply(o: IntFuncObject, x: int): int
    decreases {:global} o.invoke.bound
  {
    o.invoke(x) // error: the bound of o.invoke is not below the caller's
  }

  // twice at the level of the function it calls: the wrapper's bound {invoke} + f's is above it
  method FlatTwice(f: IntFuncObject) returns (o: IntFuncObject)
    decreases {:global} multiset{Twice} + f.invoke.bound by Bags()
  {
    AboveEmpty(Twice, f.invoke.bound);
    o := new IntFuncObject(bounded<Level, int -> int>.certify(f.invoke.bound, (x: int) => 2 * Apply(f, x))); // error: Apply's bound is above f's
  }

  // main with too small a budget: twice of plus_one is applied at {invoke, twice}, above {twice}
  method LowMain(t: IntFuncObject)
    requires t.invoke.bound == multiset{Twice}
    decreases {:global} multiset{Twice} by Bags()
  {
    var r := Apply(t, 10); // error: {invoke} + t's exceeds {twice}
  }
}

// K9: an order type with the carrier of Level and another order
module Levels2 {
  import opened Std.Termination.Orders
  // The empty order on the same carrier: no bag is above another
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

  // The callee's bag is below the caller's in the multiset order, but the callee's bound is a Level2: the orders
  // differ, so the bounds are not compared
  function ApplyMix(f: bounded<Level2, int -> int>, d: Level, x: int): int
    requires MultisetGt<nat>(Gt, d, f.bound)
    decreases {:global} d
  {
    f(x) // error: the orders differ
  }

  // The same call with the callee's bound in the caller's order type is accepted
  function ApplySame(f: bounded<Level, int -> int>, d: Level, x: int): int
    requires MultisetGt<nat>(Gt, d, f.bound)
    decreases {:global} d
  {
    f(x)
  }

  // The order is part of the type and does not vary: a bounded value of one order type is not one of another with
  // the same carrier
  method Convert(f: bounded<Level2, int -> int>) returns (g: bounded<Level, int -> int>) {
    g := f; // error: not a bounded<Level, int -> int>
  }
}
