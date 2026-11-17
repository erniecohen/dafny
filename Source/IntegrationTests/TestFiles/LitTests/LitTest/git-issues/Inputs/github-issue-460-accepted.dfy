// https://github.com/erniecohen/dafny/issues/460: induction that stays within the bound is still accepted.  The only
// errors are the 'assert false' controls.
module Lib {
  trait {:termination false} T {
    lemma L(n: nat)
      decreases {:global} n
    ghost predicate Inv(n: nat)
  }
}

module Client {
  import opened Lib

  ghost function Sum(m: nat): nat { if m == 0 then 0 else m + Sum(m - 1) }

  // The induction hypothesis at the same bound and a smaller m.
  lemma {:induction t, n, m} SameBound(t: T, n: nat, m: nat)
    ensures Sum(m) * 2 == m * (m + 1)
    decreases {:global} n
    decreases m
  {
    if n > 0 {
      t.L(n - 1);
    }
    assert false;  // control
  }

  // The induction hypothesis at a smaller bound.
  ghost predicate Small(t: T, n: nat, m: nat) { Sum(m) * 2 == m * (m + 1) }
  lemma {:induction t, n, m} SmallerBound(t: T, n: nat, m: nat)
    ensures Small(t, n, m)
    decreases {:global} n
    decreases n, m
  {
    if n > 0 {
      t.L(n - 1);
      assert Small(t, n - 1, m + 1);
    } else if m > 0 {
      assert Small(t, n, m - 1);
    }
    assert false;  // control
  }

  least predicate Even(x: nat) { x == 0 || (x >= 2 && Even(x - 2)) }

  // A least lemma under a bound: its recursive call, and the induction hypothesis of its prefix lemma, at the
  // same bound.
  least lemma EvenIsEven(t: T, n: nat, x: nat)
    requires Even(x)
    ensures x % 2 == 0
    decreases {:global} n
  {
    if n > 0 {
      t.L(n - 1);
    }
    if x != 0 {
      EvenIsEven(t, n, x - 2);
    }
    assert false;  // control
  }

  least lemma EvenIsEvenAuto(t: T, n: nat, x: nat)
    requires Even(x)
    ensures x % 2 == 0
    decreases {:global} n
  {
    if n > 0 {
      t.L(n - 1);
    }
  }

  // The generated limit case, used at the same bound: the precondition holds at a limit ordinal only.
  least predicate Q(i: nat) { i == 0 || Q(i - 1) }
  ghost predicate AllBelow(t: T, n: nat) { true }
  least lemma {:induction false} Limit(t: T, n: nat)
    requires forall i: nat :: Q(i)
    ensures AllBelow(t, n)
    decreases {:global} n
  {
    if n > 0 {
      t.L(n - 1);
    }
  }

  lemma UseThem(t: T, n: nat)
    decreases {:global} n
  {
    SameBound(t, n, 5);
    SmallerBound(t, n, 5);
    assert Even(0);
    assert Even(2);
    EvenIsEven(t, n, 2);
    EvenIsEvenAuto(t, n, 2);
    assert false;  // control
  }
}
