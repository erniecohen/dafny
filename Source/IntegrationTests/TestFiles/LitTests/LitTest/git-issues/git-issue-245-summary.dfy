// RUN: %verify --standard-libraries:false --type-system-refresh --general-traits=datatype --decreases-orders --modular-termination --solver-path %review-z3 "%s" > "%t"
// RUN: %exits-with 2 %verify --standard-libraries:false --decreases-orders --modular-termination --solver-path %review-z3 "%S/Inputs/git-issue-245-summary-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

module SummaryOrders {
  import opened Std.Termination.Orders

  ghost predicate {:termination_closed} Empty(x: nat, y: nat) { false }
  lemma {:termination_closed} EmptyWellFounded() ensures wellFounded<nat>(Empty) {
    forall c: Chain<nat> ensures stops<nat>(Empty, c) { assert !Empty(c(0), c(1)); }
  }
  // Reverse declaration order exercises the least fixpoint.
  ghost function {:termination_closed} Forward(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { Leaf(r) }
  ghost function {:termination_closed} Leaf(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { Of<nat>(r) }
  ghost function {:termination_closed} Wrapped(): Wfo<nat> {
    EmptyWellFounded(); Forward(Empty)
  }
}

module SummaryClient {
  import opened SummaryOrders
  trait Callee {
    function Step(n: nat): nat decreases {:global} n by Wrapped(), 0
  }
  function Call(c: Callee, n: nat): nat
    decreases {:global} n by Wrapped(), 1
  { c.Step(n) }
  function Callback(g: nat -> nat, n: nat): nat
    decreases {:global} n by Wrapped(), 1
    decreases {:callback g} n by Wrapped(), 0
  { g(0) }
}

// A refinement can retain an effect summary that its base already established.
abstract module SummaryTemplate {
  import opened Std.Termination.Orders
  ghost function {:termination_closed} Forward(r: (nat, nat) -> bool): Wfo<nat>
    requires wellFounded<nat>(r)
  { Of<nat>(r) }
}
module SummaryImplementation refines SummaryTemplate { }
module RefinedClient {
  import opened Std.Termination.Orders
  import opened SummaryOrders
  import P = SummaryImplementation
  ghost function {:termination_closed} Refined(): Wfo<nat> {
    EmptyWellFounded(); P.Forward(Empty)
  }
  trait Callee {
    function Step(n: nat): nat decreases {:global} n by Refined(), 0
  }
  function Call(c: Callee, n: nat): nat
    decreases {:global} n by Refined(), 1
  { c.Step(n) }
}
