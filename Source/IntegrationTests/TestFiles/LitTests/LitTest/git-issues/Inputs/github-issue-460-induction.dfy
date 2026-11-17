// https://github.com/erniecohen/dafny/issues/460: the automatic induction hypothesis of a lemma covered every instance
// that is smaller in the lemma's own decreases order, whatever its 'decreases {:global}' bound.  K used
// it at the bound n + 1, so the implementation L could call K, and Main proved false.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat, m: nat)
      requires n > 0 || m > 0
      ensures false
      decreases {:global} n
  }
}
module Client {
  import opened Lib
  ghost predicate Bad(t: T, n: nat, m: nat) { false }
  lemma {:induction t, n, m} K(t: T, n: nat, m: nat)
    requires n > 0 || m > 0
    ensures Bad(t, n, m)
    decreases {:global} n
    decreases m
  {
    if m > 0 {
      assert Bad(t, n + 1, m - 1);  // the automatic induction hypothesis, at a larger bound
    } else {
      t.L(n - 1, 1);
    }
  }
  class C extends T {
    constructor() {}
    lemma L(n: nat, m: nat)
      requires n > 0 || m > 0
      ensures false
    {
      K(this, n, m);
    }
  }
  method Main() {
    var c := new C();
    c.L(1, 0);
    assert false;
  }
}
