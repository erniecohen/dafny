// Shared providers: all relations and well-foundedness arguments are checked source code.
module InstanceOrders {
  import opened Std.Termination.Orders

  ghost predicate {:termination_closed} Down(x: nat, y: nat) { x > y }

  lemma DownWellFounded() ensures wellFounded<nat>(Down) {
    forall c: Chain<nat> ensures stops<nat>(Down, c) {
      if forall n: nat :: Down(c(n), c(n + 1)) {
        DownBound(c(0) + 1);
      }
    }
  }

  lemma DownBound(n: nat)
    ensures forall c: Chain<nat> :: (forall k: nat :: Down(c(k), c(k + 1))) ==> c(n) + n <= c(0)
  {
    if n > 0 {
      DownBound(n - 1);
      forall c: Chain<nat> | forall k: nat :: Down(c(k), c(k + 1)) ensures c(n) + n <= c(0) {
        assert Down(c(n - 1), c(n));
      }
    }
  }

  ghost function {:termination_closed} Lex(): Wfo<seq<nat>> { DownWellFounded(); LexicographicOf(Down, 2) }
  ghost function {:termination_closed} Pointwise(): Wfo<seq<nat>> { DownWellFounded(); PointwiseOf(Down) }
  ghost function {:termination_closed} Product(): Wfo<(nat, nat)> { DownWellFounded(); ProductOf(Down, Down) }
  // Non-injective: the second field is deliberately ignored.
  function {:termination_closed} Rank(p: (nat, nat)): nat { p.0 }
  ghost function {:termination_closed} PulledBack(): Wfo<(nat, nat)> { DownWellFounded(); InverseImageOf(Down, Rank) }
}
