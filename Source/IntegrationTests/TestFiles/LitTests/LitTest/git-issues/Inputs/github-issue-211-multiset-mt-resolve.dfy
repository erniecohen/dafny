// The orders library's multiset functions and lemmas apply the relation passed to them: under --modular-termination
// a relation that is code not known (here a parameter) breaks a {:termination_closed} promise, as an application of
// it would (github-issue-211-multiset.dfy).

module Unknown {
  import opened Std.Termination.Orders

  // The relation is a parameter, code that is not known: the library's lemmas and functions apply it
  function {:termination_closed} ByLemma(r: (nat, nat) -> bool, bag: multiset<nat>, L: nat): nat
    requires L in bag
  {
    MultisetReplaceOne<nat>(r, bag, L, multiset{});
    0
  }
  ghost function {:termination_closed} ByRel(r: (nat, nat) -> bool, M: multiset<nat>, N: multiset<nat>): bool {
    MultisetRel<nat>(r)(M, N)
  }
  ghost function {:termination_closed} ByGt(r: (nat, nat) -> bool, M: multiset<nat>, N: multiset<nat>): bool {
    MultisetGt<nat>(r, M, N)
  }
  ghost function {:termination_closed} ByGe(r: (nat, nat) -> bool, M: multiset<nat>, N: multiset<nat>): bool {
    MultisetGe<nat>(r, M, N)
  }
}
