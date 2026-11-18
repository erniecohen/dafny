// A user order whose chain-stopping proof applies its total chain parameter.
module UserOrder {
  import opened Std.Termination.Orders

  datatype {:termination_closed} One extends Wfo<nat> = One {
    ghost predicate {:termination_closed} gt(x: nat, y: nat) { x == 1 && y == 0 }
    ghost function {:termination_closed} aha(c: Chain<nat>): (r: nat)
      ensures !gt(c(r), c(r + 1))
    {
      if c(0) == 1 && c(1) == 0 then 1 else 0
    }
  }

  ghost function {:termination_closed} Order(): (o: Wfo<nat>)
    ensures o == One
  { One }

  trait Counter {
    ghost function Next(n: nat): nat decreases {:global} n by Order()
  }

  ghost function Local(t: Counter): nat
    decreases {:global} (1 as nat) by Order()
  { t.Next(0) }

  // An ordinary call applies the actual argument: a closed lambda is allowed.
  ghost function Witness(): nat
    decreases {:global} 0
  { One().aha((n: nat) => 0) }

  // A quantifier-bound chain is logical at an ordinary call too.
  lemma LogicalCall()
    decreases {:global} 0
    ensures forall c: Chain<nat> :: !One().gt(c(One().aha(c)), c(One().aha(c) + 1))
  { }
}

module Client {
  import UserOrder

  ghost function Global(t: UserOrder.Counter): nat
    decreases {:global} (1 as nat) by UserOrder.Order()
  { t.Next(0) }

  ghost function Callback(g: nat -> nat): nat
    decreases {:global} (1 as nat) by UserOrder.Order()
    decreases {:callback g} (0 as nat) by UserOrder.Order()
  { g(0) }
}
