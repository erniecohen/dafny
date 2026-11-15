// A library verified against the contracts of module Lib.
module Dependent {
  import Lib

  method Triple(n: nat) returns (r: nat)
    ensures r == 3 * n
  {
    var a := Lib.Count(n);
    r := a + Lib.Double(n);
  }
}
