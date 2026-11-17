// https://github.com/erniecohen/dafny/issues/456: a co-recursive call was not checked against the bound of its caller.  F calls itself with a
// larger bound, so H(n, t) == t.G(n) and the implementation G(n) == H(n, this) + 1 was accepted.
module Lib {
  trait {:termination false} T {
    function G(n: nat): nat
      decreases {:global} n
  }
}
module Mid {
  import opened Lib
  codatatype Stream = Cons(head: nat, tail: Stream)
  function F(n: nat, t: T): Stream
    decreases {:global} n
  {
    Cons(if n > 0 then t.G(n - 1) else 0, F(n + 1, t))  // the co-recursive call raises the bound
  }
  function H(n: nat, t: T): nat
    decreases {:global} n
  {
    F(n, t).tail.head
  }
  lemma HIsG(n: nat, t: T)
    ensures H(n, t) == t.G(n)
  {
    assert F(n, t).tail == F(n + 1, t);
  }
}
module Client {
  import opened Lib
  import opened Mid
  class C extends T {
    constructor() {}
    function G(n: nat): nat
    {
      H(n, this) + 1
    }
  }
  method Main() {
    var c := new C();
    HIsG(0, c);
    assert false;
    print c.G(0), "\n";
  }
}
