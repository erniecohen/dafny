// A trait with {:termination false}: the calls dispatched through it are not checked for
// termination, so Tie's methods recurse forever through T.F and still verify.
module Tr {
  trait {:termination false} T {
    method F(n: nat) returns (r: nat)
  }

  // The same assumption, acknowledged with {:axiom}: not reported.
  trait {:termination false} {:axiom} Acknowledged {
    method G(n: nat) returns (r: nat)
  }
}

module Tie {
  import Tr
  class C extends Tr.T {
    var other: Tr.T?
    method F(n: nat) returns (r: nat) {
      r := 0;
      if other != null {
        r := other.F(n);
      }
    }
  }
}
