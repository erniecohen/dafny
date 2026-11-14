// False controls for bounds with a component "e by O" (https://github.com/erniecohen/dafny/issues/245, M4): a bound
// may not reach open dispatch, and the binder rule covers only variables of a total arrow type bound by a quantifier.

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

  // A proof that passes the chain to a lemma as a parameter: the lemma applies a value whose code is not known.
  lemma ParamWellFounded() ensures wellFounded<nat>(Up) {
    forall c: Chain<nat> ensures stops<nat>(Up, c) {
      if forall n: nat :: Up(c(n), c(n + 1)) {
        ParamClimbs(c, 101);
        assert Up(c(101), c(102));
      }
    }
  }

  lemma ParamClimbs(c: Chain<nat>, n: nat)
    requires forall k: nat :: Up(c(k), c(k + 1))
    ensures c(n) >= n
  {
    if n > 0 { ParamClimbs(c, n - 1); }
  }

  ghost function ParamOrder(): Wfo<nat> { ParamWellFounded(); Of(Up) }

  // A lemma that quantifies over a general arrow type: the binder rule does not cover it.
  lemma General() ensures forall g: nat ~> nat {:trigger g.requires(0)} :: g.requires(0) ==> g(0) == g(0) {}

  ghost function GeneralOrder(): Wfo<nat> { General(); UpOrder() }

  // Not closed, so other modules may not use it in their bounds.
  ghost function OpenUpOrder(): Wfo<nat> { UpOrder() }

  trait {:termination false} Counter {
    function Next(i: nat): nat requires i <= 100 decreases {:global} i by UpOrder()
  }

  // Error: the bound's order calls a lemma that applies a function-typed parameter.
  function ByParam(c: Counter, i: nat): nat
    requires i < 100
    decreases {:global} i by ParamOrder()
  {
    c.Next(i + 1)
  }

  // Error: the bound's order applies a variable of type nat ~> nat bound by a quantifier.
  function ByGeneral(c: Counter, i: nat): nat
    requires i < 100
    decreases {:global} i by GeneralOrder()
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

  // Error: the bound applies a function-typed parameter.
  function Param(t: Tr.T, g: nat -> nat, i: nat): nat
    requires i < 100
    decreases {:global} g(i) by Ord.UpOrder()
  {
    0
  }

  // Error: the bound applies a lambda that it formed.
  function Lambda(t: Tr.T, i: nat): nat
    requires i < 100
    decreases {:global} (var f := (x: nat) => x; f(i)) by Ord.UpOrder()
  {
    0
  }

  // Error: the order is not closed, so this module may not use it in a bound.
  function NotClosed(t: Tr.T, i: nat): nat
    requires i < 100
    decreases {:global} i by Ord.OpenUpOrder()
  {
    t.F(i + 1)
  }
}
