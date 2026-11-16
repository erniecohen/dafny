// https://github.com/erniecohen/dafny/issues/456: the same constructs within the rules.  Nothing here is rejected but the
// 'assert false' controls.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      decreases {:global} n
    function G(n: nat): nat
      decreases {:global} n
  }
}
module Mid {
  import opened Lib
  least predicate {:termination_closed} Even(x: nat) { x == 0 || (x > 1 && Even(x - 2)) }
  greatest predicate {:termination_closed} Always(x: nat) { Always(x + 1) }
  // A least lemma under a bound: it dispatches below the bound and calls itself under the same bound.
  least lemma K(t: T, n: nat, x: nat)
    requires Even(x)
    ensures x % 2 == 0
    decreases {:global} n
  {
    if n > 0 { t.L(n - 1); }
    if x != 0 { K(t, n, x - 2); }
  }
  greatest lemma A(t: T, n: nat, x: nat)
    ensures Always(x)
    decreases {:global} n
  {
    if n > 0 { t.L(n - 1); }
    A(t, n, x + 1);
  }
  least lemma {:termination_closed} Pure(x: nat) requires Even(x) ensures x % 2 == 0 { }
  codatatype {:termination_closed} Stream = Cons(head: nat, tail: Stream)
  // A co-recursive call under the same bound.
  function F(n: nat, t: T): Stream
    decreases {:global} n
  {
    Cons(if n > 0 then t.G(n - 1) else 0, F(n, t))
  }
  function {:termination_closed} Nats(n: nat): Stream { Cons(n, Nats(n + 1)) }
}
module Client {
  import opened Lib
  import opened Mid
  ghost predicate A01(n: nat) decreases {:global} 0 { Even(n) && Always(n) }
  ghost predicate A02(n: nat, k: ORDINAL) decreases {:global} 0 { Even#[k](n) && Always#[k](n) }
  lemma A03(n: nat) requires Even(n) ensures n % 2 == 0 decreases {:global} 0 { Pure(n); }
  lemma A04(n: nat, k: ORDINAL) requires Even#[k](n) ensures n % 2 == 0 decreases {:global} 0 { Pure#[k](n); }
  lemma A05(t: T, n: nat, x: nat) requires Even(x) ensures x % 2 == 0 && Always(x) decreases {:global} n { K(t, n, x); A(t, n, x); }
  lemma A06(t: T, n: nat, x: nat, k: ORDINAL) requires Even#[k](x) ensures x % 2 == 0 decreases {:global} n { K#[k](t, n, x); }
  function A07(t: T, n: nat): nat decreases {:global} n { F(n, t).tail.head + Nats(n).tail.head }
  class C extends T {
    constructor() {}
    lemma L(n: nat)
    {
      assert Even(0);
      K(this, n, 0);
      A(this, n, 0);
    }
    function G(n: nat): nat
    {
      F(n, this).tail.head + 1
    }
  }
  lemma A08(t: T, n: nat, x: nat)
    requires Even(x)
    decreases {:global} n
  {
    K(t, n, x);
    A(t, n, x);
    assert false; // control
  }
  method Main() {
    var c := new C();
    c.L(0);
    assert c.G(0) == 1;
    assert false; // control
  }
}
