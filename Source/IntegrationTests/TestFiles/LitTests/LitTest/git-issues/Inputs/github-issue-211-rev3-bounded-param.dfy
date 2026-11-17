// A foreign implementation of a member without a bound that declares a bound is checked like any bounded code:
// Y.F, bounded at 5, applies the function value it is given, whose code is not known, so it has no bound.  The
// application is rejected.  (github-issue-211-rev3-bounded-value.dfy applies a value made by home code that
// dispatches to the unbounded member.)
module Tr {
  trait T {
    ghost function F(g: () -> int): int
  }
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    ghost function F(g: () -> int): int decreases {:global} 5 { g() }
  }
}
