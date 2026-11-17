// Gate (b) of https://github.com/erniecohen/dafny/issues/211: the control.  The same contexts within the
// rules: each member dispatches under a smaller bound, or makes a known call under the same bound.  Nothing
// here is rejected but the 'assert false' controls.
module Lib {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    lemma L(n: nat) decreases {:global} n
    method M(n: nat) decreases {:global} n
    ghost predicate P(n: nat) decreases {:global} n
  }
}
module Client {
  import opened Lib
  function Def(t: T, n: nat, x: nat := if n == 0 then 0 else t.F(n - 1)): nat decreases {:global} n { x }
  function Apply(g: nat -> nat, n: nat): nat
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  { g(n) }
  function H(t: T, n: nat): nat decreases {:global} n { 0 } by method { if n != 0 { t.M(n - 1); } return 0; }
  least lemma LL(t: T, n: nat) decreases {:global} n { if n != 0 { t.L(n - 1); } }
  function A01(t: T, n: nat): nat decreases {:global} n { if n == 0 then 0 else t.F(n - 1) + 1 }
  ghost predicate A02(t: T, n: nat) decreases {:global} n { n != 0 && forall i: nat :: i < n ==> t.P(i) }
  method A03(t: T, n: nat) decreases {:global} n { if n != 0 { t.M(n - 1); var x := t.F(n - 1); t.L(n - 1); } }
  function A04(t: T, n: nat): nat decreases {:global} n { Def(t, n) }
  function A05(t: T, n: nat): nat decreases {:global} n { if n == 0 then 0 else Apply(x => t.F(n - 1), n) }
  method A06(t: T, n: nat) decreases {:global} n { var x := H(t, n); }
  lemma A07(t: T, n: nat) decreases {:global} n { LL(t, n); }
  method A08(t: T, n: nat) decreases {:global} n {
    if n != 0 { t.M(n - 1); }
    assert false; // control
  }
  lemma A09(t: T, n: nat) decreases {:global} n {
    LL(t, n);
    assert false; // control
  }
}
