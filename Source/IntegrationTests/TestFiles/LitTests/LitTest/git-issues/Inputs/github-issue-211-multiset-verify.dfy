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
      GtWellFounded();
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
      GtWellFounded();
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
      MultisetSub<nat>(Gt, multiset{}, bag);
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
      LexWellFounded();
      MultisetReplaceOne<(nat, nat)>(Lex, bag, p, multiset{q, q, q});
      assert false;
      1 + Pairs(bag - multiset{p} + multiset{q, q, q}, q, n)
  }

  // Error: bags of bags
  lemma Nested(bag: multiset<multiset<nat>>, B: multiset<nat>, L: nat, k: nat)
    requires B in bag && L in B && L > 0 && k > 0
  {
    var B' := B - multiset{L} + Copies(L - 1, k);
    GtWellFounded();
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
      GtWellFounded();
      MultisetReplaceOne<nat>(Gt, bag, L, multiset{L - 1});
      assert false;
      Mixed(bag - multiset{L} + multiset{L - 1}, L - 1, 100)
  }

  // Error: absorption
  lemma Absorption(A: multiset<nat>, B: multiset<nat>, C: multiset<nat>)
    requires Bags().gt(A, B) && Bags().ge(B, C)
  {
    GtTransitive();
    GtWellFounded();
    MultisetTransitive<nat>(Gt);
    assert MultisetRel<nat>(Gt)(A, B) == Bags().gt(A, B);
    assert MultisetRel<nat>(Gt)(B, C) == Bags().gt(B, C);
    assert MultisetRel<nat>(Gt)(A, C) == Bags().gt(A, C);
    assert Bags().gt(A, C);
    assert false;
  }

  // Error: a permission is taken, with no hint
  ghost function Take(bag: multiset<nat>, a: nat, X: multiset<nat>): nat
    requires bag == multiset{a} + X
    decreases bag by Bags()
  {
    if X == multiset{} then 0 else
      var b :| b in X;
      MultisetAddOne<nat>(Gt, a, X);
      assert multiset{b} + (X - multiset{b}) == X;
      assert false;
      Take(X, b, X - multiset{b})
  }

  // Error: the lemmas of VeriFast
  lemma VeriFastLemmas(x: nat, y: nat, m: nat, X: multiset<nat>, X1: multiset<nat>, X2: multiset<nat>,
                       Y: multiset<nat>, Z: multiset<nat>, L: multiset<nat>)
  {
    GtWellFounded();
    GtTransitive();
    if x < y { MultisetReplaceByLower<nat>(Gt, y, x, X); }
    MultisetAddOne<nat>(Gt, x, X);
    if MultisetGe<nat>(Gt, X2, X1) { MultisetGeUnion<nat>(Gt, X1, X2, Y); }
    MultisetGeAdd<nat>(Gt, X, Y);
    if MultisetGe<nat>(Gt, Y, X) { MultisetGeCons<nat>(Gt, x, X, Y); }
    if MultisetGe<nat>(Gt, Y, X) && Bags().gt(Z, Y) { MultisetGtGe<nat>(Gt, Z, Y, X); }
    if MultisetGe<nat>(Gt, Y, X) && MultisetGe<nat>(Gt, Z, Y) { MultisetGeTransitive<nat>(Gt, Z, Y, X); }
    if forall l :: l in L ==> l < m { MultisetReplaceLower<nat>(Gt, m, L, X); }
    if m in X && forall l :: l in L ==> l < m { MultisetDrop<nat>(Gt, m, X, L); }
    if X <= Y { MultisetSub<nat>(Gt, X, Y); }
    assert false;
  }

  // Error: continuations
  ghost function Continue(stocks: multiset<multiset<nat>>, level: nat, rest: multiset<nat>): nat
    requires multiset{level} + rest in stocks
    decreases stocks by BagsOfBags()
  {
    if rest == multiset{} then 0 else
      var next :| next in rest;
      GtWellFounded();
      MultisetAddOne<nat>(Gt, level, rest);
      MultisetReplaceOne<multiset<nat>>(MultisetRel<nat>(Gt), stocks, multiset{level} + rest, multiset{rest});
      assert multiset{next} + (rest - multiset{next}) == rest;
      assert false;
      Continue(stocks - multiset{multiset{level} + rest} + multiset{rest}, next, rest - multiset{next})
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

  // Error: a continuation's stock is replaced by one with its level raised
  ghost function Raise(stocks: multiset<multiset<nat>>, level: nat, rest: multiset<nat>): nat
    requires multiset{level} + rest in stocks
    decreases stocks by BagsOfBags()
  {
    Raise(stocks - multiset{multiset{level} + rest} + multiset{multiset{level + 1} + rest}, level + 1, rest)
  }

  // Error: the bag goes down by MultisetGe alone, which allows it to stay the same
  ghost function GeOnly(bag: multiset<nat>, n: nat): nat
    decreases bag by Bags()
  {
    MultisetSub<nat>(Gt, bag, bag);
    if n == 0 then 0 else GeOnly(bag, n - 1)
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
