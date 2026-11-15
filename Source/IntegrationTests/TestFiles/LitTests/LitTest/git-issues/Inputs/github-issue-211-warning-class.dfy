// A class with @AssumeCrossModuleTermination extends a trait of another module that has no
// {:termination false}: the calls dispatched to it through that trait are not checked for
// termination.
module Tr {
  trait T {
    method F(n: nat) returns (r: nat)
  }
}

module Impl {
  import Tr
  @AssumeCrossModuleTermination
  class C extends Tr.T {
    var other: Tr.T?
    method F(n: nat) returns (r: nat) {
      r := 0;
      if other != null {
        r := other.F(n);
      }
    }
  }

  // The same assumption, acknowledged with {:axiom}: not reported.
  @AssumeCrossModuleTermination
  class {:axiom} D extends Tr.T {
    method F(n: nat) returns (r: nat) {
      r := n;
    }
  }

  // A trait may carry the attribute too.
  @AssumeCrossModuleTermination
  trait U extends Tr.T {
  }
}
