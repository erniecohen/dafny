// Gate (b) of https://github.com/erniecohen/dafny/issues/211: lambdas that dispatch, which the verifier
// checks under the bound of the code that creates them.  Every member Znn must get an error on its own line.
module Lib {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}
module Client {
  import opened Lib
  datatype Fn = Fn(f: nat -> nat)
  class H { const g: nat -> nat  const p: nat --> nat  constructor(g: nat -> nat) { this.g := g; this.p := g; } }
  function Big(t: T, n: nat): nat { t.F(n + 1) }
  function Apply(g: nat -> nat, n: nat): nat
    decreases {:global} n, 1
    decreases {:callback g} n, 0
  { g(n) }
  function Z03(t: T, n: nat): nat decreases {:global} n { Apply(x => t.F(n), n) }
  function Z07(t: T, n: nat): nat decreases {:global} n { Apply((x: nat) => Big(t, x), n) }
  function Z15(t: T, n: nat): nat decreases {:global} n { (x => t.F(n))(0) }
  function Z20(t: T, n: nat): seq<nat> decreases {:global} n { seq(3, i => t.F(n)) }
}
