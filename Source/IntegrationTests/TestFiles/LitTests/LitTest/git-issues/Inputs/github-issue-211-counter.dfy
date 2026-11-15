// The Tie program with a counter that every dispatched call decreases: it terminates, and
// verifies under --modular-termination.  The overrides inherit the trait's global bounds.
module Tr {
  trait {:termination false} T {
    function A(other: T, n: nat): int decreases {:global} n
    function B(other: T, n: nat): int decreases {:global} n
    method M(other: T, n: nat) returns (r: int) decreases {:global} n
  }
}

module X {
  import Tr
  class X extends Tr.T {
    function A(other: Tr.T, n: nat): int { 0 }
    function B(other: Tr.T, n: nat): int { if n == 0 then 0 else other.A(this, n - 1) }
    method M(other: Tr.T, n: nat) returns (r: int) {
      r := 0;
      if n != 0 {
        r := other.M(this, n - 1);
      }
    }
  }
}

module Y {
  import Tr
  class Y extends Tr.T {
    function A(other: Tr.T, n: nat): int { if n == 0 then 0 else other.B(this, n - 1) }
    function B(other: Tr.T, n: nat): int { 1 }
    // A smaller global bound than the trait's is allowed.
    method M(other: Tr.T, n: nat) returns (r: int) decreases {:global} n / 2 {
      r := 0;
      if n >= 4 {
        r := other.M(this, n / 2 - 1);
      }
    }
  }
}

module L {
  import Tr
  // L.Tie has no global bound: it is above every bound, so its call holds.
  function Tie(t1: Tr.T, t2: Tr.T, n: nat): int { t1.B(t2, n) }
}
