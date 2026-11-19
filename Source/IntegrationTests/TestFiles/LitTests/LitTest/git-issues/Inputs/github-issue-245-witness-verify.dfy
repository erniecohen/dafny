include "github-issue-245-witness-positive.dfy"

module Controls {
  import opened Std.Termination.Orders
  import UserOrder

  // A constant result cannot prove an everywhere-true relation well-founded.
  datatype Bad extends Wfo<nat> = Bad {
    ghost predicate gt(x: nat, y: nat) { true }
    ghost function aha(c: Chain<nat>): (r: nat)
      ensures !gt(c(r), c(r + 1))
    { var x := c(0); 0 }
  }

  ghost function EqualBound(t: UserOrder.Counter): nat
    decreases {:global} (0 as nat) by UserOrder.Order()
  { t.Next(0) }

  ghost function ReverseBound(t: UserOrder.Counter): nat
    decreases {:global} (0 as nat) by UserOrder.Order()
  { t.Next(1) }

  lemma Nonvacuity()
    decreases {:global} (1 as nat) by UserOrder.Order()
  {
    assert UserOrder.One().gt(1, 0);
    assert false;
  }
}
