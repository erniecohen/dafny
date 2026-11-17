// As github-issue-211-rev3-bounded-known.dfy, through a function value: the home Thunk returns a function value
// whose body dispatches to the unbounded A, and the foreign Y.A, bounded at 5, applies it.  The application is
// rejected.  Were it accepted, y.A() == y.A() + 1 and False would prove false: False verifies.
module Tr {
  trait T {
    ghost function A(): int
  }
  ghost function Thunk(t: T): () -> int { () => t.A() + 1 }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(): int decreases {:global} 5 { Tr.Thunk(this)() }
  }
  lemma False(y: Y)
    ensures false
  {
    assert y.A() == Tr.Thunk(y)() == y.A() + 1;
  }
}
