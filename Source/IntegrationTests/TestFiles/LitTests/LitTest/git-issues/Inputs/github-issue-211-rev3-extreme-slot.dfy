// The unbounded slot A is a least predicate, and the foreign Y.B, bounded at 1, dispatches to it: A is TOP, so
// the call is rejected.  Without that check, Bad would prove false: the assertions below hold, and Bad verifies.
module Tr {
  trait T {
    least predicate A(other: T)
    ghost function B(other: T): int decreases {:global} 1
  }
  class X extends T {
    constructor () { }
    least predicate A(other: T) { other.B(this) == 0 }
    ghost function B(other: T): int decreases {:global} 1 { 0 }
  }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    least predicate A(other: Tr.T) { false }
    ghost function B(other: Tr.T): int decreases {:global} 1 { if other.A(this) then 1 else 0 }
  }
  method Bad() ensures false {
    var x := new Tr.X();
    var y := new Y();
    var tx: Tr.T := x;
    assert tx.A(y) <==> y.B(x) == 0;
    assert y.B(x) == if tx.A(y) then 1 else 0;
  }
}
