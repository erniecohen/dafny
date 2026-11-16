// Controls for github-issue-211-multiset.dfy: "assert false" fails in each context of the positive test, so
// those contexts are not contradictory.  Then bags that do not go down.

module Controls {
  import opened Std.Termination.Orders
  import opened MultisetOrders

  // Error: one element for many smaller ones
  function Spend(bag: multiset<nat>, L: nat, k: nat): nat
    requires L in bag && k > 0
    decreases bag by Bags()
  {
    if L == 0 then 0 else
      MultisetReplaceOne<nat>(Gt, bag, L, Copies(L - 1, k));
      assert false;
      1 + Spend(bag - multiset{L} + Copies(L - 1, k), L - 1, k)
  }

  // Error: the loop
  method Drain(bag0: multiset<nat>) returns (steps: nat) {
    var bag: multiset<nat> := bag0;
    steps := 0;
    while bag != multiset{}
      decreases bag by Bags()
    {
      var L :| L in bag;
      var Y := if L == 0 then multiset{} else Copies(L - 1, L);
      MultisetReplaceOne<nat>(Gt, bag, L, Y);
      bag := bag - multiset{L} + Y;
      steps := steps + 1;
      assert false;
    }
  }

  // Error: the empty bag
  function Clear(bag: multiset<nat>): nat
    decreases bag by Bags()
  {
    if bag == multiset{} then 0 else
      MultisetReplace<nat>(Gt, bag, bag, multiset{});
      assert bag - bag + multiset{} == multiset{};
      assert false;
      1 + Clear(multiset{})
  }

  // Error: pairs
  function Pairs(bag: multiset<(nat, nat)>, p: (nat, nat), n: nat): nat
    requires p in bag
    decreases bag by PairBags()
  {
    if p == (0, 0) then 0 else
      var q := if p.1 > 0 then (p.0, p.1 - 1) else (p.0 - 1, n);
      MultisetReplaceOne<(nat, nat)>(Lex, bag, p, multiset{q, q, q});
      assert false;
      1 + Pairs(bag - multiset{p} + multiset{q, q, q}, q, n)
  }

  // Error: bags of bags
  lemma Nested(bag: multiset<multiset<nat>>, B: multiset<nat>, L: nat, k: nat)
    requires B in bag && L in B && L > 0 && k > 0
  {
    var B' := B - multiset{L} + Copies(L - 1, k);
    MultisetReplaceOne<nat>(Gt, B, L, Copies(L - 1, k));
    assert MultisetRel<nat>(Gt)(B, B');
    MultisetReplaceOne<multiset<nat>>(MultisetRel<nat>(Gt), bag, B, Copies(B', k));
    assert BagsOfBags().gt(bag, bag - multiset{B} + Copies(B', k));
    assert false;
  }

  // Error: a bag followed by a number
  function Mixed(bag: multiset<nat>, L: nat, n: nat): nat
    requires L in bag
    decreases bag by Bags(), n
  {
    if n > 0 then Mixed(bag, L, n - 1)
    else if L == 0 then 0
    else
      MultisetReplaceOne<nat>(Gt, bag, L, multiset{L - 1});
      assert false;
      Mixed(bag - multiset{L} + multiset{L - 1}, L - 1, 100)
  }

  // Error: absorption
  lemma Absorption(A: multiset<nat>, B: multiset<nat>, C: multiset<nat>)
    requires Bags().gt(A, B) && Bags().ge(B, C)
  {
    GtTransitive();
    MultisetTransitive<nat>(Gt);
    assert MultisetRel<nat>(Gt)(A, B) == Bags().gt(A, B);
    assert MultisetRel<nat>(Gt)(B, C) == Bags().gt(B, C);
    assert MultisetRel<nat>(Gt)(A, C) == Bags().gt(A, C);
    assert Bags().gt(A, C);
    assert false;
  }

  // Error: the bag stays the same
  function Stay(bag: multiset<nat>, n: nat): nat
    decreases bag by Bags()
  {
    if n == 0 then 0 else Stay(bag, n - 1)
  }

  // Error: the empty bag stays empty
  function StayEmpty(bag: multiset<nat>): nat
    requires bag == multiset{}
    decreases bag by Bags()
  {
    StayEmpty(multiset{})
  }

  // Error: an element is replaced by a larger one
  function Grow(bag: multiset<nat>, L: nat): nat
    requires L in bag
    decreases bag by Bags()
  {
    Grow(bag - multiset{L} + multiset{L + 1}, L + 1)
  }

  // Error: an element is kept, and a smaller one added
  function Add(bag: multiset<nat>, L: nat): nat
    requires L in bag
    decreases bag by Bags()
  {
    if L == 0 then 0 else Add(bag + multiset{L - 1}, L - 1)
  }

  // Error: a bag of pairs, with (a, b) replaced by (a, b + 1)
  function PairsUp(bag: multiset<(nat, nat)>, p: (nat, nat)): nat
    requires p in bag
    decreases bag by PairBags()
  {
    var q := (p.0, p.1 + 1);
    PairsUp(bag - multiset{p} + multiset{q}, q)
  }
}
