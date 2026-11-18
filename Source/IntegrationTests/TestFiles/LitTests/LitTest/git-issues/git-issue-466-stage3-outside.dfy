// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), third
// stage: a trait of a recursive module group that a module outside the group extends.  The rule
// for the members of a group does not reach that module: it may extend the trait only if the trait
// has {:termination false}, and then the calls dispatched to its overrides are not checked for
// termination, as for any trait with that attribute (https://github.com/erniecohen/dafny/issues/211).
// So the first run verifies the program below, the lemma that proves 'false' included, on that
// unchecked premise, which the warning reports.  With --modular-termination the dispatched calls
// are checked, and the second run rejects the two calls that tie the knot.
// RUN: %verify --member-invariants --recursive-modules --allow-warnings "%s" > "%t"
// RUN: %exits-with 4 %verify --member-invariants --recursive-modules --allow-warnings --modular-termination "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Tr and Y are a group; X is above it
module Tr {
  import Y
  trait {:termination false} T {
    function A(other: T): int
    function B(other: T): int
  }
  function UseY(y: Y.Y): int { 0 }
}
module Y {
  import Tr
  class Y extends Tr.T {
    constructor () { }
    function A(other: Tr.T): int { other.B(this) + 1 }
    function B(other: Tr.T): int { 1 }
  }
}
module X {
  import Tr
  class X extends Tr.T {
    constructor () { }
    function A(other: Tr.T): int { 0 }
    function B(other: Tr.T): int { other.A(this) }
  }
}
module L {
  import Tr
  import X
  import Y
  method Bad()
    ensures false
  {
    var x := new X.X();
    var y := new Y.Y();
    assert x.B(y) == y.A(x) == x.B(y) + 1;
  }
}
