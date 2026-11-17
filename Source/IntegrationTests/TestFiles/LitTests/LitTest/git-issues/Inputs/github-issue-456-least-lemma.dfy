// https://github.com/erniecohen/dafny/issues/456: the body of a least lemma is verified as its prefix lemma, which had no bound, so the
// dispatched call in K was not checked and Main proved false.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      ensures false
      decreases {:global} n
  }
}
module Client {
  import opened Lib
  least predicate Q(n: nat) { true }
  least lemma K(t: T, n: nat)
    requires Q(n)
    ensures false
    decreases {:global} n
  {
    t.L(n);  // dispatched at the caller's own bound: must be rejected
  }
  class C extends T {
    constructor() {}
    lemma L(n: nat)
      ensures false
    {
      K(this, n);
    }
  }
  method Main() {
    var c := new C();
    c.L(0);
    assert false;
  }
}
