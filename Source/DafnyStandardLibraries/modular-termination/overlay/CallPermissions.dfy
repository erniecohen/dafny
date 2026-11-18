/*******************************************************************************
 *  Copyright by the contributors to the Dafny Project
 *  SPDX-License-Identifier: MIT
 *******************************************************************************/

module Std.CallPermissions {
  // This is an entry allowance, not a virtual computation or a mutable progress metric.
  trait {:termination_closed} Ranked extends object {
    ghost const {:termination_closed} allowance: ORDINAL
  }

  // A producer may allocate new objects, but their allowances must fit this immutable ceiling.
  // Each producing protocol proves the strict inequality in its result postcondition.
  trait {:termination_closed} ProducesRanked extends Ranked {
    ghost const {:termination_closed} producedAllowanceCeiling: ORDINAL
  }
}
