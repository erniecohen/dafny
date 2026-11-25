include "git-issue-245-instances-mt.dfy"
module InstanceBadClient {
  import opened InstanceOrders
  import InstanceCallees

  function LexSame(c: InstanceCallees.Callee, s: seq<nat>): nat
    decreases {:global} s by Lex()
  { c.LexStep(s) } // error

  function PointwiseSame(c: InstanceCallees.Callee, s: seq<nat>): nat
    decreases {:global} s by Pointwise()
  { c.PointwiseStep(s) } // error

  function ProductSame(c: InstanceCallees.Callee, p: (nat, nat)): nat
    decreases {:global} p by Product()
  { c.ProductStep(p) } // error

  function InverseSame(c: InstanceCallees.Callee, p: (nat, nat)): nat
    decreases {:global} p by PulledBack()
  { c.InverseStep((p.0, p.1 + 1)) } // error
}
