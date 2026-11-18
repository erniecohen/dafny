// Resolution errors for github-issue-211-order-types.dfy: an {:order O} attribute must name a closed order of the
// type's base type (R1, R2), and the order of a bounded type is an order type, not its carrier.

module Bad {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  type {:order Bags()} WrongBase = m: multiset<int> | true // error: R1, the order is not on the base type
  type {:order 3} NotAnOrder = n: nat | true // error: R1, not a Wfo
  class C { var o: Wfo<multiset<nat>> }
  const c: C
  type {:order c.o} Heap = m: multiset<nat> | true // error: R2, reads the heap
  type {:order Bags(), Bags()} Two = m: multiset<nat> | true // error: one argument
  type {:order Bags()} {:axiom} Axiom = m: multiset<nat> | true // error: not {:axiom}
  type {:order Bags()} Param<X> = m: multiset<nat> | true // error: no type parameters
}

// The order of a bounded type is an order type, not its carrier
module Carrier {
  import opened MultisetOrders
  type {:termination_closed} {:order Bags()} Level = m: multiset<nat> | true
  method Widen(f: bounded<Level, int -> int>) returns (g: bounded<multiset<nat>, int -> int>) { // error: multiset<nat> has no order
    g := f;
  }
}
