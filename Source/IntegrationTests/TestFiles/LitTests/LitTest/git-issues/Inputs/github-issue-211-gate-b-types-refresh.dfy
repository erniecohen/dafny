// Gate (b) of https://github.com/erniecohen/dafny/issues/211: type tests and conversions to a subset type
// whose constraint dispatches, which only --type-system-refresh accepts.  Every member Ynn must get an error
// on its own line.
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
  function Id<X>(x: X): X { x }
  predicate Y02(b: Box, n: nat) decreases {:global} n { b is S }
  function Y12(b: Box, n: nat): Box decreases {:global} n { Id<S>(b as S) }
}
