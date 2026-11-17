// https://github.com/erniecohen/dafny/issues/460: the same through the case that Dafny generates for a prefix lemma at a
// limit ordinal, which calls the prefix lemma at every smaller ordinal for all values of its parameters,
// so at every bound.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      ensures false
      decreases {:global} n
  }
}

module Client {
  import opened Lib

  least predicate Q(i: nat) { i == 0 || Q(i - 1) }
  lemma AllQ(i: nat) ensures Q(i) { if i != 0 { AllQ(i - 1); } }
  ghost function F(n: nat): nat { if n == 0 then 0 else 7 }
  ghost predicate Bad(t: T) { false }

  // With n == 0, the precondition first holds at a limit ordinal, where the body is not used: the generated
  // limit case is a call of the prefix lemma at every smaller ordinal and every t and n.
  least lemma {:induction false} K(t: T, n: nat)
    requires n == 0 ==> Q(F(1)) && forall i: nat :: Q(i)
    requires n != 0 ==> Q(F(n))
    ensures Bad(t)
    decreases {:global} n
  {
    if n == 0 {
      forall i: nat ensures Q#[_k - 1](i) { assert Q#[_k](i + 1); }
      assert Q#[_k - 1](F(1));
      K(t, 0);
    } else {
      t.L(n - 1);
    }
  }

  class C extends T {
    constructor() {}
    lemma L(n: nat)
      ensures false
    {
      forall i: nat ensures Q(i) { AllQ(i); }
      K(this, 0);
    }
  }

  method Main() {
    var c := new C();
    c.L(1);
    assert false;
  }
}
