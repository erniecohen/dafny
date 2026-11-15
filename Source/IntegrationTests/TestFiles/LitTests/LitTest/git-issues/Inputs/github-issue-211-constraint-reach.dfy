// A helper that mentions a type whose constraint reaches a dispatched call reaches it too
// (https://github.com/erniecohen/dafny/issues/211, milestone M3a): a call to it from a member with a bound
// is checked by the same-bound rule.  Each call marked "rejected" fails; the other holds.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module S {
  import Tr
  type Good = t: Tr.T | t.F(0) == 0 witness *
  // No bound: it may test Good, and so it reaches the dispatched call.
  function IsGood(t: Tr.T): bool { t is Good }
  // Reaches no dispatched call.
  function Plain(t: Tr.T): bool { true }
}

module Y {
  import Tr
  import S
  class Y extends Tr.T {
    constructor() { }
    // Rejected: the unbounded helper reaches the constraint's dispatched call.
    function F(n: nat): nat { var me: Tr.T := this; if S.IsGood(me) && S.Plain(me) then 1 else 0 }
  }
}
