// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked single-bit extraction and tests; requires --integer-bitwise.
// All new and included bodies are checked with the default solver engine.
include "IntegerBitwiseNativeShifts.dfy"
include "IntegerBitwiseCompiler.dfy"

module Std.Arithmetic.IntegerBitwiseNativeBits8 {
  import B = IntegerBitwiseNative8
  import S = IntegerBitwiseNativeShifts8
  import R = IntegerBitwise
  import C = IntegerBitwiseCompiler

  lemma {:induction false} NativeBit(x:bv8,i:nat)
    requires i<8
    ensures ((x>>i)&1) as int==((x as int)/R.P(i))%2
  { S.NativeRight(x,i); B.DigitSplit8(x>>i); }

  lemma {:induction false} BitTest(x:bv8,i:nat)
    requires i<8
    ensures (((x>>i)&1)==1)==C.bit(8,x as int,i)
  {
    NativeBit(x,i);
    R.BitAt(8,x as int,i);
    var low:=(x>>i)&1;
    assert low==0 || low==1;
  }
}

module Std.Arithmetic.IntegerBitwiseNativeBits16 {
  import B = IntegerBitwiseNative16
  import S = IntegerBitwiseNativeShifts16
  import R = IntegerBitwise
  import C = IntegerBitwiseCompiler

  lemma {:induction false} NativeBit(x:bv16,i:nat)
    requires i<16
    ensures ((x>>i)&1) as int==((x as int)/R.P(i))%2
  { S.NativeRight(x,i); B.DigitSplit16(x>>i); }

  lemma {:induction false} BitTest(x:bv16,i:nat)
    requires i<16
    ensures (((x>>i)&1)==1)==C.bit(16,x as int,i)
  {
    NativeBit(x,i);
    R.BitAt(16,x as int,i);
    var low:=(x>>i)&1;
    assert low==0 || low==1;
  }
}

module Std.Arithmetic.IntegerBitwiseNativeBits32 {
  import B = IntegerBitwiseNative32
  import S = IntegerBitwiseNativeShifts32
  import R = IntegerBitwise
  import C = IntegerBitwiseCompiler

  lemma {:induction false} NativeBit(x:bv32,i:nat)
    requires i<32
    ensures ((x>>i)&1) as int==((x as int)/R.P(i))%2
  { S.NativeRight(x,i); B.DigitSplit32(x>>i); }

  lemma {:induction false} BitTest(x:bv32,i:nat)
    requires i<32
    ensures (((x>>i)&1)==1)==C.bit(32,x as int,i)
  {
    NativeBit(x,i);
    R.BitAt(32,x as int,i);
    var low:=(x>>i)&1;
    assert low==0 || low==1;
  }
}

module Std.Arithmetic.IntegerBitwiseNativeBits64 {
  import B = IntegerBitwiseNative64
  import S = IntegerBitwiseNativeShifts64
  import R = IntegerBitwise
  import C = IntegerBitwiseCompiler

  lemma {:induction false} NativeBit(x:bv64,i:nat)
    requires i<64
    ensures ((x>>i)&1) as int==((x as int)/R.P(i))%2
  { S.NativeRight(x,i); B.DigitSplit64(x>>i); }

  lemma {:induction false} BitTest(x:bv64,i:nat)
    requires i<64
    ensures (((x>>i)&1)==1)==C.bit(64,x as int,i)
  {
    NativeBit(x,i);
    R.BitAt(64,x as int,i);
    var low:=(x>>i)&1;
    assert low==0 || low==1;
  }
}
