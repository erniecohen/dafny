// https://github.com/erniecohen/dafny/issues/456: a call of a least predicate was not checked against the bound of its caller.  LP has no
// bound and dispatches, so X may not call it; before, P(1) <==> !P(1) was provable.
module Lib {
  trait {:termination false} T {
    ghost predicate P(n: nat)
      decreases {:global} n
  }
}
module Client {
  import opened Lib
  least predicate LP(t: T, n: nat) { t.P(n) }
  ghost predicate X(n: nat, t: T)
    decreases {:global} n
  {
    LP(t, n + 1)
  }
  class C extends T {
    constructor() {}
    ghost predicate P(n: nat)
    {
      n > 0 && !X(n - 1, this)
    }
  }
  method Main() {
    var c := new C();
    assert c.P(1) <==> !X(0, c);
    assert X(0, c) <==> LP(c, 1);
    assert LP(c, 1) <==> (c as T).P(1);
    assert false;
  }
}
