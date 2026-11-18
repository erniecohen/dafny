// Finished operands are allowed in the cone; raw tests and conversions are allowed outside it.
module TwoA {
  import TwoB
  datatype T = Leaf(n: int) | Node(n: int, child: T) {
    invariant n >= 0
    invariant Node? ==> TwoB.Good(child)
    witness Leaf(0)
  }
  function Unit(): T { Node(1, Leaf(2)) }
}
module TwoB {
  import TwoA
  ghost predicate Good(t: TwoA.T) { (t is TwoA.T) && (t as TwoA.T).n >= 0 }

  // No path from T's invariants reaches these functions.
  ghost predicate Test(r: raw TwoA.T) { r is TwoA.T }
  ghost function Cast(r: raw TwoA.T): TwoA.T
    requires r.Leaf? && r.n >= 0
  { r as TwoA.T }
  ghost function Convert(r: raw TwoA.T): TwoA.T
    requires r.Leaf? && r.n >= 0
  { r }
  lemma Facts() {
    var t := TwoA.Unit();
    assert Good(t);
    var r: raw TwoA.T := TwoA.T.Leaf(3);
    assert Test(r);
    assert Cast(r).n == 3;
    assert Convert(r).n == 3;
  }
}

module ThreeA {
  import ThreeB
  datatype T = Leaf(n: int) | Node(n: int, child: T) {
    invariant n >= 0
    invariant Node? ==> ThreeB.Good(child)
    witness Leaf(0)
  }
  function Unit(): T { Node(1, Leaf(2)) }
}
module ThreeB {
  import ThreeA
  import ThreeC
  ghost predicate Good(t: ThreeA.T) { ThreeC.Good(t) }
}
module ThreeC {
  import ThreeA
  ghost predicate Good(t: ThreeA.T) { (t is ThreeA.T) && (t as ThreeA.T).n >= 0 }

  // No path from T's invariants reaches these functions.
  ghost predicate Test(r: raw ThreeA.T) { r is ThreeA.T }
  ghost function Cast(r: raw ThreeA.T): ThreeA.T
    requires r.Leaf? && r.n >= 0
  { r as ThreeA.T }
  ghost function Convert(r: raw ThreeA.T): ThreeA.T
    requires r.Leaf? && r.n >= 0
  { r }
  lemma Facts() {
    var t := ThreeA.Unit();
    assert Good(t);
    var r: raw ThreeA.T := ThreeA.T.Leaf(3);
    assert Test(r);
    assert Cast(r).n == 3;
    assert Convert(r).n == 3;
  }
}
