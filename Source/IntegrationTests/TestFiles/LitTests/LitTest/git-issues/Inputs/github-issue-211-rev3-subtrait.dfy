// A subtrait in another module may redeclare an unbounded member with a bound (U.m) or a body (V.m).  The bound
// only lowers what a call through U sees; the implementations still implement the unbounded A.T.m, so they must
// be leaves and cannot declare a bound: K.m and K2.m are rejected.
module A {
  trait T {
    function m(): int
  }
}
module B { import A
  trait U extends A.T {
    function m(): int decreases {:global} 5
  }
  trait V extends A.T {
    function m(): int { 3 }
  }
}
module C { import A import B
  class K extends B.U {
    const t: A.T  constructor (t: A.T) { this.t := t; }
    function m(): int decreases {:global} 5 { t.m() }
  }
  class K2 extends B.U {
    const u: B.U  constructor (u: B.U) { this.u := u; }
    function m(): int { u.m() }
  }
}
