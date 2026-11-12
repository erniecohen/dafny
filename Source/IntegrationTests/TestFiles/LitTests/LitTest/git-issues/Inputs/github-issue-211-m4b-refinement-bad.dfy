abstract module Signature {
  trait {:termination false} T {
    method F(n: nat) decreases {:global} n
  }
  method {:termination_closed} Closed(t: T)
}
module Realization refines Signature {
  method Closed(t: T) { t.F(0); }
}
