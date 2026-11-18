// Value types that gain a parent in a refinement, converted to that parent.  Only the refreshed
// type system has these conversions.  The methods named WouldBeBad show what the rejected text
// would prove: each verifies if the check that rejects its module is switched off.

// ----- A value of the refined type is a value of the added parent

module Issue0 {
  datatype D = X | Y {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
  newtype N = x: int | 0 <= x < 10 {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
  datatype G<X> = Mk(x: X) {
    function Get(): seq<X>
      ensures Get() == [x]
    {
      [x]
    }
  }
}

module Issue1 refines Issue0 {
  trait S {
    function One(): int
      ensures One() == 1
  }
  trait SG<Y> {
    function Get(): seq<Y>
  }
  datatype D extends S ... {}
  newtype N extends S ... {}
  datatype G<X> extends SG<X> ... {}

  method M() {
    var d: D := Y;
    var s: S := d;
    assert s.One() == 1;
    assert s is D && s as D == d;
    var n: N := 3;
    s := n;
    assert s.One() == 1;
    assert s is N;
    var g: G<int> := Mk(3);
    var sg: SG<int> := g;
    assert sg.Get() == g.Get() == [3];
    assert false; // error
  }
}

// ----- An inherited member that becomes an override is checked

module Override0 {
  datatype D = X {
    function F(): int { // error (in Override1): S.F promises 2
      1
    }
    lemma FIsOne()
      ensures F() == 1
    {
    }
  }
  newtype N = x: int | 0 <= x < 10 {
    function F(): int { // error (in Override1): S.F promises 2
      1
    }
    lemma FIsOne()
      ensures F() == 1
    {
    }
  }
}

module Override1 refines Override0 {
  trait S {
    function F(): int
      ensures F() == 2
  }
  datatype D extends S ... {}
  newtype N extends S ... {}

  method WouldBeBad()
    ensures false
  {
    var d: D := X;
    var s: S := d;
    d.FIsOne();
    assert s.F() == d.F();
  }

  method WouldBeBadToo()
    ensures false
  {
    var n: N := 3;
    var s: S := n;
    n.FIsOne();
    assert s.F() == n.F();
  }
}

// ----- A call in inherited text that the new override puts on a cycle must terminate

module Cycle0 {
  trait S {
    function F(s: S): int
  }
  datatype D = X {
    function F(s: S): int {
      s.F(s) + 1 // error (in Cycle1): s may be this
    }
    lemma Unfold(s: S)
      ensures F(s) == s.F(s) + 1
    {
    }
  }
}

module Cycle1 refines Cycle0 {
  datatype D extends S ... {}

  method WouldBeBad()
    ensures false
  {
    var d: D := X;
    var s: S := d;
    d.Unfold(s);
    assert s.F(s) == d.F(s);
  }
}
