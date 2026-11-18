// Dafny 4.11 checks only the edges of extension that cross modules.  A class that extends, within its own module, a
// trait that extends a trait of another module under @AssumeCrossModuleTermination implements the foreign members
// under that premise (as Std.Actions.FunctionAction does), so it need not be a leaf.  The design is in use here (the
// global bound of Down), so the leaf rule runs.
module Tr {
  trait T { function Size(): nat  method Run(other: T) }
  method Down(n: nat) decreases {:global} n { if n > 0 { Down(n - 1); } }
}
module A { import Tr
  @AssumeCrossModuleTermination trait Mid extends Tr.T { }
  class C extends Mid {
    constructor () { }
    function Size(): nat { 0 }
    method Run(other: Tr.T) { other.Run(this); }           // accepted: a premise, through Mid
  }
}
module B { import Tr
  trait Mid extends Tr.T { }                               // an unmarked crossing edge
  class C extends Mid {
    constructor () { }
    function Size(): nat { 0 }
    method Run(other: Tr.T) { other.Run(this); }           // error: not a leaf
  }
}
