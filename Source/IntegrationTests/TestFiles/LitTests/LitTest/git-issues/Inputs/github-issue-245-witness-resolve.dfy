include "github-issue-245-witness-positive.dfy"

module Controls {
  import opened Std.Termination.Orders
  import UserOrder

  // Ordinary parameters do not become logical chains when passed to aha.
  ghost function Unknown(c: Chain<nat>): nat
    decreases {:global} 0
  { UserOrder.One().aha(c) }

  // A callback bound is not silently transferred through an ordinary aha call.
  ghost function Callback(c: Chain<nat>): nat
    decreases {:global} 1
    decreases {:callback c} 0
  { UserOrder.One().aha(c) }

  // Selecting a witness as a value must not use its abstract-chain summary.
  ghost function Value(): nat
    decreases {:global} 0
  { var f := UserOrder.One().aha; f((n: nat) => 0) }

  // The same restriction applies to the name as a value in a bound.
  ghost function ValueBound(): nat
    decreases {:global} (var f := UserOrder.One().aha; f((n: nat) => 0))
  { 0 }

  ghost function Take(f: Chain<nat> -> nat): nat
    decreases {:global} 1
    decreases {:callback f} 0
  { f((n: nat) => 0) }

  ghost function Passing(): nat
    decreases {:global} 2
  { Take(UserOrder.One().aha) }

  // Even without a caller bound, a closed promise cannot hide the actual chain's code.
  ghost function {:termination_closed} Closed(c: Chain<nat>): nat
  { UserOrder.One().aha(c) }

  // Only the resolved library witness qualifies, not a function named aha.
  ghost function aha(c: Chain<nat>): nat { c(0) }
  ghost function Fake(): nat
    decreases {:global} aha((n: nat) => 0)
  { 0 }
}
