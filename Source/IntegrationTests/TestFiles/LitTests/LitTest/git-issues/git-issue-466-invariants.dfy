// Invariants in recursive module groups: cross-module predicates, inherited invariants, newtypes,
// and raw members retain their checks and facts. Only the reachable assertion of false fails.
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules "%s" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --modular-termination "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Ranges {
  import Bounds
  datatype Range = Range(lo: int, hi: int) {
    invariant Bounds.Ordered(lo, hi)
    predicate Ordered() { Bounds.Ordered(lo, hi) }
  }
  function Make(): Range { Range(0, 1) }
  newtype Positive = int {
    invariant this > 0
  }
  method One() returns (p: Positive) { p := 1; }
}
module Bounds {
  import Ranges
  predicate Ordered(lo: int, hi: int) { lo <= hi }
  lemma Use(r: Ranges.Range) {
    assert r.lo <= r.hi;
  }
  lemma Nonvacuity() {
    var r := Ranges.Make();
    assert r.lo <= r.hi;
    assert false; // the only failure
  }
}

// The derived invariant types must exist before any signature is resolved, even when the
// inherited invariant travels against source order through two other members of the group.
module Cells {
  import Middle
  class Cell extends Middle.Mid {
    constructor () { n := 1; }
  }
  lemma Use(c: Cell) { assert c.n > 0; }
}
module Middle {
  import Roots
  trait Mid extends Roots.Root { }
}
module Roots {
  import Cells
  trait Root extends object {
    const n: int
    invariant n > 0
  }
  lemma Use(c: Cells.Cell) { assert c.n > 0; }
}
