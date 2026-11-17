// Controls and bounds that exceed the caller's, for github-issue-211-multiset-mt.dfy

module Callees {
  import opened MultisetOrders

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

module Controls {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import Callees

  // Controls: each positive context of github-issue-211-multiset-mt.dfy, with assert false
  function Call(c: Callees.Callee, bag: multiset<nat>, L: nat, k: nat): nat
    requires L in bag && L > 0
    decreases {:global} bag by Bags()
  {
    GtWellFounded();
    MultisetReplaceOne<nat>(Gt, bag, L, Copies(L - 1, k));
    assert false; // error
    c.Run(bag - multiset{L} + Copies(L - 1, k))
  }

  function Lower(c: Callees.Callee, bag: multiset<nat>, m: nat): nat
    requires m in bag
    decreases {:global} bag by Bags()
  {
    MultisetSub<nat>(Gt, bag - multiset{m}, bag);
    assert bag - multiset{m} != bag by { assert (bag - multiset{m})[m] < bag[m]; }
    assert false; // error
    c.Run(bag - multiset{m})
  }

  function CallEmpty(c: Callees.Callee, bag: multiset<nat>): nat
    requires bag != multiset{}
    decreases {:global} bag by Bags()
  {
    MultisetSub<nat>(Gt, multiset{}, bag);
    assert false; // error
    c.Run(multiset{})
  }

  function CallTake(c: Callees.Callee, a: nat, X: multiset<nat>): nat
    decreases {:global} multiset{a} + X by Bags()
  {
    MultisetAddOne<nat>(Gt, a, X);
    assert false; // error
    c.Run(X)
  }

  function CallPairs(c: Callees.PairCallee, bag: multiset<(nat, nat)>, a: nat, n: nat): nat
    requires (a + 1, 0) in bag
    decreases {:global} bag by PairBags()
  {
    var q := (a, n);
    LexWellFounded();
    MultisetReplaceOne<(nat, nat)>(Lex, bag, (a + 1, 0), multiset{q, q, q});
    assert false; // error
    c.Run(bag - multiset{(a + 1, 0)} + multiset{q, q, q})
  }

  function CallNested(c: Callees.NestedCallee, bag: multiset<multiset<nat>>, L: nat, k: nat): nat
    requires multiset{L} in bag && L > 0
    decreases {:global} bag by BagsOfBags()
  {
    NestedStep(bag, L, k);
    assert false; // error
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

  // A dispatched call needs a bound strictly below the caller's.  Equal: the same bag
  function Same(c: Callees.Callee, bag: multiset<nat>): nat
    decreases {:global} bag by Bags()
  {
    c.Run(bag) // error
  }

  // Above: the empty bag, the bottom of the order, grows
  function FromEmpty(c: Callees.Callee, empty: multiset<nat>): nat
    requires empty == multiset{}
    decreases {:global} empty by Bags()
  {
    c.Run(multiset{0}) // error
  }

  // Above: a level is added
  function Grow(c: Callees.Callee, bag: multiset<nat>, L: nat): nat
    requires L in bag
    decreases {:global} bag by Bags()
  {
    c.Run(bag + multiset{L}) // error
  }

  // Above: a pair is exchanged for a larger one
  function PairsUp(c: Callees.PairCallee, bag: multiset<(nat, nat)>, a: nat): nat
    requires (a, 0) in bag
    decreases {:global} bag by PairBags()
  {
    c.Run(bag - multiset{(a, 0)} + multiset{(a, 1)}) // error
  }

  // Above: an inner bag is exchanged for a larger inner bag
  function NestedUp(c: Callees.NestedCallee, bag: multiset<multiset<nat>>, L: nat): nat
    requires multiset{L} in bag
    decreases {:global} bag by BagsOfBags()
  {
    c.Run(bag - multiset{multiset{L}} + multiset{multiset{L, L}}) // error
  }
}
