// The Tie program with constant global bounds.  One of X.B's and Y.A's calls must go up:
// bound(A) < bound(B) and bound(B) < bound(A) cannot both hold.  Y.A's call is rejected.
module Tr {
  trait {:termination false} T {
    function A(other: T): int decreases {:global} 1
    function B(other: T): int decreases {:global} 2
  }
}

module X {
  import Tr
  class X extends Tr.T {
    function A(other: Tr.T): int { 0 }
    function B(other: Tr.T): int { other.A(this) }
  }
}

module Y {
  import Tr
  class Y extends Tr.T {
    function A(other: Tr.T): int { other.B(this) }
    function B(other: Tr.T): int { 1 }
  }
}

module L {
  import Tr
  function Tie(t1: Tr.T, t2: Tr.T): int { t1.B(t2) }
}
