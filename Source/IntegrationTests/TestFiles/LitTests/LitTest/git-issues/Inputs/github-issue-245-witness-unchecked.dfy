// An unchecked witness does not qualify for the logical-chain summary.
module Unchecked {
  import opened Std.Termination.Orders
  datatype Bad extends Wfo<nat> = Bad {
    ghost predicate gt(x: nat, y: nat) { true }
    ghost function {:verify false} aha(c: Chain<nat>): (r: nat)
      ensures !gt(c(r), c(r + 1))
    { var x := c(0); 0 }
  }
  ghost function Order(): Wfo<nat> { Bad }
  ghost function Bound(): nat
    decreases {:global} (1 as nat) by Order()
  { 0 }
}
