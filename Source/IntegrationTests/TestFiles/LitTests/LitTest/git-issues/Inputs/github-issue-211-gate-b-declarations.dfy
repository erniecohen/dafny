// Gate (b) of https://github.com/erniecohen/dafny/issues/211: least and greatest predicates and lemmas,
// constructors and iterators that dispatch, used under a bound.  Every member Xnn must get an error on its
// own line.
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
  least predicate LP(t: T, n: nat) { t.P(n) }
  greatest predicate GP(t: T, n: nat) { t.P(n) }
  least lemma LL(t: T, n: nat) requires LP(t, n) { t.L(n); }
  class D { constructor(t: T, n: nat) { t.M(n); } }
  class D2 { var x: nat  constructor(t: T, n: nat) { x := t.F(n); } }
  iterator It(t: T, n: nat) yields (y: nat) { t.M(n); yield 0; }
  iterator It2(t: T, n: nat) yields (y: nat) requires t.F(n) == t.F(n) { yield 0; }
  ghost predicate X01(t: T, n: nat) decreases {:global} n { LP(t, n) }
  ghost predicate X02(t: T, n: nat) decreases {:global} n { LP#[1](t, n) }
  ghost predicate X03(t: T, n: nat) decreases {:global} n { GP(t, n) }
  ghost predicate X04(t: T, n: nat) decreases {:global} n { GP#[1](t, n) }
  lemma X05(t: T, n: nat) requires LP(t, n) decreases {:global} n { LL(t, n); }
  lemma X06(t: T, n: nat) requires LP#[1](t, n) decreases {:global} n { LL#[1](t, n); }
  method X07(t: T, n: nat) decreases {:global} n { var d := new D(t, n); }
  method X08(t: T, n: nat) decreases {:global} n { var d := new D2(t, n); }
  method X09(t: T, n: nat) decreases {:global} n { var it := new It(t, n); }
  method X10(t: T, n: nat) decreases {:global} n { var it := new It(t, n); var more := it.MoveNext(); }
  method X11(t: T, n: nat) decreases {:global} n { var it := new It2(t, n); }
  method X12(it: It) requires it.Valid() modifies it, it._modifies, it._new decreases {:global} 0 { var more := it.MoveNext(); }
  ghost predicate X13(it: It) reads * decreases {:global} 0 { it.Valid() }
}
