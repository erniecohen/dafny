// Gate (b) of https://github.com/erniecohen/dafny/issues/211: statement contexts and specification clauses.
// Every member Snn has the bound n and dispatches T.L, T.M or T.F at the same n: each must get an error on
// its own line.
module Lib {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    lemma L(n: nat) decreases {:global} n
    method M(n: nat) decreases {:global} n
    twostate lemma L2(n: nat) decreases {:global} n
  }
}
module Client {
  import opened Lib
  lemma S01(t: T, n: nat) decreases {:global} n { t.L(n); }
  lemma S02(t: T, n: nat) decreases {:global} n { forall m: nat | m == n { t.L(m); } }
  lemma S03(t: T, n: nat) decreases {:global} n { assert true by { t.L(n); } }
  lemma S04(t: T, n: nat) decreases {:global} n { calc { 0; { t.L(n); } 0; } }
  lemma S05(t: T, n: nat) decreases {:global} n { var x := (t.L(n); 0); }
  lemma S06(t: T, n: nat) decreases {:global} n { if n > 0 { t.L(n); } else { t.L(n); } }
  lemma S07(t: T, n: nat) decreases {:global} n { var i := 0; while i < 1 { t.L(n); i := i + 1; } }
  lemma S08(t: T, n: nat) decreases {:global} n { for i := 0 to 1 { t.L(n); } }
  lemma S09(t: T, n: nat, d: (nat, nat)) decreases {:global} n { match d { case (a, b) => t.L(n); } }
  lemma S10(t: T, n: nat) decreases {:global} n { forall m: nat ensures true { t.L(n); } }
  lemma S11(t: T, n: nat) decreases {:global} n { opaque { t.L(n); } }
  lemma S12(t: T, n: nat) requires t.F(n) == t.F(n) decreases {:global} n { }
  lemma S13(t: T, n: nat) ensures t.F(n) == t.F(n) decreases {:global} n { }
  method S14(t: T, n: nat) decreases {:global} n { t.M(n); }
  method S15(t: T, n: nat) modifies if t.F(n) == 0 then {} else {} decreases {:global} n { }
  method S16(t: T, n: nat) decreases {:global} n { var i := 0; while i < 1 invariant t.F(n) == t.F(n) { i := i + 1; } }
  method S17(t: T, n: nat) decreases {:global} n { var i := 0; while i < 1 decreases 1 - i, t.F(n) { i := i + 1; } }
  method S18(t: T, n: nat) decreases {:global} n { print t.F(n); }
  method S19(t: T, n: nat) decreases {:global} n { expect t.F(n) == t.F(n); }
  method S20(t: T, n: nat) decreases {:global} n { var a := new nat[1](i => t.F(n)); }
  method S21(t: T, n: nat) decreases {:global} n { var x: nat :| x == t.F(n); }
  method S22(t: T, n: nat) decreases {:global} n { var f := () => t.F(n); }
  method S23(t: T, n: nat) returns (r: nat) decreases {:global} n { return t.F(n); }
  method S24(t: T, n: nat) decreases {:global} n { ghost var x := 0; x := (t.L(n); 0); }
  twostate lemma S25(t: T, n: nat) decreases {:global} n { t.L2(n); }
  twostate lemma S26(t: T, n: nat) decreases {:global} n { t.L(n); }
  least lemma S27(t: T, n: nat) decreases {:global} n { t.L(n); }
  greatest lemma S28(t: T, n: nat) decreases {:global} n { t.L(n); }
  // (a least predicate cannot have a decreases clause)
  method S30(t: T, n: nat) decreases {:global} n { var a := new nat[1]; a[0] := t.F(n); }
  method S31(t: T, n: nat) decreases {:global} n { assume {:axiom} t.F(n) == 0; }
  method S32(t: T, n: nat) decreases {:global} n { label A: assert old@A(t.F(n)) == t.F(n) || true; }
  method S33(t: T, n: nat) decreases {:global} n { modify {} ; var u := t.F(n); }
  method S34(t: T, n: nat) decreases {:global} n { var r := S23(t, t.F(n)); }
}
