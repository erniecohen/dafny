// False controls: the positive program of github-issue-266-m2.dfy, with a reachable "assert false" in each family as
// its only failures.
// Applications under a global bound, by the call sugar, "erase", "requires" and "reads".
module Apply {
  function Apply3(c: bounded<nat, int -> int>, n: nat): int
    requires c.bound < n
    decreases {:global} n
  {
    assert false; // error: the only failure
    c(3) + c.erase(3)
  }
  // Code without a bound is above every bound.
  function ApplyTop(c: bounded<nat, int -> int>): int {
    c(3)
  }
  ghost function Reads(c: bounded<nat, int ~> int>, n: nat): set<object?>
    requires c.bound < n && c.requires(3)
    reads c.reads(3)
    decreases {:global} n
  {
    c.reads(3)
  }
  method Method(c: bounded<ORDINAL, int -> int>, o: ORDINAL) returns (r: int)
    requires c.bound < o
    decreases {:global} o
  {
    r := c(3);
    assert false; // error: the only failure
  }
}

// Formation from a lambda, from a bounded value (copy or widen), and from a named function.
module Form {
  import opened Apply
  method Lambda() returns (r: int) {
    var d := bounded.certify(5, (x: int) requires x > 0 => x + 1);
    assert d.bound == 5 && d.requires(1) && d(2) == 3;
    r := d(2);
    assert false; // error: the only failure
    var o := bounded.certify(7 as ORDINAL, (x: int) => x);
    var p: bounded<ORDINAL, int -> int> := o;
    assert p.bound == 7 as ORDINAL;
  }
  function Widen(c: bounded<nat, int -> int>): bounded<nat, int -> int>
    requires c.bound <= 7
  {
    bounded.certify(7, c.erase)
  }
  // A lambda applies a captured value under its own bound.
  function Nest(c: bounded<nat, int -> int>): bounded<nat, int -> int>
    requires c.bound < 9
  {
    assert false; // error: the only failure
    bounded.certify(9, x => c(x) + 1)
  }
  // A lambda calls a function at its own bound (same-bound rule).
  function G(n: nat): int
    decreases {:global} 5
  {
    if n == 0 then 0 else Apply3(bounded.certify(3, x => x), 4)
  }
  // Forwarding to a parameter with a callback bound.
  function Use(f: int -> int, k: nat): int
    decreases {:callback f, k}
  {
    0
  }
  function Forward(c: bounded<nat, int -> int>): int
    requires c.bound <= 4
  {
    Use(c.erase, 4)
  }
}

// A named function is admitted with its own code: its bound must not exceed the bound given.
module Named {
  trait {:termination false} T extends object {
    function f(): int
      decreases {:global} 1
  }
  function High(t: T): int
    decreases {:global} 2
  {
    t.f()
  }
  function Leaf(t: T): int {
    0
  }
  class K {
    const n: int
    function Get(t: T): int
      decreases {:global} 3
    {
      t.f() + n
    }
  }
  method Certify(k: K) {
    var h := bounded.certify(2, High);
    var g := bounded.certify(4, k.Get);
    // A function that reaches no dispatched call fits every bound.
    var l := bounded.certify(0, Leaf);
    assert false; // error: the only failure
  }
}

// Stored bounded values: a constant field holds a call budget that a member spends later.
module Stored {
  trait {:termination false} {:termination_closed} T extends object {
    function f(): int
      ensures f() == 0
      decreases {:global} 1
  }
  function High(t: T): int
    ensures High(t) == 0
    decreases {:global} 2
  {
    t.f()
  }
}
module Observer {
  import Stored
  type Zero = h: bounded<nat, Stored.T -> int> | h.bound == 0
    ghost witness bounded.certify(0, (t: Stored.T) => 0)
  class C extends Stored.T {
    const cb: Zero
    constructor (h: Zero) { cb := h; }
    function f(): int
      ensures f() == 0
    {
      var ignored := cb(this);
      0
    }
  }
  method Main() {
    var h: Zero := bounded.certify(0, (t: Stored.T) => 0);
    var c := new C(h);
    var result := Stored.High(c);
    assert result == 0;
    assert false; // error: the only failure
  }
}

// A mutable field, a sequence and a result hold bounded values; each application checks the bound of the value
// read in the current state, so replacing a value cannot close a loop.
module Mutable {
  class Iterator {
    var step: bounded<nat, int -> int>
    var pending: seq<bounded<nat, int -> int>>
    constructor (s: bounded<nat, int -> int>, rest: seq<bounded<nat, int -> int>>)
      ensures step == s && pending == rest
    {
      step := s;
      pending := rest;
    }
    method Next(x: int) returns (r: int)
      requires step.bound < 3 && forall i :: 0 <= i < |pending| ==> pending[i].bound < 3
      modifies this
      ensures step.bound < 3 && forall i :: 0 <= i < |pending| ==> pending[i].bound < 3
      decreases {:global} 3
    {
      assert false; // error: the only failure
      r := step(x);
      if pending != [] {
        step := pending[0];
        pending := pending[1..];
      }
    }
  }
  function Make(k: nat): bounded<nat, int -> int>
    ensures Make(k).bound == k
  {
    bounded.certify(k, x => x + 1)
  }
  method Main() {
    var it := new Iterator(Make(2), [Make(1)]);
    it.pending := it.pending + [Make(0)];
    var a := it.Next(0);
    var b := it.Next(a);
  }
}
