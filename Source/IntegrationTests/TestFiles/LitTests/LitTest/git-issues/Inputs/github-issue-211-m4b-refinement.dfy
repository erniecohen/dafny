abstract module Signature {
  trait {:termination false} T {
    method F(n: nat) decreases {:global} n
  }
  method {:termination_closed} Closed() { }
}
module Realization refines Signature { }
abstract module Client {
  import L : Realization
  method Call(t: L.T) decreases {:global} 1 { t.F(0); L.Closed(); }
}
module Concrete refines Client { import L = Realization }
