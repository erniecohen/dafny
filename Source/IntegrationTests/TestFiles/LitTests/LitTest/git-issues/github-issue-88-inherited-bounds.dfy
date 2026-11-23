// RUN: %exits-with 4 %verify --refinement-extends "%s" > "%t"
// RUN: %exits-with 4 %verify --refinement-extends --type-system-refresh:false --general-newtypes:false "%s" >> "%t"
// RUN: %exits-with 4 %verify --refinement-extends --modular-termination "%s" >> "%t"
// RUN: %exits-with 4 %verify --refinement-extends --modular-termination --type-system-refresh:false --general-newtypes:false "%s" >> "%t"
// RUN: %verify --refinement-extends --legacy-no-modular-termination --modular-termination "%s" >> "%t"
// RUN: %verify --refinement-extends --legacy-no-modular-termination --modular-termination --type-system-refresh:false --general-newtypes:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The new parent gives an inherited body a global bound. Its old calls must be
// checked against that bound even if no local call-graph cycle has changed.
// Without that check, the later implementation closes a cross-module cycle
// and WouldBeBad proves false. Test both the default rules and the explicit option.
module Slots {
  trait A {
    function K(a: A, b: B, n: nat): int
  }
  trait B {
    function G(a: A, b: B, n: nat): int
      decreases {:global} n
  }
}

module Base {
  import Slots
  trait T extends object {
    function G(a: Slots.A, b: Slots.B, n: nat): int {
      a.K(a, b, n) + 1
    }
    lemma Unfold(a: Slots.A, b: Slots.B, n: nat)
      ensures G(a, b, n) == a.K(a, b, n) + 1
    {}
  }
  class C extends T {
    constructor () {}
  }
}

module Refined refines Base {
  trait T ... extends Slots.B {}
}

module Later {
  import Slots
  import Refined
  class K extends Slots.A {
    constructor () {}
    function K(a: Slots.A, b: Slots.B, n: nat): int
      decreases {:global} n + 1
    {
      b.G(a, b, n)
    }
    lemma Unfold(a: Slots.A, b: Slots.B, n: nat)
      decreases {:global} n + 1
      ensures K(a, b, n) == b.G(a, b, n)
    {}
  }
  method WouldBeBad() ensures false {
    var c := new Refined.C();
    var k := new K();
    c.Unfold(k, c, 0);
    k.Unfold(k, c, 0);
  }
}
