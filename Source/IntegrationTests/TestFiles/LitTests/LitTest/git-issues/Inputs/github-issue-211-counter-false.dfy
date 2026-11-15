// Vacuity control for github-issue-211-counter.dfy: the same program, with an assert false
// after each call that the smaller-bound rule checks.  Each of the four must fail.
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
    function B(other: Tr.T, n: nat): int { if n == 0 then 0 else var r := other.A(this, n - 1); assert false; r }
    method M(other: Tr.T, n: nat) returns (r: int) {
      r := 0;
      if n != 0 {
        r := other.M(this, n - 1);
        assert false;
      }
    }
  }
}

module Y {
  import Tr
  class Y extends Tr.T {
    function A(other: Tr.T, n: nat): int { if n == 0 then 0 else var r := other.B(this, n - 1); assert false; r }
    function B(other: Tr.T, n: nat): int { 1 }
    // A smaller global bound than the trait's is allowed.
    method M(other: Tr.T, n: nat) returns (r: int) decreases {:global} n / 2 {
      r := 0;
      if n >= 4 {
        r := other.M(this, n / 2 - 1);
        assert false;
      }
    }
  }
}

module L {
  import Tr
  // L.Tie has no global bound: it is above every bound, so its call holds.
  function Tie(t1: Tr.T, t2: Tr.T, n: nat): int { t1.B(t2, n) }
}
