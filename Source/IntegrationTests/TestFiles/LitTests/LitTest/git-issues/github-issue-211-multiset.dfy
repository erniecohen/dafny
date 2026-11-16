// https://github.com/erniecohen/dafny/issues/211
// The multiset order (Dershowitz-Manna) of the orders library, Std.Termination.Orders.MultisetOf, over any
// well-founded relation: a bag goes down when one or more of its elements are replaced by finitely many
// smaller ones.  It is the order of VeriFast's bags of call permission levels.  The library proves it
// well-founded, and transitive over a transitive relation.
// RUN: echo "the orders library, verified from its sources" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false "%repositoryRoot/Source/DafnyStandardLibraries/orders/dfyconfig.toml" >> "%t"
// RUN: echo "positive: one element for many smaller ones, the empty bag, pairs, bags of bags, a mixed tuple, absorption" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%s" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "controls (assert false in each positive context) and bags that do not go down" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries --type-system-refresh --general-traits=datatype --decreases-orders "%S/Inputs/github-issue-211-multiset-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: global bounds by multiset orders, the orders library verified with the client" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders --modular-termination "%S/Inputs/github-issue-211-multiset-mt.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: controls and bounds above the caller's" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders --modular-termination "%S/Inputs/github-issue-211-multiset-mt-verify.dfy" "%S/Inputs/github-issue-211-multiset-orders.dfy" >> "%t"
// RUN: echo "--modular-termination: a relation that is not known, passed to the library's multiset functions and lemmas" >> "%t"
// RUN: %exits-with 2 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders --modular-termination "%S/Inputs/github-issue-211-multiset-mt-resolve.dfy" >> "%t"
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
      MultisetReplace<nat>(Gt, bag, bag, multiset{});
      assert bag - bag + multiset{} == multiset{};
      1 + Clear(multiset{})
  }

  // and nothing is below it
  lemma NothingBelowEmpty(N: multiset<nat>) ensures !Bags().gt(multiset{}, N) {
    if MultisetGt<nat>(Gt, multiset{}, N) {
      var X: multiset<nat>, Y: multiset<nat> :|
        X != multiset{} && X <= multiset{} && N == multiset{} - X + Y && Dominated<nat>(Gt, X, Y);
      var x :| x in X;
    }
  }

  // A pair (a, 0) is exchanged for three pairs (a - 1, n), however large n is
  function Pairs(bag: multiset<(nat, nat)>, p: (nat, nat), n: nat): nat
    requires p in bag
    decreases bag by PairBags()
  {
    if p == (0, 0) then 0 else
      var q := if p.1 > 0 then (p.0, p.1 - 1) else (p.0 - 1, n);
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
      MultisetReplaceOne<nat>(Gt, bag, L, multiset{L - 1});
      Mixed(bag - multiset{L} + multiset{L - 1}, L - 1, 100)
  }

  // The order is transitive, so both absorption laws of the 211 plan hold
  lemma Absorption(A: multiset<nat>, B: multiset<nat>, C: multiset<nat>)
    ensures Bags().gt(A, B) && Bags().ge(B, C) ==> Bags().gt(A, C)
    ensures Bags().ge(A, B) && Bags().gt(B, C) ==> Bags().gt(A, C)
  {
    GtTransitive();
    MultisetTransitive<nat>(Gt);
    assert MultisetRel<nat>(Gt)(A, B) == Bags().gt(A, B);
    assert MultisetRel<nat>(Gt)(B, C) == Bags().gt(B, C);
    assert MultisetRel<nat>(Gt)(A, C) == Bags().gt(A, C);
  }
}
