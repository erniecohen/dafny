// https://github.com/erniecohen/dafny/issues/460: the same through the induction hypothesis of the prefix lemma of a least
// lemma, which is by induction on the ordinal _k and covered every bound.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      requires n > 0
      ensures false
      decreases {:global} n
  }
}

module Client {
  import opened Lib

  least predicate P(x: int) { x == 0 || P(x - 1) }
  ghost predicate Bad(t: T, n: nat, x: int) { false }

  least lemma {:induction} K(t: T, n: nat, x: int)
    requires n > 0 && P(x) && (x == 0 ==> n > 1)
    ensures Bad(t, n, x)
    decreases {:global} n
  {
    if x == 0 {
      t.L(n - 1);
    } else {
      assert Bad(t, n + 1, x - 1);  // the induction hypothesis of the prefix lemma, at a larger bound
    }
  }

  class C extends T {
    constructor() {}
    lemma L(n: nat)
      requires n > 0
      ensures false
    {
      assert P(0);
      assert P(1);
      K(this, n, 1);
    }
  }

  method Main() {
    var c := new C();
    c.L(1);
    assert false;
  }
}
