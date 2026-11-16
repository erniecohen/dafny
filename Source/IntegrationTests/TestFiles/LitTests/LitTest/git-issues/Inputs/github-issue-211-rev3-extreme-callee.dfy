// p2 through a home least predicate: the foreign Y.B, bounded at 1, calls P, which dispatches to the unbounded A.
// P has no bound (an extreme predicate cannot declare one) and reaches open dispatch, so the call is rejected.
// Without that check, Bad would prove false: the assertions below hold, and Bad itself verifies.
module Tr {
  trait T {
    ghost function A(other: T): int
    ghost function B(other: T): int decreases {:global} 1
  }
  class X extends T {
    constructor () { }
    ghost function A(other: T): int { other.B(this) }
    ghost function B(other: T): int decreases {:global} 1 { 0 }
  }
  least predicate P(t: T, o: T) { t.A(o) == 0 }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(other: Tr.T): int { 0 }
    ghost function B(other: Tr.T): int decreases {:global} 1 { if Tr.P(other, this) then 1 else 0 }
  }
  method Bad() ensures false {
    var x := new Tr.X();
    var y := new Y();
    assert Tr.P(x, y) <==> x.A(y) == 0;
    assert x.A(y) == y.B(x);
    assert y.B(x) == if Tr.P(x, y) then 1 else 0;
  }
}
