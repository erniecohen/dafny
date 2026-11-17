// https://github.com/erniecohen/dafny/issues/456: least and greatest members and co-recursive functions of an imported module.
// Every member Bnn must be rejected.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      ensures false
      decreases {:global} n
    ghost predicate P(n: nat)
      decreases {:global} n
    function G(n: nat): nat
      decreases {:global} n
  }
}
module Mid {
  import opened Lib
  least predicate Q(n: nat) { true }
  least predicate LP(t: T, n: nat) { t.P(n) }
  least predicate {:termination_closed} Pure(n: nat) { n == 0 || (n > 1 && Pure(n - 2)) }
  least predicate NotPromised(n: nat) { n == 0 || (n > 1 && NotPromised(n - 2)) }
  least lemma K(t: T, n: nat) requires Q(n) ensures false decreases {:global} n { if n > 0 { t.L(n - 1); } else { assume {:axiom} false; } }
  least lemma {:termination_closed} PureLemma(n: nat) requires Pure(n) ensures n % 2 == 0 { }
  codatatype {:termination_closed} Stream = Cons(head: nat, tail: Stream)
  function F(n: nat, t: T): Stream decreases {:global} n { Cons(if n > 0 then t.G(n - 1) else 0, F(n, t)) }
  function {:termination_closed} Nats(n: nat): Stream { Cons(n, Nats(n + 1)) }
}
module Client {
  import opened Lib
  import opened Mid
  ghost predicate B04(t: T, n: nat) decreases {:global} n { LP(t, n) }
  ghost predicate B05(t: T, n: nat, k: ORDINAL) decreases {:global} n { LP#[k](t, n) }
  ghost predicate B06(n: nat) decreases {:global} n { NotPromised(n) }
  lemma B07(t: T, n: nat) requires Q(n + 1) ensures false decreases {:global} n { K(t, n + 1); }
  lemma B08(t: T, n: nat, k: ORDINAL) requires Q#[k](n + 1) ensures false decreases {:global} n { K#[k](t, n + 1); }
  function B09(t: T, n: nat): nat decreases {:global} n { F(n + 1, t).head }
}
