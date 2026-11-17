// As github-issue-211-rev3-bounded-known.dfy, through an extreme predicate: the home least predicate Ext has no
// bound and dispatches to the unbounded A, and the foreign Y.A, bounded at 5, evaluates it.  Ext is TOP, so the
// call is rejected.  Were it accepted, y.A() would be 0 exactly when it is not, and False would prove false:
// False verifies.
module Tr {
  trait T {
    ghost function A(): int
  }
  least predicate Ext(t: T) { t.A() == 0 }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function A(): int decreases {:global} 5 { if Tr.Ext(this) then 1 else 0 }
  }
  lemma False(y: Y)
    ensures false
  {
    if y.A() == 0 {
      assert Tr.Ext#[1](y);
      assert Tr.Ext(y);
      assert y.A() == 1;
    } else {
      assert !Tr.Ext(y);
      assert y.A() == 0;
    }
  }
}
