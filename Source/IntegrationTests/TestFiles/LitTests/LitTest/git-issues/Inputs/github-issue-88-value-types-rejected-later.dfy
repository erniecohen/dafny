// Programs in which a value type gains a parent in a refinement, rejected by the resolver in a
// later phase than those of github-issue-88-value-types-rejected.dfy.

// ----- A member of the added parent may not capture a name that inherited text uses

module Capture0 {
  function G(): int {
    1
  }
  datatype D = X {
    lemma L()
      ensures G() == 1 // error (in Capture1): G would be S.G
    {
    }
  }
  newtype N = x: int | 0 <= x < 10 {
    lemma L()
      ensures G() == 1 // error (in Capture1): G would be S.G
    {
    }
  }
}

module Capture1 refines Capture0 {
  trait S {
    function G(): int {
      2
    }
  }
  datatype D extends S ... {}
  newtype N extends S ... {}
}
