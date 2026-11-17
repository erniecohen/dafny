// Global bounds of --modular-termination by multiset orders (github-issue-211-multiset.dfy).  The orders
// library is loaded and verified with this file.

module Callees {
  import opened MultisetOrders

  // Dispatched calls that hold permissions at the levels in their bags
  trait Callee {
    function Run(bag: multiset<nat>): nat decreases {:global} bag by Bags()
  }
  trait PairCallee {
    function Run(bag: multiset<(nat, nat)>): nat decreases {:global} bag by PairBags()
  }
  trait NestedCallee {
    function Run(bag: multiset<multiset<nat>>): nat decreases {:global} bag by BagsOfBags()
  }
}

module Client {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import Callees

  // The permission at level L is exchanged for k at level L - 1 before the dispatched call
  function Call(c: Callees.Callee, bag: multiset<nat>, L: nat, k: nat): nat
    requires L in bag && L > 0
    decreases {:global} bag by Bags()
  {
    GtWellFounded();
    MultisetReplaceOne<nat>(Gt, bag, L, Copies(L - 1, k));
    c.Run(bag - multiset{L} + Copies(L - 1, k))
  }

  // One permission is given up before the dispatched call, whose bound is then strictly below the caller's
  // (the same bag is rejected, in github-issue-211-multiset-mt-verify.dfy)
  function Lower(c: Callees.Callee, bag: multiset<nat>, m: nat): nat
    requires m in bag
    decreases {:global} bag by Bags()
  {
    MultisetSub<nat>(Gt, bag - multiset{m}, bag);
    assert bag - multiset{m} != bag by { assert (bag - multiset{m})[m] < bag[m]; }
    c.Run(bag - multiset{m})
  }

  // Every permission is given up before the dispatched call
  function CallEmpty(c: Callees.Callee, bag: multiset<nat>): nat
    requires bag != multiset{}
    decreases {:global} bag by Bags()
  {
    MultisetSub<nat>(Gt, multiset{}, bag);
    c.Run(multiset{})
  }

  // A permission is taken before the dispatched call, with no hint about multiplicities
  function CallTake(c: Callees.Callee, a: nat, X: multiset<nat>): nat
    decreases {:global} multiset{a} + X by Bags()
  {
    MultisetAddOne<nat>(Gt, a, X);
    c.Run(X)
  }

  // A pair (a, 0) is exchanged for three pairs (a - 1, n)
  function CallPairs(c: Callees.PairCallee, bag: multiset<(nat, nat)>, a: nat, n: nat): nat
    requires (a + 1, 0) in bag
    decreases {:global} bag by PairBags()
  {
    var q := (a, n);
    LexWellFounded();
    MultisetReplaceOne<(nat, nat)>(Lex, bag, (a + 1, 0), multiset{q, q, q});
    c.Run(bag - multiset{(a + 1, 0)} + multiset{q, q, q})
  }

  // An inner bag {L} is exchanged for k copies of {L - 1, L - 1}
  function CallNested(c: Callees.NestedCallee, bag: multiset<multiset<nat>>, L: nat, k: nat): nat
    requires multiset{L} in bag && L > 0
    decreases {:global} bag by BagsOfBags()
  {
    NestedStep(bag, L, k);
    c.Run(bag - multiset{multiset{L}} + Copies(multiset{L - 1, L - 1}, k))
  }

  lemma NestedStep(bag: multiset<multiset<nat>>, L: nat, k: nat)
    requires multiset{L} in bag && L > 0
    ensures MultisetGt<multiset<nat>>(MultisetRel<nat>(Gt), bag, bag - multiset{multiset{L}} + Copies(multiset{L - 1, L - 1}, k))
  {
    var B' := multiset{L - 1, L - 1};
    GtWellFounded();
    MultisetReplaceOne<nat>(Gt, multiset{L}, L, B');
    assert multiset{L} - multiset{L} + B' == B';
    assert MultisetRel<nat>(Gt)(multiset{L}, B');
    MultisetReplaceOne<multiset<nat>>(MultisetRel<nat>(Gt), bag, multiset{L}, Copies(B', k));
  }
}
