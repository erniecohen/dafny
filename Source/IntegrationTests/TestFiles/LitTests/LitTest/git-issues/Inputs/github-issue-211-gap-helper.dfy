// KNOWN GAP of milestone M1 (https://github.com/erniecohen/dafny/issues/211): this program
// verifies under --modular-termination, and proves false.  Y.F reaches the dispatched call
// t.F(n) only through the helper L.H, which has no global bound, and M1 compares a dispatched
// call only with the bound of the member that makes it directly.  The same-bound rule of M2
// checks L.H's call at Y.F's bound, n, and must reject it; this test's expected output then
// changes from "verifies" to "rejected".
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module L {
  import Tr
  function H(t: Tr.T, n: nat): nat { t.F(n) }
}

module Y {
  import Tr
  import L
  class Y extends Tr.T {
    constructor() { }
    function F(n: nat): nat { L.H(this, n) + 1 }
  }
  method Bad() ensures false {
    var y := new Y();
    assert y.F(0) == L.H(y, 0) + 1;
    var t: Tr.T := y;
    assert L.H(y, 0) == t.F(0) == y.F(0);
  }
}
