// A library under --modular-termination with an order type and bounded values (https://github.com/erniecohen/dafny/issues/266,
// M4): funcptr.c with bags of levels, from github-issue-211-order-types.dfy.  Built with github-issue-211-multiset-orders.dfy.

module Levels {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  type {:termination_closed} {:order Bags()} Level = m: multiset<nat> | true
  const {:termination_closed} Invoke: nat := 1
  const {:termination_closed} Twice: nat := 2
  const {:termination_closed} MainL: nat := 3
  const {:termination_closed} Empty: multiset<nat> := multiset{}

  // A level replaced by a lower one: {a} + d is above {b} + d when a > b
  lemma {:termination_closed} Lower(a: nat, b: nat, d: multiset<nat>)
    requires a > b
    ensures MultisetGt<nat>(Gt, multiset{a} + d, multiset{b} + d)
  {
    GtWellFounded();
    MultisetReplaceLower<nat>(Gt, a, multiset{b}, d);
  }

  // Any bag with a level in it is above the empty one
  lemma {:termination_closed} AboveEmpty(a: nat, d: multiset<nat>)
    ensures MultisetGt<nat>(Gt, multiset{a} + d, Empty)
  {
    GtWellFounded();
    MultisetDrop<nat>(Gt, a, multiset{a} + d, Empty);
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

module FuncApi {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels

  // struct int_func_object: a stored function, whose bag of levels is its bound
  class {:termination_closed} IntFuncObject {
    const {:termination_closed} invoke: bounded<Level, int -> int>
    constructor (invoke: bounded<Level, int -> int>) ensures this.invoke == invoke decreases {:global} Empty by Bags() {
      this.invoke := invoke;
    }
  }

  // int_func_object_invoke: {invoke} + the stored function's bag.  The bound has type Level, so its order is Bags().
  function Apply(o: IntFuncObject, x: int): int
    decreases {:global} multiset{Invoke} + o.invoke.bound by Bags()
  {
    StoredBelow(o.invoke.bound);
    o.invoke(x)
  }

  lemma {:termination_closed} StoredBelow(d: multiset<nat>)
    ensures MultisetGt<nat>(Gt, multiset{Invoke} + d, d)
  {
    MultisetAddOne<nat>(Gt, Invoke, d);
  }
}

module FuncImpl {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi

  // plus_one: the empty bag
  method CreatePlusOne() returns (o: IntFuncObject)
    ensures o.invoke.bound == Empty
    decreases {:global} multiset{Twice} by Bags()
  {
    AboveEmpty(Twice, Empty);
    o := new IntFuncObject(bounded<Level, int -> int>.certify(Empty, (x: int) => x + 1));
  }

  // twice: its stored function applies f's through the wrapper, at {invoke} + f's bag, below {twice} + f's bag
  method CreateTwice(f: IntFuncObject) returns (o: IntFuncObject)
    ensures o.invoke.bound == multiset{Twice} + f.invoke.bound
    decreases {:global} multiset{Twice} + f.invoke.bound by Bags()
  {
    var invoke := bounded<Level, int -> int>.certify(multiset{Twice} + f.invoke.bound,
      (x: int) => (Lower(Twice, Invoke, f.invoke.bound); 2 * Apply(f, x)));
    AboveEmpty(Twice, f.invoke.bound);
    o := new IntFuncObject(invoke);
  }
}
