// Constraints and witnesses under --modular-termination (https://github.com/erniecohen/dafny/issues/211,
// milestone M3a): positives.  A member with a global bound may test, convert to and allocate types whose
// constraint, witness and components reach no dispatched call; a member without a bound may also use a type
// whose constraint does.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module S {
  import Tr
  function Leaf(x: int): bool { x != 7 }
  // The constraint calls known code that reaches no dispatched call.
  type {:termination_closed} Small = x: int | Leaf(x) witness 0
  newtype {:termination_closed} Byte = x: int | 0 <= x < 256
  datatype {:termination_closed} Box = Box(s: Small, b: Byte)
  // The constraint makes a dispatched call: a type without a bound may, to a callee with one.
  type Good = t: Tr.T | t.F(0) == 0 witness *
}

module Y {
  import Tr
  import S
  class Y extends Tr.T {
    constructor() { }
    // Y.F inherits the bound n.
    function F(n: nat): nat {
      var s: S.Small := 3;
      var b := (n % 256) as S.Byte;
      var x := S.Box(s, b);
      if s is S.Small && x.b as int < 256 then n else 0
    }
  }
  // Without a bound, a member may evaluate Good's constraint.
  function Probe(t: Tr.T): bool { t is S.Good }
  method Use(n: nat) returns (r: nat) decreases {:global} n {
    var a := new S.Small[n];
    var y := new Y();
    r := y.F(n);
    assert r == n;
  }
}
