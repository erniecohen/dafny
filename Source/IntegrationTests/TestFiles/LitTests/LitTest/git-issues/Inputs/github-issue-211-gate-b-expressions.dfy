// Gate (b) of https://github.com/erniecohen/dafny/issues/211: expression contexts.
// Every member Enn has the bound n and dispatches T.F, T.L, T.M or T.P at the same n, where the
// smaller-bound rule needs a smaller one: each must get an error on its own line.
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
  function E01(t: T, n: nat): nat decreases {:global} n { t.F(n) }
  function E02(t: T, n: nat): nat requires t.F(n) == 0 decreases {:global} n { 0 }
  function E03(t: T, n: nat): nat ensures t.F(n) == t.F(n) decreases {:global} n { 0 }
  function E04(t: T, n: nat): nat reads if t.F(n) == 0 then {} else {} decreases {:global} n { 0 }
  function E05(t: T, n: nat): nat decreases t.F(n) decreases {:global} n { 0 }
  function E06(t: T, n: nat, v: nat := t.F(n)): nat decreases {:global} n { 0 }
  function E07(t: T, n: nat): nat decreases {:global} n { var f := (m: nat) => t.F(n); 0 }
  function E08(t: T, n: nat): nat decreases {:global} n { var f := (m: nat) requires t.F(n) == 0 => 0; 0 }
  function E09(t: T, n: nat): nat decreases {:global} n { var f := (m: nat) reads if t.F(n) == 0 then {} else {} => 0; 0 }
  ghost function E10(t: T, n: nat): nat decreases {:global} n { if forall m: nat :: t.F(n) == m then 0 else 1 }
  ghost function E11(t: T, n: nat): nat decreases {:global} n { if exists m: nat | t.F(n) == m :: true then 0 else 1 }
  ghost function E12(t: T, n: nat): set<nat> decreases {:global} n { set m: nat | m < 3 && t.F(n) == m }
  ghost function E13(t: T, n: nat): map<nat, nat> decreases {:global} n { map m: nat | m < 3 :: t.F(n) }
  ghost function E14(t: T, n: nat): nat decreases {:global} n { var m: nat :| m == m && t.F(n) == t.F(n); m }
  function E15(t: T, n: nat, d: (nat, nat)): nat decreases {:global} n { match d case (a, b) => t.F(n) }
  function E16(t: T, n: nat): nat decreases {:global} n { var x := t.F(n); x }
  function E17(t: T, n: nat): nat decreases {:global} n { assert t.F(n) == t.F(n); 0 }
  function E18(t: T, n: nat): nat decreases {:global} n { calc { 0; { assert t.F(n) == t.F(n); } 0; } 0 }
  function E19(t: T, n: nat): nat decreases {:global} n { t.L(n); 0 }
  function E20(t: T, n: nat): seq<nat> decreases {:global} n { seq(2, i => t.F(n)) }
  function E21(t: T, n: nat): (nat, nat) decreases {:global} n { (0, 0).(0 := t.F(n)) }
  function E22(t: T, n: nat): nat decreases {:global} n { 0 } by method { var x := t.F(n); return 0; }
  function E23(t: T, n: nat): nat decreases {:global} n { 0 } by method { t.M(n); return 0; }
  ghost function E24(t: T, n: nat): bool decreases {:global} n { t.P(n) }
  ghost predicate E25(t: T, n: nat) decreases {:global} n { forall m: nat {:trigger t.P(m)} :: m == n ==> t.P(m) }
  twostate function E26(t: T, n: nat): nat decreases {:global} n { t.F(n) }
  twostate function E27(t: T, n: nat): nat decreases {:global} n { old(t.F(n)) }
  function E28(t: T, n: nat): nat decreases {:global} n { if t.F(n) == 0 then 0 else 1 }
  function E29(t: T, n: nat): nat decreases {:global} n { [t.F(n)][0] }
}
