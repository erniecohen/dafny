// A declared bound binds the home implementation too.  B has bound 1 and A bound 2; the home X.B
// dispatches to A, above its bound, and is rejected.  Otherwise the foreign Y.A, bounded at 2,
// dispatches to B (1 < 2), and x.B(y) == x.B(y) + 1 would prove false.
module Tr {
  trait T {
    function A(other: T): int decreases {:global} 2
    function B(other: T): int decreases {:global} 1
  }
  class X extends T {
    constructor () { }
    function A(other: T): int decreases {:global} 2 { 0 }
    function B(other: T): int decreases {:global} 1 { other.A(this) }
  }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    function A(other: Tr.T): int decreases {:global} 2 { other.B(this) + 1 }
    function B(other: Tr.T): int decreases {:global} 1 { 0 }
  }
}
