// Input of git-issue-466-stage2-unsupported.dfy: inherited invariants are supported in a recursive
// module group, including when a trait of another member inherits one from below the group.

module Shapes {
  trait {:termination false} Shape {
    const sides: int := 4
    invariant sides >= 3
  }
  trait {:termination false} Cell extends object {
    const limit: int
    invariant limit >= 0
  }
}

// a datatype of a member
module Datatype1 {
  import Shapes
  import Datatype2
  datatype Square extends Shapes.Shape = Square(s: int)
  function F(b: Datatype2.Box): int { 0 }
}
module Datatype2 {
  import Datatype1
  datatype Box = Box(q: Datatype1.Square)
  // would follow from the inherited invariant
  lemma Sides(b: Box)
    ensures b.q.sides >= 3
  {
  }
}

// a class of a member
module Class1 {
  import Shapes
  import Class2
  class Counter extends Shapes.Cell {
    constructor ()
    {
      limit := 0;
    }
    method Use(u: Class2.User)
    {
    }
  }
}
module Class2 {
  import Class1
  class User {
    constructor ()
    {
    }
  }
  lemma Limit(c: Class1.Counter)
    ensures c.limit >= 0
  {
  }
}

// a trait of one member that inherits the invariant, and a datatype of another member that extends it
module Trait1 {
  import Trait2
  datatype Circle extends Trait2.Round = Circle(r: int)
}
module Trait2 {
  import Shapes
  import Trait1
  trait Round extends Shapes.Shape {
  }
  function Radius(c: Trait1.Circle): int { c.r }
}
