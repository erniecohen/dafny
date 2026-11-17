// Gate (b) of https://github.com/erniecohen/dafny/issues/211: the function value of a dispatched member.
// Every member Enn applies the value, or its requires, under a bound: each must get an error on its own line.
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
  function E30(t: T, n: nat): nat decreases {:global} n { var g := t.F; g(n) }
  function E31(t: T, n: nat): nat decreases {:global} n { (t.F)(n) }
  ghost function E32(t: T, n: nat): bool decreases {:global} n { t.F.requires(n) }
}
