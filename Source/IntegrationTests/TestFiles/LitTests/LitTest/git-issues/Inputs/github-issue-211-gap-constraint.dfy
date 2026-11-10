// KNOWN GAP of --modular-termination (https://github.com/erniecohen/dafny/issues/211): the constraint
// of a subset type (or newtype) belongs to no method or function, and a type test evaluates it at
// run time with no call to check.  Here S.Good's constraint makes the dispatched call t.F(0), and
// Y.F, whose bound is n, reaches it through "me is S.Good": nothing compares the two, and the
// program verifies and proves false.  The same holds for a witness, which compiled code may evaluate.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module S {
  import Tr
  type Good = t: Tr.T | t.F(0) == 0 witness *
}

module Y {
  import Tr
  import S
  class Y extends Tr.T {
    constructor() { }
    function F(n: nat): nat { var me: Tr.T := this; if me is S.Good then 1 else 0 }
  }
  method Bad() ensures false {
    var y := new Y();
    var t: Tr.T := y;
    assert y.F(0) == (if (y as Tr.T) is S.Good then 1 else 0);
    assert t.F(0) == y.F(0);
  }
}
