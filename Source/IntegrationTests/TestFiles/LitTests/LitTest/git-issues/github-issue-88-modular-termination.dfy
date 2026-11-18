// RUN: %exits-with 4 %verify --refinement-extends --modular-termination "%s" > "%t"
// RUN: %exits-with 4 %verify --refinement-extends --modular-termination --type-system-refresh=false --general-newtypes=false "%s" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --modular-termination "%S/Inputs/github-issue-88-modular-termination-rejected.dfy" >> "%t"
// RUN: %exits-with 2 %resolve --refinement-extends --modular-termination --type-system-refresh=false --general-newtypes=false "%S/Inputs/github-issue-88-modular-termination-rejected.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny build --target:lib --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination "%S/Inputs/github-issue-88-modular-termination-library.dfy" --output "%t-mt.doo" >> "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --library "%t-mt.doo" "%S/Inputs/github-issue-88-modular-termination-client.dfy" >> "%t"
// RUN: %exits-with 1 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --refinement-extends --library "%t-mt.doo" "%S/Inputs/github-issue-88-modular-termination-client.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Issue 88 with --modular-termination: a parent added in a refinement (--refinement-extends) is
// subject to the rules of --modular-termination as if it had been written on a fresh declaration.
// An inherited member that becomes an override is compared with the bound of the member it now
// overrides, an inherited call that the new override puts on a cycle must terminate, and an
// inherited member that becomes an implementation of a member of another module that has no
// bound must be a leaf (the input file).  Each module that refines is followed by a one-module
// control that is rejected for the same reason, or accepted with an assert false that fails.
//
// The last three RUN lines: under --modular-termination a library and its client agree on
// --refinement-extends, so a library built without it loads for a client that does not use the
// option and is refused for one that does.
//
// An inherited member that acquires a bound from the added parent must also recheck its body.
// The acquisition and refined-callee controls live in github-issue-493-refinement.dfy
// (https://github.com/erniecohen/dafny/issues/493).

module Tr {
  trait S {
    function F(other: S, n: nat): int
      decreases {:global} n
  }
  trait Open {
    function H(n: nat): int
      decreases {:global} n
  }
  trait NoBound {
    function J(n: nat): int
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
      0
    }
  }
}

// ----- A call that the new override puts on a cycle

module Call0 {
  trait S0 {
    function K(s: S0, n: nat): int
  }
  trait T {
    function K(s: S0, n: nat): int {
      s.K(s, n) + 1 // error (in Call1): s.K may be T.K itself
    }
    lemma UnfoldK(s: S0, n: nat)
      ensures K(s, n) == s.K(s, n) + 1
    {
    }
  }
  class C extends T {
    constructor () {}
  }
}

module Call1 refines Call0 {
  trait T ... extends S0 {}

  method WouldBeBad()
    ensures false
  {
    var c := new C();
    var s: S0 := c;
    c.UnfoldK(s, 0);
    assert s.K(s, 0) == c.K(s, 0);
  }
}

module CallControl {
  trait S0 {
    function K(s: S0, n: nat): int
  }
  trait T extends S0 {
    function K(s: S0, n: nat): int {
      s.K(s, n) + 1 // error: s.K may be T.K itself
    }
  }
}

// ----- Accepted: an inherited member with no bound takes the bound of the member it now
// overrides, and an inherited leaf may implement a member of another module that has no bound

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
  trait Z {
    function J(n: nat): int {
      n + 1
    }
  }
  class CZ extends Z {
    constructor () {}
  }
}

module Leaf1 refines Leaf0 {
  trait V ... extends Tr.Open {}
  trait W ... extends Tr.Open {}
  trait Z ... extends Tr.NoBound {}

  method M(n: nat) {
    var c := new CV();
    var o: Tr.Open := c;
    assert o.H(n) == c.H(n) == 3;
    var w: CW := new CW();
    o := w;
    assert o.H(0) == w.H(0) == 0;
    var z := new CZ();
    var j: Tr.NoBound := z;
    assert j.J(n) == z.J(n) == n + 1;
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
  trait Z extends Tr.NoBound {
    function J(n: nat): int {
      n + 1
    }
  }
  class CV extends V {
    constructor () {}
  }
  class CZ extends Z {
    constructor () {}
  }
  method M(n: nat) {
    var c := new CV();
    var o: Tr.Open := c;
    assert o.H(n) == c.H(n) == 3;
    var z := new CZ();
    var j: Tr.NoBound := z;
    assert j.J(n) == z.J(n) == n + 1;
    assert false; // error
  }
}
