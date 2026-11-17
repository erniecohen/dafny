// Input of git-issue-466-stage2-unsupported.dfy: a class that extends a trait of another member of
// its recursive module group.  The override checks of a group are a later stage.

module Trait1 {
  import Trait2
  trait T {
    function F(n: nat): nat
      decreases n
  }
  function Use(c: Trait2.C): nat { c.F(0) }
}
module Trait2 {
  import Trait1
  class C extends Trait1.T {
    function F(n: nat): nat
      decreases n
    {
      Trait1.Use(this)
    }
  }
}
