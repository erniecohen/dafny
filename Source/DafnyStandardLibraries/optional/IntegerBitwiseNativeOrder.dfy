// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked unsigned equality and order; requires --integer-bitwise.
// Check these bodies with --solver-option O:smt.bv.solver=2. Clients and the
// included Boolean/conversion proof closure retain the default engine.
// See docs/dev/integer-bitwise-references.md.
include "IntegerBitwiseNative.dfy"

module Std.Arithmetic.IntegerBitwiseNativeOrder8 {
  lemma {:induction false} Equal(x:bv8,y:bv8)
    ensures (x==y)==((x as int)==(y as int))
  {}

  lemma {:induction false} Less(x:bv8,y:bv8)
    ensures (x<y)==((x as int)<(y as int))
  {}

  lemma {:induction false} LessEqual(x:bv8,y:bv8)
    ensures (x<=y)==((x as int)<=(y as int))
  {}

}

module Std.Arithmetic.IntegerBitwiseNativeOrder16 {
  lemma {:induction false} Equal(x:bv16,y:bv16)
    ensures (x==y)==((x as int)==(y as int))
  {}

  lemma {:induction false} Less(x:bv16,y:bv16)
    ensures (x<y)==((x as int)<(y as int))
  {}

  lemma {:induction false} LessEqual(x:bv16,y:bv16)
    ensures (x<=y)==((x as int)<=(y as int))
  {}

}

module Std.Arithmetic.IntegerBitwiseNativeOrder32 {
  lemma {:induction false} Equal(x:bv32,y:bv32)
    ensures (x==y)==((x as int)==(y as int))
  {}

  lemma {:induction false} Less(x:bv32,y:bv32)
    ensures (x<y)==((x as int)<(y as int))
  {}

  lemma {:induction false} LessEqual(x:bv32,y:bv32)
    ensures (x<=y)==((x as int)<=(y as int))
  {}

}

module Std.Arithmetic.IntegerBitwiseNativeOrder64 {
  lemma {:induction false} Equal(x:bv64,y:bv64)
    ensures (x==y)==((x as int)==(y as int))
  {}

  lemma {:induction false} Less(x:bv64,y:bv64)
    ensures (x<y)==((x as int)<(y as int))
  {}

  lemma {:induction false} LessEqual(x:bv64,y:bv64)
    ensures (x<=y)==((x as int)<=(y as int))
  {}

}
