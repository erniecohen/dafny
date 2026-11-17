// The control for the github-issue-211-rev3-bounded-*.dfy attacks: foreign implementations of the unbounded A
// that declare a bound of their own and call home code through a known call, a function value and an extreme
// predicate, each of which stays below the bound.  Known and Apply have bounds below the implementations', the
// function value passed to Apply runs under its callback bound, and Ext reaches no dispatch and promises so.  All are accepted; the only error is the 'assert false'
// control at the end, which shows that the program is not vacuous.
module Tr {
  trait T {
    ghost function A(): int
    ghost function W(): int decreases {:global} 1
  }
  ghost function Known(t: T): int decreases {:global} 2 { t.W() + 1 }
  ghost function Apply(f: T -> int, t: T): int
    decreases {:global} 3
    decreases {:callback f} 2
  {
    f(t)
  }
  least predicate {:termination_closed} Ext(n: nat) { n == 0 || (n > 0 && Ext(n - 1)) }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(): int decreases {:global} 5 { Tr.Known(this) }
    ghost function W(): int decreases {:global} 1 { 0 }
  }
  class V extends Tr.T {
    constructor () { }
    ghost function A(): int decreases {:global} 5 { Tr.Apply((t: Tr.T) => t.W() + 1, this) }
    ghost function W(): int decreases {:global} 1 { 0 }
  }
  class E extends Tr.T {
    constructor () { }
    ghost function A(): int decreases {:global} 5 { if Tr.Ext(3) then 1 else 0 }
    ghost function W(): int decreases {:global} 1 { 0 }
  }
  lemma Values(y: Y, v: V)
    ensures y.A() == 1 && v.A() == 1
  {
  }
  lemma Control(y: Y)
    ensures false
  {
    assert y.A() == 1;
    assert false;
  }
}
