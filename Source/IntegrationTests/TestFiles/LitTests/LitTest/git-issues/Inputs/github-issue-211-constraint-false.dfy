// Vacuity control for github-issue-211-constraint.dfy: the same program, with an assert false after each
// use of a type that the rules allow.  Each must fail.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat decreases {:global} n
  }
}

module S {
  import Tr
  function Leaf(x: int): bool { x != 7 }
  type {:termination_closed} Small = x: int | Leaf(x) witness 0
  newtype {:termination_closed} Byte = x: int | 0 <= x < 256
  datatype {:termination_closed} Box = Box(s: Small, b: Byte)
  type Good = t: Tr.T | t.F(0) == 0 witness *
}

module Y {
  import Tr
  import S
  class Y extends Tr.T {
    constructor() { }
    function F(n: nat): nat {
      var s: S.Small := 3;
      var b := (n % 256) as S.Byte;
      var x := S.Box(s, b);
      assert false;
      if s is S.Small && x.b as int < 256 then n else 0
    }
  }
  function Probe(t: Tr.T): bool { var r := t is S.Good; assert false; r }
  method Use(n: nat) returns (r: nat) decreases {:global} n {
    var a := new S.Small[n];
    if * {
      assert false;
    }
    var y := new Y();
    r := y.F(n);
    if * {
      assert false;
    }
  }
}
