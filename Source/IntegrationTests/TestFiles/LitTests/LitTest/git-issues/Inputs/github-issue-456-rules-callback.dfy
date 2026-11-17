// https://github.com/erniecohen/dafny/issues/456: a least or greatest lemma may declare a callback bound.  A lambda passed for
// the parameter runs under that bound, in a call of the lemma and in a call of its prefix lemma, so a lambda
// that calls code with a larger bound is rejected.  The arguments differ so that a bound taken from the wrong
// parameter of the prefix lemma would show.
module Lib {
  trait {:termination false} T {
    function F(n: nat): nat
      decreases {:global} n
  }
  function G(t: T, n: nat): nat
    decreases {:global} n + 1
  {
    t.F(n)
  }
  least predicate {:termination_closed} Even(x: nat) { x == 0 || (x >= 2 && Even(x - 2)) }
  least lemma Walk(g: nat -> nat, a: nat, b: nat, x: nat)
    requires Even(x)
    decreases {:global} a + 1
    decreases {:callback g} a
  {
    var y := g(0);
  }
  greatest lemma Co(g: nat -> nat, a: nat, b: nat)
    ensures true
    decreases {:global} a + 1
    decreases {:callback g} a
  {
    var y := g(0);
  }
}
module Client {
  import opened Lib
  lemma A19(t: T, x: nat) requires Even(x) decreases {:global} 5 { Walk(n => G(t, 100), 3, 1000, x); }
  lemma A20(t: T, x: nat) requires Even#[7](x) decreases {:global} 5 { Walk#[7](n => G(t, 100), 3, 1000, x); }
  lemma A21(t: T, x: nat) requires Even(x) decreases {:global} 5 { Walk(n => G(t, 3), 3, 1000, x); }
  lemma A22(t: T) decreases {:global} 5 { Co(n => G(t, 100), 3, 1000); }
  lemma A23(t: T) decreases {:global} 5 { Co#[7](n => G(t, 100), 3, 1000); }
}
