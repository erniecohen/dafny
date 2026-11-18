// How a copying import meets the rules for the code that a refining module inherits.

// A copying import may take the place of an inherited abstract import if the copied module refines
// the target of that import (https://github.com/erniecohen/dafny/issues/408).

abstract module Idempotent {
  type T
  function F(t: T): T
  lemma Idem(t: T)
    ensures F(F(t)) == F(t)
}

module Constant refines Idempotent {
  datatype T = Zero | One
  function F(t: T): T { Zero }
  lemma Idem(t: T)
    ensures F(F(t)) == F(t)
  { }
}

abstract module User {
  import A : Idempotent
  lemma Twice(t: A.T)
    ensures A.F(A.F(t)) == A.F(t)
  {
    A.Idem(t);
  }
}

module BindsCopy refines User {
  import A := Constant
  lemma Use()
    ensures A.F(A.F(A.One)) == A.Zero
  {
    Twice(A.One);
  }
}

// The copy that takes the place of the abstract import is one copy among others.
module BindsOneOfTwo refines User {
  import A := Constant
  import B := Constant
  lemma Use(b: B.T)
    ensures A.F(A.F(A.One)) == A.F(A.One)
    ensures B.F(b) == B.Zero
  {
    Twice(A.One);
  }
}

// A refinement of the binding module gets a new copy, and the inherited lemmas are about it.
module BindsCopyAgain refines BindsCopy {
  lemma UseAgain()
    ensures A.F(A.F(A.One)) == A.Zero
  {
    Use();
  }
}

// A copy of the target itself also refines the target.
abstract module BindsCopyOfTarget refines User {
  import A := Idempotent
}

// A copy has no declaration of its own.  So the checks that a refining module makes again for an
// inherited member are not made for it: not the check of an override against a trait of the copied
// module (https://github.com/erniecohen/dafny/issues/404), not the check that a constructor assigns
// the fields (https://github.com/erniecohen/dafny/issues/411), and not the check of termination
// (https://github.com/erniecohen/dafny/issues/409).  Shapes has 8 proofs, and the six copies below
// add none.

module Shapes {
  trait Shape {
    function Sides(): nat
      ensures Sides() >= 3
    method Grow() returns (n: nat)
      ensures n >= Sides()
  }

  class Square extends Shape {
    var side: nat
    const colour: int

    constructor (s: nat)
      ensures side == s
    {
      side := s;
      colour := 0;
    }

    function Sides(): nat
      ensures Sides() == 4
    {
      4
    }

    method Grow() returns (n: nat)
      ensures n == 5
    {
      n := 5;
    }
  }

  function Sum(n: nat): nat
    decreases n
  {
    if n == 0 then 0 else n + Sum(n - 1)
  }

  lemma SumBound(n: nat)
    ensures Sum(n) >= n
  { }
}

module ThreeShapes {
  import A := Shapes
  import B := Shapes
  import C := Shapes
}

module ThreeMoreShapes refines ThreeShapes { }

// The one check that is made again for a copy.  Frames has a copying import, and a class of Frames
// extends a trait of that copy.  A copy of Frames has a new copy of Sided, so its class extends a
// trait that Frames did not check it against, and the check of the override is made again
// (https://github.com/erniecohen/dafny/issues/404).  Sided and Frames have 3 proofs, and each of
// the four copies of Frames below adds 1.

module Sided {
  trait {:termination false} {:axiom} Polygon {
    function Sides(): nat
      ensures Sides() >= 3
  }
}

module Frames {
  import Y := Sided

  class Frame extends Y.Polygon {
    constructor () { }

    function Sides(): nat
      ensures Sides() == 4
    {
      4
    }
  }
}

module TwoFrames {
  import A := Frames
  import B := Frames
}

module TwoMoreFrames refines TwoFrames { }
