// Each accepted rule, attacked: every marked line must fail, at the check named.
module Rules {
  function ApplyBad(c: bounded<nat, int -> int>, n: nat): int
    decreases {:global} n
  {
    c(3) // error: c.bound < n is not known
  }
  ghost function ReadsBad(c: bounded<nat, int ~> int>, n: nat): set<object?>
    requires c.requires(3)
    reads c.reads(3)
    decreases {:global} n
  {
    c.reads(3) // error: c.bound < n is not known
  }
  method OrdinalFromNat(c: bounded<ORDINAL, int -> int>)
    decreases {:global} 5
  {
    var x := c(0); // error: an ORDINAL bound is not below a nat one
  }
  function NatFromOrdinal(c: bounded<nat, int -> int>, o: ORDINAL): int
    requires c.bound < 3 && 3 < o
    decreases {:global} o
  {
    c(0) // error: a nat bound is not below an ORDINAL one
  }
  function Use(f: int -> int, k: nat): int
    decreases {:callback f, k}
  {
    0
  }
  function ForwardBad(c: bounded<nat, int -> int>): int {
    Use(c.erase, 4) // error: c.bound <= 4 is not known
  }
  function Narrow(c: bounded<nat, int -> int>): bounded<nat, int -> int> {
    bounded.certify(2, c.erase) // error: c.bound <= 2 is not known
  }
  function NestBad(c: bounded<nat, int -> int>): bounded<nat, int -> int> {
    bounded.certify(9, x => c(x) + 1) // error: c.bound < 9 is not known
  }
  method Negative() {
    var bad := bounded.certify(-1 as nat, (x: int) => x); // error: -1 is not a nat
  }
  method Precondition() {
    var d := bounded.certify(5, (x: int) requires x > 0 => x + 1);
    var e := d(0); // error: precondition
  }
}

// Landin's knot through a constant field: the stored value runs under its bound, and code that applies it
// without a bound of its own cannot be called from code with a bound.
module Knot {
  type Zero = h: bounded<nat, C -> int> | h.bound == 0
    ghost witness bounded.certify(0, (c: C) => 0)
  class C {
    const cb: Zero
    constructor (h: Zero) { cb := h; }
    function Top(): int {
      cb(this)
    }
  }
  function Bounded(c: C): int
    decreases {:global} 0
  {
    c.Top() // error: the callee has no bound
  }
  method Close() {
    var h: Zero := bounded.certify(0, (c: C) => c.Top()); // error: the callee has no bound
  }
  method Self() {
    var h: Zero := bounded.certify(0, (c: C) => c.cb(c)); // error: 0 < 0
  }
}
