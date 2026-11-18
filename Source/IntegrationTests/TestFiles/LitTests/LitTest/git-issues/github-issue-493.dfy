// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

module Tr {
  trait A {
    function K(a: A, b: B, n: nat): int   // no bound
  }
  trait B {
    function G(a: A, b: B, n: nat): int
      decreases {:global} n
  }
}

module Top0 {
  import Tr
  type T {
    function G(a: Tr.A, b: Tr.B, n: nat): int {
      a.K(a, b, n) + 1
    }
    lemma UnfoldG(a: Tr.A, b: Tr.B, n: nat)
      ensures G(a, b, n) == a.K(a, b, n) + 1
    {}
  }
}
module Top1 refines Top0 {
  class T extends Tr.B { constructor () {} }
}

module Later {
  import Tr
  import Top1
  class KI extends Tr.A {
    constructor () {}
    function K(a: Tr.A, b: Tr.B, n: nat): int
      decreases {:global} n + 1
    {
      b.G(a, b, n)
    }
    lemma UnfoldK(a: Tr.A, b: Tr.B, n: nat)
      decreases {:global} n + 1
      ensures K(a, b, n) == b.G(a, b, n)
    {}
  }
  method WouldBeBad()
    ensures false
  {
    var c := new Top1.T();
    var k := new KI();
    var a: Tr.A := k;
    var b: Tr.B := c;
    c.UnfoldG(a, b, 0);
    k.UnfoldK(a, b, 0);
    assert b.G(a, b, 0) == c.G(a, b, 0);
    assert a.K(a, b, 0) == k.K(a, b, 0);
  }
}
