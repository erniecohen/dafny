// Bounds of --modular-termination with a component "e by O" (https://github.com/erniecohen/dafny/issues/245, M4).
// The orders library is loaded and verified with this file.

// The order "up to 100" on nat.  Its proof binds each chain with a quantifier (the binder rule): applying
// such a variable calls nothing, so the order's code reaches no open dispatch.
module Ord {
  import opened Std.Termination.Orders

  ghost predicate Up(x: nat, y: nat) { x < y <= 100 }

  lemma UpWellFounded() ensures wellFounded<nat>(Up) {
    forall c: Chain<nat> ensures stops<nat>(Up, c) {
      if forall n: nat :: Up(c(n), c(n + 1)) {
        Climbs(101);
        assert Up(c(101), c(102));
      }
    }
  }

  lemma Climbs(n: nat)
    ensures forall c: Chain<nat> :: (forall k: nat :: Up(c(k), c(k + 1))) ==> c(n) >= n
  {
    if n > 0 {
      Climbs(n - 1);
      forall c: Chain<nat> | forall k: nat :: Up(c(k), c(k + 1)) ensures c(n) >= n {
        assert Up(c(n - 1), c(n));
      }
    }
  }

  // Closed, so that other modules may use it in their bounds.
  ghost function {:termination_closed} UpOrder(): Wfo<nat> { UpWellFounded(); Of(Up) }

  // A bound by the order inside its own module: a dispatched call climbs.
  trait {:termination false} Counter {
    function Next(i: nat): nat requires i <= 100 decreases {:global} i by UpOrder()
  }

  function Step(c: Counter, i: nat): nat
    requires i < 100
    decreases {:global} i by UpOrder()
  {
    c.Next(i + 1)
  }
}

module Tr {
  import Ord
  trait {:termination false} T {
    function F(i: nat): nat requires i <= 100 decreases {:global} i by Ord.UpOrder()
  }
}

module Client {
  import Ord
  import Tr

  // The dispatched callee's bound i + 1 is below the caller's bound i in the order Up.
  function Caller(t: Tr.T, i: nat): nat
    requires i < 100
    decreases {:global} i by Ord.UpOrder()
  {
    t.F(i + 1)
  }

  // A callback bound (i, 0) by the order, below the global bound (i, 1).
  function Apply(g: nat -> nat, i: nat): nat
    requires i <= 100
    decreases {:global} i by Ord.UpOrder(), 1
    decreases {:callback g} i by Ord.UpOrder(), 0
  {
    g(i)
  }

  method M(t: Tr.T, i: nat) returns (r: nat)
    requires i < 100
    decreases {:global} i by Ord.UpOrder()
  {
    r := t.F(i + 1);
  }
}
