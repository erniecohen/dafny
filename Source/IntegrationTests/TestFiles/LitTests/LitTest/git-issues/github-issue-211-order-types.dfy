// https://github.com/erniecohen/dafny/issues/211
// Order types: "type {:order O} L = x: T | C" names the order O of its values, and a bounded value of type
// "bounded<L, F>" (https://github.com/erniecohen/dafny/issues/266) has a bound compared by O.  Here L is VeriFast's
// bag of levels, so funcptr.c is ported in VeriFast's own measure: the stored function of int_func_object holds
// a bag, plus_one's the empty one, twice's {twice} + f's, and the wrapper int_func_object_invoke runs at {invoke}
// + the stored function's.  The order is part of the type and is non-variant: two order types with the same carrier
// and different orders do not mix.
// RUN: echo "funcptr.c with bags of levels, as an order type" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "controls (assert false in each positive context), bounds that do not go down, and orders that do not mix" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-order-types-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "order attributes that do not name an order of the base type, or are not closed" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-211-order-types-resolve.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The levels of funcptr.c, as bags of the levels of its functions, in an order type whose order is the multiset
// order.  The order type is used across modules, so it is closed: its order is part of every contract that names it.
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

module FuncClient {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi
  import FuncImpl

  // main: twice of twice of plus_one is created at {twice, twice}, and applied through the wrapper at
  // {invoke, twice, twice}
  method Main() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Twice});
    var plusOne := FuncImpl.CreatePlusOne();
    assert multiset{Twice} + plusOne.invoke.bound == multiset{Twice};
    var twicePlusOne := FuncImpl.CreateTwice(plusOne);
    UnderMain(multiset{Twice, Twice});
    assert multiset{Twice} + twicePlusOne.invoke.bound == multiset{Twice, Twice};
    var twiceTwicePlusOne := FuncImpl.CreateTwice(twicePlusOne);
    UnderMain(multiset{Invoke, Twice, Twice});
    assert multiset{Invoke} + twiceTwicePlusOne.invoke.bound == multiset{Invoke, Twice, Twice};
    var r := Apply(twiceTwicePlusOne, 10);
  }
}
