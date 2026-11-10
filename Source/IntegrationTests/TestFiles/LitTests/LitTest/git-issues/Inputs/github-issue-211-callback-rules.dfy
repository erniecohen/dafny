// Calls through function values that the verifier rejects under --modular-termination
// (https://github.com/erniecohen/dafny/issues/211, milestone M3b).  Each is marked "rejected"; every other
// call holds.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module L {
  function Apply(g: nat -> nat, n: nat): nat
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  {
    g(n)
  }
  // Rejected: the callback's bound, n, is not below the global bound, n.
  function Level(g: nat -> nat, n: nat): nat
    decreases {:global} n
    decreases {:callback g} n
  {
    g(n)
  }
  // Rejected: the forwarded callback's bound, (n, 1), exceeds Apply's, (n, 0).
  function Forward(g: nat -> nat, n: nat): nat
    decreases {:global} n, 2
    decreases {:callback g} n, 1
  {
    Apply(g, n)
  }
  // An unbounded helper that applies a function value reaches code that is not known.
  function Unbounded(g: nat -> nat, n: nat): nat { g(n) }
}

module Y {
  import Tr
  import L
  class Y extends Tr.T {
    constructor() { }
    function F(n: nat): nat {
      var t: Tr.T := this;
      // Rejected: the lambda's body runs under Apply's callback bound, (n, 0), and t.F(n) is not below it.
      L.Apply(_ => t.F(n), n) +
      // Rejected: the helper reaches code that is not known, and it has no bound.
      L.Unbounded(x => x, n)
    }
  }
  // Rejected: the lambda's body runs under the callback bound even where the caller has no bound.
  function Free(t: Tr.T, n: nat): nat { L.Apply(_ => t.F(n), n) }
  // An escaped lambda: a lambda that a function returns is checked under the bound of the function that creates
  // it, wherever it is applied later.  Rejected: t.F(n) is not below the creator's bound n.
  function Escape(t: Tr.T, n: nat): nat -> nat decreases {:global} n { x => t.F(n) }
  // Holds: n is below the creator's bound n + 1.
  function EscapeOk(t: Tr.T, n: nat): nat -> nat decreases {:global} n + 1 { x => t.F(n) }
}
