// Gate (b) of https://github.com/erniecohen/dafny/issues/211: function values and lambdas under a bound.
// Every member Znn must get an error on its own line; the resolver reports these.
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
  function Z01(h: H, n: nat): nat decreases {:global} n { h.g(n) }
  function Z02(h: H, n: nat): nat decreases {:global} n { Apply(h.g, n) }
  function Z04(t: T, n: nat): nat decreases {:global} n { var g := (x: nat) => t.F(n); Apply(g, n) }
  function Z05(d: Fn, n: nat): nat decreases {:global} n { d.f(n) }
  function Z06(d: Fn, n: nat): nat decreases {:global} n { Apply(d.f, n) }
  function Z08(t: T, n: nat): nat decreases {:global} n { Apply(if n == 0 then (x => t.F(n)) else (x => 0), n) }
  ghost predicate Z09(h: H, n: nat) decreases {:global} n { h.p.requires(n) }
  function Z11(q: seq<nat -> nat>, n: nat): nat requires |q| > 0 decreases {:global} n { q[0](n) }
  function Z12(m: map<nat, nat -> nat>, n: nat): nat requires 0 in m decreases {:global} n { m[0](n) }
  function Z13(g: nat -> nat -> nat, n: nat): nat decreases {:global} n decreases {:callback g} 0 { g(n)(n) }
  function Z14(t: T, n: nat): nat decreases {:global} n { Apply((x => y => t.F(n))(0), n) }
  function Z16(t: T, n: nat): nat decreases {:global} n { Apply(t.F, n) }
  ghost function Z17(h: H, n: nat): set<object?> decreases {:global} n { h.p.reads(n) }
  method Z18(h: H, n: nat) decreases {:global} n { var a := new nat[3](h.g); }
  function Z19(h: H, n: nat): seq<nat> decreases {:global} n { seq(3, h.g) }
}
