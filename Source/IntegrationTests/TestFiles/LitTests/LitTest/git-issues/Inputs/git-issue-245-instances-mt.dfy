include "git-issue-245-instance-orders.dfy"

module InstanceCallees {
  import opened InstanceOrders
  trait Callee {
    function LexStep(s: seq<nat>): nat decreases {:global} s by Lex()
    function PointwiseStep(s: seq<nat>): nat decreases {:global} s by Pointwise()
    function ProductStep(p: (nat, nat)): nat decreases {:global} p by Product()
    function InverseStep(p: (nat, nat)): nat decreases {:global} p by PulledBack()
  }
}

module InstanceClient {
  import opened Std.Termination.Orders
  import opened InstanceOrders
  import InstanceCallees

  function LexCall(c: InstanceCallees.Callee, n: nat): nat
    requires n > 0
    decreases {:global} [n] by Lex()
  { c.LexStep([n - 1, 100]) }

  function PointwiseCall(c: InstanceCallees.Callee, s: seq<nat>, i: nat): nat
    requires i < |s| && s[i] > 0
    decreases {:global} s by Pointwise()
  {
    assert PointwiseGt<nat>(Down, s, s[i := s[i] - 1]);
    c.PointwiseStep(s[i := s[i] - 1])
  }

  function ProductCall(c: InstanceCallees.Callee, p: (nat, nat)): nat
    requires p.1 > 0
    decreases {:global} p by Product()
  { c.ProductStep((p.0, p.1 - 1)) }

  function InverseCall(c: InstanceCallees.Callee, p: (nat, nat)): nat
    requires p.0 > 0
    decreases {:global} p by PulledBack()
  { c.InverseStep((p.0 - 1, p.1 + 100)) }

  function LexCallback(g: nat -> nat, s: seq<nat>): nat
    decreases {:global} s by Lex(), 1
    decreases {:callback g} s by Lex(), 0
  { g(0) }

  function PointwiseCallback(g: nat -> nat, s: seq<nat>): nat
    decreases {:global} s by Pointwise(), 1
    decreases {:callback g} s by Pointwise(), 0
  { g(0) }

  function ProductCallback(g: nat -> nat, p: (nat, nat)): nat
    decreases {:global} p by Product(), 1
    decreases {:callback g} p by Product(), 0
  { g(0) }

  function InverseCallback(g: nat -> nat, p: (nat, nat)): nat
    decreases {:global} p by PulledBack(), 1
    decreases {:callback g} p by PulledBack(), 0
  { g(0) }
}
