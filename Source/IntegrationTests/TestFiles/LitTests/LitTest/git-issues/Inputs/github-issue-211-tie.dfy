// fabiomadge's Tie program (dafny-lang/dafny#1588): every module's call graph is acyclic,
// yet Tie(x, y) evaluates x.B(y) = y.A(x) = x.B(y) = ... forever.
module Tr {
  trait {:termination false} T {
    function A(other: T): int
    function B(other: T): int
  }
}

module X {
  import Tr
  class X extends Tr.T {
    function A(other: Tr.T): int { 0 }
    function B(other: Tr.T): int { other.A(this) }
  }
}

module Y {
  import Tr
  class Y extends Tr.T {
    function A(other: Tr.T): int { other.B(this) }
    function B(other: Tr.T): int { 1 }
  }
}

module L {
  import Tr
  function Tie(t1: Tr.T, t2: Tr.T): int { t1.B(t2) }
}
