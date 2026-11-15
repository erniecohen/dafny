// Function values that the resolver rejects under --modular-termination
// (https://github.com/erniecohen/dafny/issues/211, milestone M3b).  Each is marked "rejected".
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    // Rejected: the parameter of a callback clause must be an in-parameter of an arrow type.
    function G(n: nat): nat decreases {:global} n decreases {:callback n} 0
    function H(g: nat -> nat, n: nat): nat decreases {:global} n + 1 decreases {:callback g} n
  }
}

module L {
  import Tr
  function Apply(g: nat -> nat, n: nat): nat
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  {
    g(n)
  }
  datatype D extends Tr.T = D {
    function F(n: nat): nat { 0 }
    function G(n: nat): nat { 0 }
    // Rejected: an override has the callback bounds of the member it overrides.
    function H(g: nat -> nat, n: nat): nat decreases {:callback g} n { g(n) }
  }
}

module Y {
  import Tr
  import L
  datatype Y extends Tr.T = Y(cb: nat -> nat) {
    function F(n: nat): nat {
      // Rejected: a function value whose code is not known, applied under a bound.
      cb(n) +
      // Rejected: a sequence constructor's initializer whose code is not known.
      |seq(n, cb)| +
      // Rejected: a value for a parameter with a callback bound that is neither a lambda, nor such a
      // parameter, nor a function that reaches no dispatched call.
      L.Apply(cb, n)
    }
    function G(n: nat): nat { 0 }
    function H(g: nat -> nat, n: nat): nat { g(n) }
  }
  // Rejected: the precondition of a function value whose code is not known, under a bound.
  ghost function Pre(cb: nat --> nat, n: nat): bool decreases {:global} n { cb.requires(n) }
  // Rejected: a function with a callback parameter as a value.
  function Value(): (nat -> nat, nat) -> nat { L.Apply }
  method Arr(cb: nat -> nat, n: nat) returns (a: array<nat>) decreases {:global} n {
    // Rejected: an array's initializer whose code is not known.
    a := new nat[n](cb);
  }
}
