// Input of git-issue-466-stage3-rejected.dfy: a module outside a recursive module group is another
// module to the traits of the group, as it is to the traits of any module it imports.

module Tr {
  import Y
  trait T {
    function A(other: T): int
    function B(other: T): int
  }
  function UseY(y: Y.Y): int { 0 }
}
module Y {
  import Tr
  class Y extends Tr.T {
    function A(other: Tr.T): int { 0 }
    function B(other: Tr.T): int { 1 }
  }
}
module X {
  import Tr
  class X extends Tr.T {
    function A(other: Tr.T): int { 0 }
    function B(other: Tr.T): int { other.A(this) }
  }
}
