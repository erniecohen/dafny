// Rejected by the resolver under --refinement-extends --modular-termination.  The parent that the
// refinement adds makes the inherited T.K an implementation of Tr.A.K, a member of another module
// that has no 'decreases {:global}' bound.  Such an implementation must be a leaf or have a bound
// of its own, and T.K is neither: it makes a dispatched call.  The check is made in the refining
// module, at the inherited member.  The one-module control gets the same error.

module Tr {
  trait A {
    function K(a: A, n: nat): int
  }
}

module NotLeaf0 {
  import Tr
  trait T {
    function K(a: Tr.A, n: nat): int { // error (in NotLeaf1): not a leaf
      a.K(a, n) + 1
    }
    lemma Unfold(a: Tr.A, n: nat)
      ensures K(a, n) == a.K(a, n) + 1
    {
    }
  }
  class C extends T {
    constructor () {}
  }
}

module NotLeaf1 refines NotLeaf0 {
  trait T ... extends Tr.A {}

  method WouldBeBad()
    ensures false
  {
    var c := new C();
    var a: Tr.A := c;
    c.Unfold(a, 0);
    assert a.K(a, 0) == c.K(a, 0);
  }
}

module NotLeafControl {
  import Tr
  trait T extends Tr.A {
    function K(a: Tr.A, n: nat): int { // error: not a leaf
      a.K(a, n) + 1
    }
  }
}
