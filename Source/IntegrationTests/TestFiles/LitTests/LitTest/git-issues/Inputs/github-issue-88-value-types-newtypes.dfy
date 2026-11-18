// Newtypes that gain a parent in a refinement.  A newtype needs --general-traits=full to extend a
// trait.

// ----- A newtype gains a parent

module Issue0 {
  newtype N = x: int | 0 <= x < 10 {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
}

module Issue1 refines Issue0 {
  trait S {
    const k: int := 5
    function One(): int
      ensures One() == 1
    function Two(): int {
      One() + 1
    }
  }
  newtype N extends S ... {}

  method M(n: N) {
    assert n.k == 5;
    assert n.Two() == 2;
    assert false; // error
  }
}

// ----- An inherited member that becomes an override is checked

module Override0 {
  newtype N = x: int | 0 <= x < 10 {
    function F(): int { // error (in Override1): S.F promises 2
      1
    }
  }
}

module Override1 refines Override0 {
  trait S {
    function F(): int
      ensures F() == 2
  }
  newtype N extends S ... {}
}

module OverrideControl {
  trait S {
    function F(): int
      ensures F() == 2
  }
  newtype N extends S = x: int | 0 <= x < 10 {
    function F(): int { // error: S.F promises 2
      1
    }
  }
}

// ----- A call in inherited text that the new override puts on a cycle must terminate

module Cycle0 {
  trait S {
    function F(s: S): int
  }
  newtype N = x: int | 0 <= x < 10 {
    function F(s: S): int {
      s.F(s) + 1 // error (in Cycle1): s may be this
    }
  }
}

module Cycle1 refines Cycle0 {
  newtype N extends S ... {}
}

module CycleControl {
  trait S {
    function F(s: S): int
  }
  newtype N extends S = x: int | 0 <= x < 10 {
    function F(s: S): int {
      s.F(s) + 1 // error: s may be this
    }
  }
}

// ----- A chain of refinements

module Chain0 {
  newtype N = x: int | 0 <= x < 10 {
    function One(): int
      ensures One() == 1
    {
      1
    }
  }
}

module Chain1 refines Chain0 {
  trait P {
    function One(): int
      ensures One() == 1
    function OneMore(): int {
      One() + 1
    }
  }
  newtype N extends P ... {}
}

module Chain2 refines Chain1 {
  trait Q {
    function Two(): int
      ensures Two() == 2
    function TwoMore(): int {
      Two() + 1
    }
  }
  newtype N extends Q ... {
    function Two(): int
      ensures Two() == 2
    {
      2
    }
  }

  method M(n: N) {
    assert n.OneMore() + n.TwoMore() == 5;
    assert false; // error
  }
}
