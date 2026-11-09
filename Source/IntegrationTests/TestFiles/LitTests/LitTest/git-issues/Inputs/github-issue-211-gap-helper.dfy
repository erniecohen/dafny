// Forwarding helpers that hid a cycle through a dispatched call from milestone M1
// (https://github.com/erniecohen/dafny/issues/211).  M1 compares a dispatched call only with the
// bound of the member that makes it directly, so under it each of the three programs below
// verified and proved false.  In each, Y.F reaches the dispatched call t.F(n) only through a
// helper H: unbounded and in another module (L), bounded but larger (L2), or in the implementing
// module with an ordinary decreases clause (Y3).  Under the same-bound rule of milestone M2, H
// runs under Y.F's bound, n, so its own bound must not exceed n: each call of H is rejected.
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

// Variant: the helper has a global bound, larger than the caller's.  M1 never compared a
// helper's own bound with its caller's, so it laundered the bound just the same.
module Tr2 {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module L2 {
  import Tr2
  function H(t: Tr2.T, n: nat): nat decreases {:global} n + 1 { t.F(n) }
}

module Y2 {
  import Tr2
  import L2
  class Y extends Tr2.T {
    constructor() { }
    function F(n: nat): nat { L2.H(this, n) + 1 }
  }
  method Bad() ensures false {
    var y := new Y();
    assert y.F(0) == L2.H(y, 0) + 1;
    var t: Tr2.T := y;
    assert L2.H(y, 0) == t.F(0) == y.F(0);
  }
}

// Variant: the helper is in the implementing module itself, and the ordinary decreases
// clauses (1 for the trait member and its override, 0 for the helper) let the module's own
// check pass: Y.F to H goes down from 1 to 0, and the dispatched call t.F(n) leaves the
// module, so only the global bound is compared there, against H's, which is TOP.  Under M2,
// the call of H is compared with Y.F's bound.
module Tr3 {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n decreases 1
  }
}

module Y3 {
  import Tr3
  function H(t: Tr3.T, n: nat): nat decreases 0 { t.F(n) }
  class Y extends Tr3.T {
    constructor() { }
    function F(n: nat): nat decreases 1 { H(this, n) + 1 }
  }
  method Bad() ensures false {
    var y := new Y();
    assert y.F(0) == H(y, 0) + 1;
    var t: Tr3.T := y;
    assert H(y, 0) == t.F(0) == y.F(0);
  }
}
