// RUN: %exits-with 4 %verify --refinement-extends --modular-termination "%s" > "%t"
// RUN: %exits-with 4 %verify --refinement-extends --modular-termination --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --modular-termination "%S/Inputs/github-issue-88-modular-termination-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --modular-termination --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-modular-termination-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --modular-termination "%S/Inputs/github-issue-88-modular-termination-rejected-later.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --modular-termination --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-modular-termination-rejected-later.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 88 with --modular-termination: a parent added in a refinement (--refinement-extends) is
// subject to the rules of --modular-termination as if it had been written on a fresh declaration.
// An inherited member that becomes an override is compared with the bound of the member it now
// overrides, and an inherited call that the new override puts on a cycle through an open trait
// must terminate.  Each module that refines is followed by a one-module control that is rejected
// for the same reason, or accepted with an assert false that fails.

module Tr {
  trait {:termination false} {:axiom} S {
    function F(other: S, n: nat): int
      decreases {:global} n
  }
  trait {:termination false} {:axiom} Open {
    function H(n: nat): int
      decreases {:global} n
  }
}

// ----- The bound of an inherited member that becomes an override

module Bound0 {
  import Tr
  trait T {
    function F(other: Tr.S, n: nat): int // error (in Bound1): the bound is above that of Tr.S.F
      decreases {:global} n + 1
    {
      other.F(other, n) + 1
    }
    lemma Unfold(other: Tr.S, n: nat)
      decreases {:global} n + 1
      ensures F(other, n) == other.F(other, n) + 1
    {
    }
  }
  class C extends T {
    constructor () {}
  }
}

module Bound1 refines Bound0 {
  trait T ... extends Tr.S {}

  method WouldBeBad()
    decreases {:global} 1
    ensures false
  {
    var t := new C();
    var s: Tr.S := t;
    t.Unfold(s, 0);
    assert s.F(s, 0) == t.F(s, 0);
  }
}

module BoundControl {
  import Tr
  trait T extends Tr.S {
    function F(other: Tr.S, n: nat): int // error: the bound is above that of Tr.S.F
      decreases {:global} n + 1
    {
      other.F(other, n) + 1
    }
  }
}

// ----- A call that the new override puts on a cycle through an open trait

module Call0 {
  import Tr
  trait S0 {
    function K(other: Tr.S, n: nat): int
  }
  trait T {
    function K(other: Tr.S, n: nat): int
      decreases {:global} n + 1
    {
      other.F(other, n) + 1
    }
    lemma UnfoldK(other: Tr.S, n: nat)
      decreases {:global} n + 1
      ensures K(other, n) == other.F(other, n) + 1
    {
    }
  }
  class C extends T {
    constructor () {}
  }
  class X extends Tr.S {
    const s0: S0
    constructor (s0: S0)
      ensures this.s0 == s0
    {
      this.s0 := s0;
    }
    function F(other: Tr.S, n: nat): int {
      s0.K(other, n) + 1 // error (in Call1): s0.K may be T.K, which calls other.F
    }
    lemma UnfoldF(other: Tr.S, n: nat, k: int)
      requires k == s0.K(other, n)
      ensures F(other, n) == k + 1
    {
    }
  }
}

module Call1 refines Call0 {
  trait T ... extends S0 {}

  method WouldBeBad()
    ensures false
  {
    var t := new C();
    var x: X := new X(t);
    var s: Tr.S := x;
    t.UnfoldK(s, 0);
    var s0: S0 := t;
    ghost var k := t.K(s, 0);
    assert s0.K(s, 0) == k;
    x.UnfoldF(s, 0, k);
    assert s.F(s, 0) == x.F(s, 0);
  }
}

module CallControl {
  import Tr
  trait S0 {
    function K(other: Tr.S, n: nat): int
  }
  trait T extends S0 {
    function K(other: Tr.S, n: nat): int
      decreases {:global} n + 1
    {
      other.F(other, n) + 1
    }
  }
  class X extends Tr.S {
    const s0: S0
    constructor (s0: S0) {
      this.s0 := s0;
    }
    function F(other: Tr.S, n: nat): int {
      s0.K(other, n) + 1 // error: s0.K may be T.K, which calls other.F
    }
  }
}

// ----- An inherited member with no bound takes the bound of the member it now overrides

module Leaf0 {
  import Tr
  trait V {
    function H(n: nat): int {
      3
    }
  }
  class CV extends V {
    constructor () {}
  }
  trait W {
    function H(n: nat): int {
      if n == 0 then 0 else H(n - 1)
    }
  }
  class CW extends W {
    constructor () {}
  }
}

module Leaf1 refines Leaf0 {
  trait V ... extends Tr.Open {}
  trait W ... extends Tr.Open {}

  method M(n: nat) {
    var c := new CV();
    var o: Tr.Open := c;
    assert o.H(n) == c.H(n) == 3;
    var w: CW := new CW();
    o := w;
    assert o.H(0) == w.H(0) == 0;
    assert false; // error
  }
}

module LeafControl {
  import Tr
  trait V extends Tr.Open {
    function H(n: nat): int {
      3
    }
  }
  trait W extends Tr.Open {
    function H(n: nat): int {
      if n == 0 then 0 else H(n - 1)
    }
  }
  class CV extends V {
    constructor () {}
  }
  method M(n: nat) {
    var c := new CV();
    var o: Tr.Open := c;
    assert o.H(n) == c.H(n) == 3;
    assert false; // error
  }
}
