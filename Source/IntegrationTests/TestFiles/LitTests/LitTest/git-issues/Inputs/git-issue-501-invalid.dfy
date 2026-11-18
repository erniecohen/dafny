// Each declaration must retain its witness check.
module BaseConstraint {
  newtype NZ = x: int | x != 0 witness 1
  newtype Invalid = NZ {
    invariant this as int >= 2
    witness 0 // error: violates the base constraint (and invariant)
  }
}
module Invariant {
  newtype NZ = x: int | x != 0 witness 1
  newtype Invalid = NZ {
    invariant this as int >= 2
    witness 1 // error: satisfies the base, but violates the invariant
  }
}
module NoWitness {
  newtype NZ = x: int | x != 0 witness 1
  newtype Default = NZ {
    invariant this as int >= 2
  } // error: the implicit zero still violates the base constraint
}
