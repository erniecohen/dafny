// Run with --type-system-refresh --general-traits:datatype.  Evaluations of a constraint, witness or component default that --modular-termination rejects
// (https://github.com/erniecohen/dafny/issues/211, milestone M3a).  Each is marked "rejected": a member with
// a global bound mentions a type whose constraint, witness or components reach a dispatched call, and that
// code has no bound (TOP), which is above the member's.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
    method M(n: nat) returns (r: nat) decreases {:global} n
  }
}

module S {
  import Tr
  type Good = t: Tr.T | t.F(0) == 0 witness *
  // Through a function that reaches the dispatched call.
  function Zero(t: Tr.T): bool { t.F(0) == 0 }
  type Good2 = t: Tr.T | Zero(t) witness *
  // A newtype whose witness reaches the dispatched call, through a constant.
  newtype N = x: int | true witness K
  datatype D extends Tr.T = D0 {
    function F(n: nat): nat { 0 }
    method M(n: nat) returns (r: nat) { r := 0; }
  }
  const K: int := var t: Tr.T := D0; t.F(0)
  // A datatype with a component of type Good2.
  datatype Holder = Holder(g: Good2)
}

module Y {
  import Tr
  import S
  datatype Y extends Tr.T = Y {
    // Rejected: a type test.
    function F(n: nat): nat { var me: Tr.T := this; if me is S.Good then 1 else 0 }
    method M(n: nat) returns (r: nat) {
      // Rejected: a conversion to a newtype, and the constant.
      var k := 3 as S.N;
      var c := S.K;
      // Rejected: a subset check.
      var g: S.Good2 := *;
      // Rejected: a datatype with a component of type Good2.
      var h := S.Holder(g);
      r := 0;
    }
  }
  // Rejected: the result type.
  function Res(t: S.Good2, n: nat): S.Good2 decreases {:global} n { t }
  // Holds: no bound.
  function Free(t: Tr.T): bool { t is S.Good && t is S.Good2 }
}
