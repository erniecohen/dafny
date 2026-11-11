// A trait with {:termination false} whose dispatched call --modular-termination accepts: the call goes
// to a smaller global bound.  The option checks it here, but not in code verified without the option,
// so --warn-unchecked-termination still reports the trait.
module Tr {
  trait {:termination false} T {
    function F(n: nat): nat
      decreases {:global} n
  }
}

module Impl {
  import Tr
  class C extends Tr.T {
    const other: Tr.T
    constructor (other: Tr.T) { this.other := other; }
    function F(n: nat): nat
      decreases {:global} n
    {
      if n == 0 then 0 else other.F(n - 1)
    }
  }
}
