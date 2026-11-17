// A subtrait in another module may redeclare an unbounded member with a bound (U.m) or a body (V.m).  The bound
// binds the implementations of U.m, and a call through U sees it: Use dispatches to it from bound 6.  An
// implementation of A.T.m outside A may also declare a bound of its own (K3.m), and dispatch below it, to w.  K.m,
// bounded at 5 through U, dispatches to the unbounded A.T.m, which is TOP, and is rejected: were it accepted,
// k.m(k) == k.m(k) + 1 and False would prove false.
module A {
  trait T {
    function m(x: T): int
    function w(): int decreases {:global} 1
  }
}
module B { import A
  trait U extends A.T {
    function m(x: A.T): int decreases {:global} 5
  }
  trait V extends A.T {
    function m(x: A.T): int { 3 }
  }
  function Use(u: U): int decreases {:global} 6 { u.m(u) }
}
module C { import A import B
  class K extends B.U {
    constructor () { }
    function m(x: A.T): int { x.m(x) + 1 }
    function w(): int { 0 }
  }
  class K2 extends B.U {
    constructor () { }
    function m(x: A.T): int { 0 }
    function w(): int { 0 }
  }
  class K3 extends A.T {
    constructor () { }
    function m(x: A.T): int decreases {:global} 7 { x.w() }
    function w(): int { 0 }
  }
  lemma False(k: K)
    ensures false
  {
    var t: A.T := k;
    assert k.m(t) == t.m(t) + 1;
    assert t.m(t) == k.m(t);
  }
}
