// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked unsigned division and remainder; requires --integer-bitwise.
// Check these bodies with --solver-option O:smt.bv.solver=2. Clients and the
// included Boolean/conversion proof closure retain the default engine.
// See docs/dev/integer-bitwise-references.md.
include "IntegerBitwiseNative.dfy"

module Std.Arithmetic.IntegerBitwiseNativeDivision8 {
  // Nonzero unsigned division agrees with Euclidean integer division.
  lemma {:induction false} Divide(x:bv8,y:bv8)
    requires y!=0
    ensures (x/y) as int==(x as int)/(y as int)
  {}

  lemma {:induction false} Remainder(x:bv8,y:bv8)
    requires y!=0
    ensures (x%y) as int==(x as int)%(y as int)
  {}
}

module Std.Arithmetic.IntegerBitwiseNativeDivision16 {
  // Nonzero unsigned division agrees with Euclidean integer division.
  lemma {:induction false} Divide(x:bv16,y:bv16)
    requires y!=0
    ensures (x/y) as int==(x as int)/(y as int)
  {}

  lemma {:induction false} Remainder(x:bv16,y:bv16)
    requires y!=0
    ensures (x%y) as int==(x as int)%(y as int)
  {}
}

module Std.Arithmetic.IntegerBitwiseNativeDivision32 {
  // Nonzero unsigned division agrees with Euclidean integer division.
  lemma {:induction false} Divide(x:bv32,y:bv32)
    requires y!=0
    ensures (x/y) as int==(x as int)/(y as int)
  {}

  lemma {:induction false} Remainder(x:bv32,y:bv32)
    requires y!=0
    ensures (x%y) as int==(x as int)%(y as int)
  {}
}

module Std.Arithmetic.IntegerBitwiseNativeDivision64 {
  // Nonzero unsigned division agrees with Euclidean integer division.
  lemma {:induction false} Divide(x:bv64,y:bv64)
    requires y!=0
    ensures (x/y) as int==(x as int)/(y as int)
  {}

  lemma {:induction false} Remainder(x:bv64,y:bv64)
    requires y!=0
    ensures (x%y) as int==(x as int)%(y as int)
  {}
}
