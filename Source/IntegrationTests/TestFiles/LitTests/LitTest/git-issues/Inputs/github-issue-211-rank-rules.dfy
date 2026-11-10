// Milestone M4a of --modular-termination (https://github.com/erniecohen/dafny/issues/211): a global or
// callback bound may only use code that cannot reach a call dispatched to implementations that other modules
// may provide.  Each bound below can, by some route, and is rejected by the resolver, which names the route.

module Tr {
  trait {:termination false} T {
    function F(n: nat): nat
      decreases {:global} n
  }
  // A rank computed through dispatch.
  function R(t: T, n: nat): nat
    decreases {:global} n + 1
  {
    t.F(n)
  }
}

module Routes {
  import Tr

  // A rank through two helpers.
  function Q(t: Tr.T, n: nat): nat
    decreases {:global} n + 1
  {
    Tr.R(t, n)
  }

  // A constant whose initializer dispatches.
  class K {
    const k: nat := 0
    const t: Tr.T
    const viaDispatch: nat := t.F(0)
    constructor (t: Tr.T) { this.t := t; }
  }

  // A subset type whose constraint dispatches.
  type Good = t: Tr.T | t.F(0) == 0 witness *

  function ByHelper(t: Tr.T, n: nat): nat
    decreases {:global} Q(t, n)            // error: 'Routes.Q' -> 'Tr.R' -> dispatch of 'Tr.T.F'
  {
    0
  }

  function ByConstant(c: K, n: nat): nat
    decreases {:global} c.viaDispatch      // error: the initializer of 'Routes.K.viaDispatch'
  {
    0
  }

  function ByTypeTest(t: Tr.T, n: nat): nat
    decreases {:global} if t is Good then 1 else 0   // error: the constraint of 'Routes.Good'
  {
    0
  }

  function ByLambda(t: Tr.T, n: nat): nat
    decreases {:global} ((m: nat) => t.F(m))(n)     // error: dispatch of 'Tr.T.F', in a lambda's body
  {
    0
  }

  function ByValue(g: nat -> nat, n: nat): nat
    decreases {:global} g(n)               // error: a function value whose code is not known
  {
    0
  }

  function ByCallback(t: Tr.T, g: nat -> nat, n: nat): nat
    decreases {:global} n + 1
    decreases {:callback g} Tr.R(t, n)     // error: a callback bound, through 'Tr.R'
  {
    g(n)
  }

  function ClosedFine(t: Tr.T, c: K, n: nat): nat
    decreases {:global} n + c.k            // fine: no route to a dispatch
  {
    0
  }
}

module TraitBound {
  // A trait member's own bound calls a rank computed through dispatch.  The override inherits the bound,
  // which is reported once, where it is declared.
  trait {:termination false} U {
    function G(n: nat): nat
      decreases {:global} Rank(this)       // error: 'TraitBound.Rank' -> dispatch of 'TraitBound.U.G'
  }
  function Rank(u: U): nat
    decreases {:global} 8
  {
    u.G(0)
  }
}

module Override {
  import TraitBound
  class V extends TraitBound.U {
    function G(n: nat): nat { 0 }
  }
}
