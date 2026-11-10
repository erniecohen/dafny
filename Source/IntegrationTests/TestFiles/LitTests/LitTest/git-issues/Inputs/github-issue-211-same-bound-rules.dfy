// Calls that the same-bound rule of --modular-termination rejects
// (https://github.com/erniecohen/dafny/issues/211, milestone M2).  Each is marked "rejected";
// every other call holds.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    method M(n: nat) returns (r: nat) decreases {:global} n
  }
}

module L {
  import Tr
  // Unbounded helpers that reach a dispatched call.
  function H(t: Tr.T, n: nat): nat { if n == 0 then 0 else t.F(n - 1) }
  method HM(t: Tr.T, n: nat) returns (r: nat) {
    r := 0;
    if n != 0 {
      r := t.M(n - 1);
    }
  }
  // Bounded helpers.
  function Same(t: Tr.T, n: nat): nat decreases {:global} n { if n == 0 then 0 else t.F(n - 1) }
  function Up(t: Tr.T, n: nat): nat decreases {:global} n + 1 { t.F(n) }
}

module Y {
  import Tr
  import L
  class Y extends Tr.T {
    // Rejected: an unbounded helper that reaches a dispatched call, called with a bound.
    function F(n: nat): nat { L.H(this, n) }
    // Rejected: the same, for a method.
    method M(n: nat) returns (r: nat) {
      r := L.HM(this, n);
    }
  }
  class Y2 extends Tr.T {
    // Rejected: the helper's bound, n + 1, is above the caller's, n.
    function F(n: nat): nat { L.Up(this, n) }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
  // Rejected: the helper's bound, n, is (n, TOP), which is above the caller's (n, 0).
  function Shorter(t: Tr.T, n: nat): nat decreases {:global} n, 0 { L.Same(t, n) }
}

// The method of a function's "by method" body has the function's bound, inherited here from the
// trait member.
module Y5 {
  import Tr
  class Y extends Tr.T {
    function F(n: nat): nat { 0 } by method {
      // Rejected: the dispatched callee's bound, n, is not below the caller's, n.
      var t: Tr.T := this;
      var r := t.F(n);
      return 0;
    }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
}

// A bodyless member of a trait that is not open reaches the dispatched call through its
// implementation in the same module.
module C6 {
  import Tr
  trait C {
    function G(t: Tr.T, n: nat): nat
  }
  class K extends C {
    function G(t: Tr.T, n: nat): nat { if n == 0 then 0 else t.F(n - 1) }
  }
}

module Y6 {
  import Tr
  import C6
  class Y extends Tr.T {
    const c: C6.C
    constructor(c: C6.C) { this.c := c; }
    // Rejected: C6.C.G has no bound.
    function F(n: nat): nat { c.G(this, n) }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
}

// An iterator has no global bound.
module Y7 {
  import Tr
  iterator Iter(t: Tr.T, n: nat) yields (r: nat) {
    if n != 0 {
      r := t.F(n - 1);
      yield;
    }
  }
  class Y extends Tr.T {
    function F(n: nat): nat { 0 }
    method M(n: nat) returns (r: nat) {
      var it := new Iter(this, n);
      // Rejected: the iterator reaches the dispatched call, and has no bound.
      var more := it.MoveNext();
      r := 0;
    }
  }
}

// A parameter's default value is evaluated as part of a call that omits the argument, so it is
// checked with the callee: it reaches the dispatched call through H.
module L8 {
  import Tr
  function H(t: Tr.T, n: nat): nat { if n == 0 then 0 else t.F(n - 1) }
  function D(t: Tr.T, n: nat, k: nat := H(t, n)): nat { k }
  // Rejected: H has no bound, and the default value runs under E's bound.
  function E(t: Tr.T, n: nat, k: nat := H(t, n)): nat decreases {:global} n { k }
}

module Y8 {
  import Tr
  import L8
  class Y extends Tr.T {
    // Rejected: D has no bound, and reaches the dispatched call through its default value.
    function F(n: nat): nat { L8.D(this, n) }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
}
