// Vacuity control for github-issue-211-same-bound.dfy: the same program, with an assert false
// after each call that the same-bound or smaller-bound rule checks.  Each must fail.
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
    if n == 0 then 0 else var r := t.F(n - 1); assert false; r
  }
  method HM(t: Tr.T, n: nat) returns (r: nat) decreases {:global} n {
    r := 0;
    if n != 0 {
      r := t.M(n - 1);
      assert false;
    }
  }
  // Same-bound calls may recurse: the global bound stays the same, and the ordinary decreases
  // clause goes down.
  function Count(t: Tr.T, n: nat, k: nat): nat decreases {:global} n decreases k {
    if k == 0 then var r := H(t, n); assert false; r else var r := Count(t, n, k - 1); assert false; r
  }
  // A callee's bound that agrees with the caller's on the caller's components and has more is not
  // above it: (n, 7) is below (n, TOP).
  function Longer(t: Tr.T, n: nat): nat decreases {:global} n, 7 {
    if n == 0 then 0 else var r := t.F(n - 1); assert false; r
  }
  // An imported closed helper publishes its checked closed promise instead of a bound.
  function {:termination_closed} Plus(a: nat, b: nat): nat { a + b }
}

module Y {
  import Tr
  import L
  class Y extends Tr.T {
    constructor() { }
    function F(n: nat): nat { var a := L.Count(this, n, 3); var b := L.Longer(this, n); assert false; L.Plus(a, b) }
    method M(n: nat) returns (r: nat) {
      r := L.HM(this, n);
      assert false;
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
    function G(t: Tr4.T, n: nat): nat { if n == 0 then 0 else var r := t.F(n - 1); assert false; r }
  }
}

module Y4 {
  import Tr4
  import C4
  class Y extends Tr4.T {
    const c: C4.C
    constructor(c: C4.C) { this.c := c; }
    function F(n: nat): nat { var r := c.G(this, n); assert false; r }
  }
}
