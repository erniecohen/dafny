// The classifier's hints (--show-hints): which calls need the smaller-bound rule.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    function G(n: nat): nat
    function H(n: nat): nat { n }  // has a body, so a call to it is not dispatched
  }
}

module Client {
  import Tr
  function Calls(t: Tr.T, f: nat -> nat, n: nat): nat
    decreases {:global} n + 1
  {
    t.F(n)          // dispatched, checked
    + t.G(n)        // dispatched, rejected: G has no global bound
    + t.H(n)        // not dispatched
    + f(n)          // through a function value
    + (x => x)(n)   // a lambda literal: known code
  }
  function NoBound(t: Tr.T, n: nat): nat { t.F(n) }  // holds: no caller bound
  method Collections(f: nat -> nat, n: nat) {
    var s := seq(n, f);                   // through a function value
    var s' := seq(n, i => i);             // known code
    var a := new nat[n](f);               // through a function value
  }
}
