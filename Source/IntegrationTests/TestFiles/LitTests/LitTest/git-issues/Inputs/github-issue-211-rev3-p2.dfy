// Without a bound, A is TOP, not BOTTOM.  The home X.A dispatches to B; the foreign Y.B, bounded at 1,
// dispatches to A, above its bound, and is rejected.  Were A BOTTOM, every rule would hold, yet
// x.A(y) == x.A(y) + 1 would prove false.
module Tr {
  trait T {
    function A(other: T): int
    function B(other: T): int decreases {:global} 1
  }
  class X extends T {
    constructor () { }
    function A(other: T): int { other.B(this) }
    function B(other: T): int decreases {:global} 1 { 0 }
  }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    function A(other: Tr.T): int { 0 }
    function B(other: Tr.T): int decreases {:global} 1 { other.A(this) + 1 }
  }
}
