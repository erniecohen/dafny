// A program that does not use the design of issue 211: traits extended within their module, one extended from another
// module under {:termination false}, a type with @AssumeCrossModuleTermination, function values and recursion.  Its
// Boogie program is the same with and without --legacy-no-modular-termination.
module Tr {
  trait {:termination false} T { function Get(): nat  method Go(other: T) }
  trait U { function Size(): nat }
  class C extends U { function Size(): nat { 1 } }
  function Twice(f: nat -> nat, x: nat): nat { f(f(x)) }
  function Fact(n: nat): nat { if n == 0 then 1 else n * Fact(n - 1) }
}
module A { import Tr
  class A extends Tr.T {
    constructor () { }
    function Get(): nat { Tr.Fact(3) }
    method Go(other: Tr.T) { var g := other.Get(); }
  }
  @AssumeCrossModuleTermination class B extends Tr.U {
    function Size(): nat { Tr.Twice(x => x + 1, 0) }
  }
  method M(t: Tr.T, u: Tr.U) { t.Go(t); var s := u.Size(); }
}
