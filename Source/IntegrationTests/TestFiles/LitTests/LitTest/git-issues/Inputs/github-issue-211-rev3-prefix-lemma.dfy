// A least lemma's body is checked as its prefix lemma's, under the least lemma's bound.  L, bounded at 0,
// dispatches to M, bounded at 5, and is rejected.  Without that check, the foreign Y.M would prove false through L.
module Tr {
  trait T {
    lemma M(other: T) ensures false decreases {:global} 5
  }
  least predicate Q(x: int) { true }
  least lemma L(t: T, o: T, x: int) requires Q(x) ensures false decreases {:global} 0 { t.M(o); }
}
module Y { import Tr
  class Y extends Tr.T {
    lemma M(other: Tr.T) ensures false decreases {:global} 5 { Tr.L(other, this, 0); }
  }
}
