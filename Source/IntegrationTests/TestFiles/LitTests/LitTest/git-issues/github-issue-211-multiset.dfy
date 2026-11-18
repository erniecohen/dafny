// https://github.com/erniecohen/dafny/issues/211
// The multiset order of the orders library, Std.Termination.Orders.MultisetOf, over any well-founded relation,
// in the form of Huet and Oppen that VeriFast uses for its bags of call permission levels: a bag goes down when
// one or more of its elements are replaced by finitely many smaller ones.  The library proves it well-founded,
// and transitive over a transitive relation, so that bags of bags are ordered too.
// RUN: echo "the orders library, verified from its sources" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false "%repositoryRoot/Source/DafnyStandardLibraries/orders/dfyconfig.toml" >> "%t"
// RUN: echo "positive: one element for many smaller ones, the empty bag, pairs, bags of bags, a mixed tuple, absorption, the lemmas of VeriFast" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "controls (assert false in each positive context) and bags that do not go down" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-211-multiset-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: global bounds by multiset orders, the orders library verified with the client" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-211-multiset-mt.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: controls, the same bag, and bounds above the caller's" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-211-multiset-mt-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: a relation that is not known, passed to the library's multiset functions and lemmas" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-211-multiset-mt-resolve.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Clients {
  import opened Std.Termination.Orders
  import opened MultisetOrders

  // The permission at level L is exchanged for k at level L - 1, again and again: the bag grows, and goes down
  function Spend(bag: multiset<nat>, L: nat, k: nat): nat
    requires L in bag && k > 0
    decreases bag by Bags()
  {
    if L == 0 then 0 else
      GtWellFounded();
      MultisetReplaceOne<nat>(Gt, bag, L, Copies(L - 1, k));
      1 + Spend(bag - multiset{L} + Copies(L - 1, k), L - 1, k)
  }

  // The same in a loop, with L permissions at level L - 1 for each permission at level L
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
    }
  }

  // The empty bag is below every other bag
  function Clear(bag: multiset<nat>): nat
    decreases bag by Bags()
  {
    if bag == multiset{} then 0 else
      MultisetSub<nat>(Gt, multiset{}, bag);
      1 + Clear(multiset{})
  }

  // and nothing is below it
  lemma NothingBelowEmpty(N: multiset<nat>) ensures !Bags().gt(multiset{}, N) {
    if N != multiset{} {
      var x :| x in N;
      assert x in N - multiset{};
    }
  }

  // A pair (a, 0) is exchanged for three pairs (a - 1, n), however large n is
  function Pairs(bag: multiset<(nat, nat)>, p: (nat, nat), n: nat): nat
    requires p in bag
    decreases bag by PairBags()
  {
    if p == (0, 0) then 0 else
      var q := if p.1 > 0 then (p.0, p.1 - 1) else (p.0 - 1, n);
      LexWellFounded();
      MultisetReplaceOne<(nat, nat)>(Lex, bag, p, multiset{q, q, q});
      1 + Pairs(bag - multiset{p} + multiset{q, q, q}, q, n)
  }

  // An inner bag B is exchanged for k copies of a smaller one
  function Nested(bag: multiset<multiset<nat>>, B: multiset<nat>, L: nat, k: nat): nat
    requires B in bag && L in B && k > 0
    decreases bag by BagsOfBags()
  {
    if L == 0 then 0 else
      NestedStep(bag, B, L, k);
      1 + Nested(Outer(bag, B, L, k), Inner(B, L, k), L - 1, k)
  }

  opaque function Inner(B: multiset<nat>, L: nat, k: nat): multiset<nat>
    requires L > 0
  {
    B - multiset{L} + Copies(L - 1, k)
  }

  opaque function Outer(bag: multiset<multiset<nat>>, B: multiset<nat>, L: nat, k: nat): multiset<multiset<nat>>
    requires L > 0
  {
    bag - multiset{B} + Copies(Inner(B, L, k), k)
  }

  lemma NestedStep(bag: multiset<multiset<nat>>, B: multiset<nat>, L: nat, k: nat)
    requires B in bag && L in B && L > 0 && k > 0
    ensures L - 1 in Inner(B, L, k) && Inner(B, L, k) in Outer(bag, B, L, k)
    ensures BagsOfBags().gt(bag, Outer(bag, B, L, k))
  {
    reveal Inner(), Outer();
    var B' := Inner(B, L, k);
    GtWellFounded();
    MultisetReplaceOne<nat>(Gt, B, L, Copies(L - 1, k));
    assert MultisetRel<nat>(Gt)(B, B');
    MultisetReplaceOne<multiset<nat>>(MultisetRel<nat>(Gt), bag, B, Copies(B', k));
  }

  // A bag followed by a number: the number may grow when the bag goes down
  function Mixed(bag: multiset<nat>, L: nat, n: nat): nat
    requires L in bag
    decreases bag by Bags(), n
  {
    if n > 0 then Mixed(bag, L, n - 1)
    else if L == 0 then 0
    else
      GtWellFounded();
      MultisetReplaceOne<nat>(Gt, bag, L, multiset{L - 1});
      Mixed(bag - multiset{L} + multiset{L - 1}, L - 1, 100)
  }

  // Both absorption laws of the 211 plan, for the order's own ge (gt or equal)
  lemma Absorption(A: multiset<nat>, B: multiset<nat>, C: multiset<nat>)
    ensures Bags().gt(A, B) && Bags().ge(B, C) ==> Bags().gt(A, C)
    ensures Bags().ge(A, B) && Bags().gt(B, C) ==> Bags().gt(A, C)
  {
    GtTransitive();
    GtWellFounded();
    MultisetTransitive<nat>(Gt);
    assert MultisetRel<nat>(Gt)(A, B) == Bags().gt(A, B);
    assert MultisetRel<nat>(Gt)(B, C) == Bags().gt(B, C);
    assert MultisetRel<nat>(Gt)(A, C) == Bags().gt(A, C);
  }

  // The order's at-least, MultisetGe, is weaker than ge: both absorption laws hold for it too
  lemma AbsorptionGe(A: multiset<nat>, B: multiset<nat>, C: multiset<nat>)
    ensures Bags().gt(A, B) && MultisetGe<nat>(Gt, B, C) ==> Bags().gt(A, C)
    ensures MultisetGe<nat>(Gt, A, B) && Bags().gt(B, C) ==> Bags().gt(A, C)
  {
    GtTransitive();
    GtWellFounded();
    if Bags().gt(A, B) && MultisetGe<nat>(Gt, B, C) { MultisetGtGe<nat>(Gt, A, B, C); }
    if MultisetGe<nat>(Gt, A, B) && Bags().gt(B, C) { MultisetGeGt<nat>(Gt, A, B, C); }
  }

  // A permission is taken from the bag: the bag goes down with no hint about multiplicities (Fig. 15 of the
  // VeriFast paper)
  ghost function Take(bag: multiset<nat>, a: nat, X: multiset<nat>): nat
    requires bag == multiset{a} + X
    decreases bag by Bags()
  {
    if X == multiset{} then 0 else
      var b :| b in X;
      MultisetAddOne<nat>(Gt, a, X);
      assert multiset{b} + (X - multiset{b}) == X;
      Take(X, b, X - multiset{b})
  }

  // The lemmas of VeriFast's termination.gh, stated as its examples use them.  VeriFast's X < Y is
  // Bags().gt(Y, X) here, and its X <= Y is MultisetGe(Gt, Y, X).
  lemma VeriFastLemmas(x: nat, y: nat, m: nat, X: multiset<nat>, X1: multiset<nat>, X2: multiset<nat>,
                       Y: multiset<nat>, Z: multiset<nat>, L: multiset<nat>)
    // bag_lt_cons_lt
    ensures x < y ==> Bags().gt(multiset{y} + X, multiset{x} + X)
    // bag_lt_xs_cons_xs
    ensures Bags().gt(multiset{x} + X, X)
    // bag_le_bag_le_append_l
    ensures MultisetGe<nat>(Gt, X2, X1) ==> MultisetGe<nat>(Gt, X2 + Y, X1 + Y)
    // bag_le_append_r
    ensures MultisetGe<nat>(Gt, X + Y, Y)
    // bag_le_cons_xs_cons_ys
    ensures MultisetGe<nat>(Gt, Y, X) ==> MultisetGe<nat>(Gt, multiset{x} + Y, multiset{x} + X)
    // bag_le_bag_lt and bag_le_trans
    ensures MultisetGe<nat>(Gt, Y, X) && Bags().gt(Z, Y) ==> Bags().gt(Z, X)
    ensures MultisetGe<nat>(Gt, Y, X) && MultisetGe<nat>(Gt, Z, Y) ==> MultisetGe<nat>(Gt, Z, X)
    // one element for finitely many lower ones
    ensures (forall l :: l in L ==> l < m) ==> Bags().gt(multiset{m} + X, L + X)
    // an element above all of another bag (OuterDrop), and a sub-multiset (OuterSub)
    ensures m in X && (forall l :: l in L ==> l < m) ==> Bags().gt(X, L)
    ensures X <= Y ==> MultisetGe<nat>(Gt, Y, X)
    // irreflexivity
    ensures !Bags().gt(X, X)
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
  }

  // A continuation's stock is its level together with its continuation's stock; calling the continuation
  // gives up the level (VeriFast's continuation-passing example, Figs. 16 and 17 of the paper), so stocks of
  // nested continuations are ordered by the multiset order over bags
  ghost function Continue(stocks: multiset<multiset<nat>>, level: nat, rest: multiset<nat>): nat
    requires multiset{level} + rest in stocks
    decreases stocks by BagsOfBags()
  {
    if rest == multiset{} then 0 else
      var next :| next in rest;
      ContinueStep(stocks, level, rest);
      assert multiset{next} + (rest - multiset{next}) == rest;
      Continue(stocks - multiset{multiset{level} + rest} + multiset{rest}, next, rest - multiset{next})
  }

  lemma ContinueStep(stocks: multiset<multiset<nat>>, level: nat, rest: multiset<nat>)
    requires multiset{level} + rest in stocks
    ensures BagsOfBags().gt(stocks, stocks - multiset{multiset{level} + rest} + multiset{rest})
  {
    GtWellFounded();
    MultisetAddOne<nat>(Gt, level, rest);
    MultisetReplaceOne<multiset<nat>>(MultisetRel<nat>(Gt), stocks, multiset{level} + rest, multiset{rest});
  }
}
