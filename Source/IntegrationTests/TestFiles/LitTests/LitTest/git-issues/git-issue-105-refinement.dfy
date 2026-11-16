// Member invariants (https://github.com/erniecohen/dafny/issues/105) in refinement.  An invariant is
// anonymous, so an invariant of a refining declaration never refines one of the refinement base: the
// refining declaration has the inherited invariants and its own, over any number of refinement steps.
// RUN: %resolve --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

abstract module M0 {
  datatype Positive = Positive(n: int) {
    invariant n > 0
  }
  type Hidden {
    invariant true
  }
  class Counter {
    const limit: int
    invariant limit > 0
    constructor () {
      limit := 1;
    }
  }
  trait Named {
    const name: string
    invariant |name| > 0
  }
}

// Each refined declaration adds invariants; the abstract type becomes a datatype with two of them.
abstract module M1 refines M0 {
  datatype Positive ... {
    invariant n < 100
  }
  datatype Hidden = Hidden(b: bool) {
    invariant b || !b
    invariant true
  }
  class Counter ... {
    invariant limit < 10
  }
  trait Named ... {
    invariant |name| < 10
  }
}

// A second step numbers its invariants after all the inherited ones.
module M2 refines M1 {
  datatype Positive ... {
    invariant n != 50
    invariant n != 51
  }
  datatype Hidden ... {
    invariant b == b
  }
  class Counter ... {
    invariant limit != 5
  }
  class Thing extends Named {
    invariant |name| != 5
    constructor () {
      name := "thing";
    }
  }
  function Get(p: Positive, h: Hidden): int { if h.b then p.n else 0 }
}

// A module that adds nothing keeps every inherited invariant.
module M3 refines M1 {
}
