// The same-bound rule of --modular-termination (https://github.com/erniecohen/dafny/issues/211,
// milestone M2): a call to known code that can reach a dispatched call runs under the caller's
// global bound, so the callee's own bound must not exceed the caller's.  Each program below
// terminates and verifies under the option.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    method M(n: nat) returns (r: nat) decreases {:global} n
  }
}

// A helper in another module, with a bound equal to its caller's, makes the dispatched call
// at a smaller argument.
module L {
  import Tr
  function H(t: Tr.T, n: nat): nat decreases {:global} n {
    if n == 0 then 0 else t.F(n - 1)
  }
  method HM(t: Tr.T, n: nat) returns (r: nat) decreases {:global} n {
    r := 0;
    if n != 0 {
      r := t.M(n - 1);
    }
  }
  // Same-bound calls may recurse: the global bound stays the same, and the ordinary decreases
  // clause goes down.
  function Count(t: Tr.T, n: nat, k: nat): nat decreases {:global} n decreases k {
    if k == 0 then H(t, n) else Count(t, n, k - 1)
  }
  // A callee's bound that agrees with the caller's on the caller's components and has more is not
  // above it: (n, 7) is below (n, TOP).
  function Longer(t: Tr.T, n: nat): nat decreases {:global} n, 7 {
    if n == 0 then 0 else t.F(n - 1)
  }
  // A helper that reaches no dispatched call needs no bound.
  function Plus(a: nat, b: nat): nat { a + b }
}

module Y {
  import Tr
  import L
  class Y extends Tr.T {
    constructor() { }
    function F(n: nat): nat { L.Plus(L.Count(this, n, 3), L.Longer(this, n)) }
    method M(n: nat) returns (r: nat) {
      r := L.HM(this, n);
    }
  }
  // A caller without a bound is above every bound, so its calls to unbounded helpers hold.
  function Unbounded(t: Tr.T, n: nat): nat { t.F(n) }
  method Main() {
    var y := new Y();
    var a := Unbounded(y, 3);
    var b := y.M(3);
    print a, " ", b, "\n";
  }
}

// A call to a bodyless member of a trait that is not open is dispatched to an implementation in
// the same module.  The member reaches the open dispatch through its implementation, so its bound
// is compared with the caller's.
module Tr4 {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module C4 {
  import Tr4
  trait C {
    function G(t: Tr4.T, n: nat): nat decreases {:global} n
  }
  class K extends C {
    constructor() { }
    function G(t: Tr4.T, n: nat): nat { if n == 0 then 0 else t.F(n - 1) }
  }
}

module Y4 {
  import Tr4
  import C4
  class Y extends Tr4.T {
    const c: C4.C
    constructor(c: C4.C) { this.c := c; }
    function F(n: nat): nat { c.G(this, n) }
  }
}
