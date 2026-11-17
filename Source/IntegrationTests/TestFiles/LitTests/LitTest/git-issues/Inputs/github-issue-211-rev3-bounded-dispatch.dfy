// As github-issue-211-rev3-bounded-known.dfy, directly and around a bounded member, in the shape of
// github-issue-211-rev3-p2.dfy: the foreign Y.A, bounded at 2, implements the unbounded A and dispatches to the
// bounded B (1 < 2), which is accepted; the foreign Y.B dispatches back to A, which is TOP, and Z.A, bounded at
// 2, dispatches to A itself.  Both are rejected.  Were they accepted, y.A(y) == y.A(y) + 1 and z.A() ==
// z.A() + 1, and False would prove false: False verifies.
module Tr {
  trait T {
    ghost function A(other: T): int
    ghost function B(other: T): int decreases {:global} 1
  }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(other: Tr.T): int decreases {:global} 2 { other.B(this) }
    ghost function B(other: Tr.T): int decreases {:global} 1 { other.A(this) + 1 }
  }
  class Z extends Tr.T {
    constructor () { }
    ghost function A(other: Tr.T): int decreases {:global} 2 { var me: Tr.T := this; me.A(other) + 1 }
    ghost function B(other: Tr.T): int decreases {:global} 1 { 0 }
  }
  lemma False(y: Y, z: Z)
    ensures false
  {
    assert y.A(y) == y.B(y) == y.A(y) + 1;
  }
}
