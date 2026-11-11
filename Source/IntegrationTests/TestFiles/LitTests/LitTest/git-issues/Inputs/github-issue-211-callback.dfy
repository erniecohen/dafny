// Calls through function values under --modular-termination (https://github.com/erniecohen/dafny/issues/211,
// milestone M3b): positives.  "decreases {:callback g} E" gives the in-parameter g the bound E: an application
// of g is a call to code with bound E, so E must be below the caller's bound; the value passed for g must run
// under E, so a lambda passed for g has its body checked under E.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    method M(n: nat) returns (r: nat) decreases {:global} n
  }
}

module L {
  // (n, 0) is below (n, 1), which is below n (that is, (n, TOP)).
  function Apply(g: nat -> nat, n: nat): nat
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  {
    g(n)
  }
  method ApplyM(g: nat -> nat, n: nat) returns (r: nat)
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  {
    r := g(n);
  }
  // Forwarding: g's bound, (n, 0), is at most Apply's, (n, 0).
  function Twice(g: nat -> nat, n: nat): nat
    decreases {:global} n, 2
    decreases {:callback g} n, 0
  {
    Apply(g, n) + g(n)
  }
  // A partial function value and its precondition, which the callback bound covers too.
  function ApplyPartial(g: nat --> nat, n: nat): nat
    requires g.requires(n)
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  {
    g(n)
  }
  function Id(x: nat): nat { x }
}

module Y {
  import Tr
  import L
  class Y extends Tr.T {
    constructor() { }
    // Y.F inherits the bound n.  The lambda's body runs under Apply's callback bound, (n, 0), and dispatches
    // t.F(n - 1), whose bound n - 1 is below it.
    function F(n: nat): nat {
      if n == 0 then 0 else
        var t: Tr.T := this;
        L.Apply(_ => t.F(n - 1), n) + L.Twice(L.Id, n) + L.ApplyPartial(k requires k == n => t.F(k - 1), n)
    }
    method M(n: nat) returns (r: nat) {
      r := 0;
      if n != 0 {
        var t: Tr.T := this;
        r := L.ApplyM(_ => t.F(n - 1), n);
      }
    }
  }
  // A lambda applied where it is created is checked where it is created.
  function Here(t: Tr.T, n: nat): nat decreases {:global} n + 1 { ((x: nat) requires x <= n => t.F(x))(n) }
  // Without a bound, a caller may apply any function value.
  function Free(g: nat -> nat): nat { g(0) }
}
