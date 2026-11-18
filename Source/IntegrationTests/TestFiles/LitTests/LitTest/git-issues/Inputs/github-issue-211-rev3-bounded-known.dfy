// A foreign implementation of a member without a bound may declare a bound of its own.  Here Y.A, bounded at 5,
// implements the unbounded A and calls the home function Known, which has no bound and dispatches to A.  Known is
// TOP, above Y.A's bound, so the call is rejected.  Were it accepted, y.A() == y.A() + 1 and False would prove
// false: False verifies.
module Tr {
  trait T {
    ghost function A(): int
  }
  ghost function Known(t: T): int { t.A() + 1 }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(): int decreases {:global} 5 { Tr.Known(this) }
  }
  lemma False(y: Y)
    ensures false
  {
    assert y.A() == Tr.Known(y) == y.A() + 1;
  }
}
