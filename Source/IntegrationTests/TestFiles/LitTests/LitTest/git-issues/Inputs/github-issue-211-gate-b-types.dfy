// Gate (b) of https://github.com/erniecohen/dafny/issues/211: constant initializers, subset-type
// constraints and default parameter values that dispatch, used under a bound.  Every member Ynn must get an
// error on its own line; the resolver reports these.
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
  function Y01(d: D, n: nat): nat reads d decreases {:global} n { d.k }
  function Y05(t: T, n: nat): nat decreases {:global} n { DD(t).x }
  function Y09(s: S, n: nat): nat decreases {:global} n { 0 }
  method Y10(n: nat) decreases {:global} n { var s: S; }
  method Y11(n: nat) decreases {:global} n { var q: seq<S> := []; }
  function Y12(b: Box, n: nat): Box decreases {:global} n { Id<S>(b) }
  function Y15(d: D, n: nat): () -> nat reads d decreases {:global} n { () reads d => d.k }
  method Y18(b: Box, n: nat) decreases {:global} n { var s: S := b; }
  method Y19(n: nat) returns (s: S) decreases {:global} n { s :| true; }
  function Y20(b: Box, n: nat): nat decreases {:global} n { match b case Box(t) => var s: S := b; 0 }
  function Y21(dd: DD, n: nat): DD decreases {:global} n { dd.(x := 1) }
}
