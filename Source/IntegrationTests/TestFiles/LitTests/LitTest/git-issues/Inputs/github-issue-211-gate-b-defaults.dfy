// Gate (b) of https://github.com/erniecohen/dafny/issues/211: default parameter values that dispatch,
// which the verifier checks where the call is made.  Every member Ynn must get an error on its own line.
module Lib {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}
module Client {
  import opened Lib
  class D { const t: T  const k: nat := t.F(5)  constructor(t: T) { this.t := t; } }
  datatype Box = Box(t: T)
  type S = b: Box | b.t.F(0) == 0 witness *
  datatype DD = DD(t: T, x: nat := t.F(3))
  function Def(t: T, x: nat := t.F(3)): nat { x }
  method DefM(t: T, x: nat := t.F(3)) {}
  lemma DefL(t: T, x: nat := t.F(3)) {}
  class E { constructor(t: T, x: nat := t.F(3)) {} }
  iterator It3(t: T, x: nat := t.F(3)) yields (y: nat) { yield 0; }
  function Id<X>(x: X): X { x }
  function Y06(t: T, n: nat): nat decreases {:global} n { Def(t) }
  method Y07(t: T, n: nat) decreases {:global} n { DefM(t); }
  method Y08(t: T, n: nat) decreases {:global} n { var e := new E(t); }
  method Y16(t: T, n: nat) decreases {:global} n { var it := new It3(t); }
  lemma Y17(t: T, n: nat) decreases {:global} n { DefL(t); }
}
