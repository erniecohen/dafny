// False controls for bounds with a component "e by O" (https://github.com/erniecohen/dafny/issues/245, M4):
// each dispatched call below fails the smaller-bound rule.

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

  // The same order under another name: a call compares a component by an order only if caller and callee
  // give it the same order expression.
  ghost function {:termination_closed} UpOrder2(): Wfo<nat> { UpWellFounded(); Of(Up) }
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

  // Error: the same bound is not below.
  function Same(t: Tr.T, i: nat): nat
    requires i <= 100
    decreases {:global} i by Ord.UpOrder()
  {
    t.F(i)
  }

  // Error: i - 1 is not below i in the order Up.
  function Wrong(t: Tr.T, i: nat): nat
    requires 0 < i <= 100
    decreases {:global} i by Ord.UpOrder()
  {
    t.F(i - 1)
  }

  // Error: the caller's bound names another order, so the components are not compared.
  function Other(t: Tr.T, i: nat): nat
    requires i < 100
    decreases {:global} i by Ord.UpOrder2()
  {
    t.F(i + 1)
  }

  // Error: the callback bound (i, 1) is not below the global bound (i, 1).
  function Apply(g: nat -> nat, i: nat): nat
    requires i <= 100
    decreases {:global} i by Ord.UpOrder(), 1
    decreases {:callback g} i by Ord.UpOrder(), 1
  {
    g(i)
  }
}
