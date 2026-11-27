// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked sign-preserving right shifts. Requires --integer-bitwise.
// Qualify new bodies with O:smt.bv.solver=2; the existing shift/conversion
// closure and public callers retain the default engine.
include "IntegerBitwiseNativeSigned.dfy"

module Std.Arithmetic.IntegerBitwiseSignedShiftArithmetic {
  import R=IntegerBitwise
  lemma {:induction false} NegativeQuotient(a:int,d:int)
    requires d>0
    ensures (-a-1)/d == -1-a/d
  {
    var q,r:=a/d,a%d;
    assert -a-1==(-1-q)*d+(d-1-r);
    assert 0<=d-1-r<d;
    R.Quotient(-a-1,d,-1-q,d-1-r);
  }
}

module Std.Arithmetic.IntegerBitwiseNativeSignedShifts8 {
  import B=IntegerBitwiseNative8
  import L=IntegerBitwiseNativeShifts8
  import S=IntegerBitwiseNativeSigned8
  import R=IntegerBitwise
  import A=IntegerBitwiseSignedShiftArithmetic

  lemma {:induction false} SignDecision(x:bv8)
    ensures (x<128)==(S.Decode(x)>=0)
  {}

  lemma {:induction false} {:isolate_assertions} Observe(x:bv8,k:nat,r:bv8)
    requires k<=8
    requires r==(if x<128 then x>>k else !((!x)>>k))
    ensures S.Decode(r)==S.Decode(x)/R.P(k)
  {
    SignDecision(x);
    if x<128 {
      L.NativeRight(x,k);
    } else {
      B.NativeNot(x); L.NativeRight(!x,k); B.NativeNot((!x)>>k);
      A.NegativeQuotient(255-x as int,R.P(k));
    }
  }

  opaque function Right(x:bv8,k:nat):bv8
    requires k<=8
    ensures S.Decode(Right(x,k))==S.Decode(x)/R.P(k)
  {
    var r:=if x<128 then x>>k else !((!x)>>k);
    Observe(x,k,r);
    r
  }
}

module Std.Arithmetic.IntegerBitwiseNativeSignedShifts16 {
  import B=IntegerBitwiseNative16
  import L=IntegerBitwiseNativeShifts16
  import S=IntegerBitwiseNativeSigned16
  import R=IntegerBitwise
  import A=IntegerBitwiseSignedShiftArithmetic

  lemma {:induction false} SignDecision(x:bv16)
    ensures (x<32768)==(S.Decode(x)>=0)
  {}

  lemma {:induction false} {:isolate_assertions} Observe(x:bv16,k:nat,r:bv16)
    requires k<=16
    requires r==(if x<32768 then x>>k else !((!x)>>k))
    ensures S.Decode(r)==S.Decode(x)/R.P(k)
  {
    SignDecision(x);
    if x<32768 {
      L.NativeRight(x,k);
    } else {
      B.NativeNot(x); L.NativeRight(!x,k); B.NativeNot((!x)>>k);
      A.NegativeQuotient(65535-x as int,R.P(k));
    }
  }

  opaque function Right(x:bv16,k:nat):bv16
    requires k<=16
    ensures S.Decode(Right(x,k))==S.Decode(x)/R.P(k)
  {
    var r:=if x<32768 then x>>k else !((!x)>>k);
    Observe(x,k,r);
    r
  }
}

module Std.Arithmetic.IntegerBitwiseNativeSignedShifts32 {
  import B=IntegerBitwiseNative32
  import L=IntegerBitwiseNativeShifts32
  import S=IntegerBitwiseNativeSigned32
  import R=IntegerBitwise
  import A=IntegerBitwiseSignedShiftArithmetic

  lemma {:induction false} SignDecision(x:bv32)
    ensures (x<2147483648)==(S.Decode(x)>=0)
  {}

  lemma {:induction false} {:isolate_assertions} Observe(x:bv32,k:nat,r:bv32)
    requires k<=32
    requires r==(if x<2147483648 then x>>k else !((!x)>>k))
    ensures S.Decode(r)==S.Decode(x)/R.P(k)
  {
    SignDecision(x);
    if x<2147483648 {
      L.NativeRight(x,k);
    } else {
      B.NativeNot(x); L.NativeRight(!x,k); B.NativeNot((!x)>>k);
      A.NegativeQuotient(4294967295-x as int,R.P(k));
    }
  }

  opaque function Right(x:bv32,k:nat):bv32
    requires k<=32
    ensures S.Decode(Right(x,k))==S.Decode(x)/R.P(k)
  {
    var r:=if x<2147483648 then x>>k else !((!x)>>k);
    Observe(x,k,r);
    r
  }
}

module Std.Arithmetic.IntegerBitwiseNativeSignedShifts64 {
  import B=IntegerBitwiseNative64
  import L=IntegerBitwiseNativeShifts64
  import S=IntegerBitwiseNativeSigned64
  import R=IntegerBitwise
  import A=IntegerBitwiseSignedShiftArithmetic

  lemma {:induction false} SignDecision(x:bv64)
    ensures (x<9223372036854775808)==(S.Decode(x)>=0)
  {}

  lemma {:induction false} {:isolate_assertions} Observe(x:bv64,k:nat,r:bv64)
    requires k<=64
    requires r==(if x<9223372036854775808 then x>>k else !((!x)>>k))
    ensures S.Decode(r)==S.Decode(x)/R.P(k)
  {
    SignDecision(x);
    if x<9223372036854775808 {
      L.NativeRight(x,k);
    } else {
      B.NativeNot(x); L.NativeRight(!x,k); B.NativeNot((!x)>>k);
      A.NegativeQuotient(18446744073709551615-x as int,R.P(k));
    }
  }

  opaque function Right(x:bv64,k:nat):bv64
    requires k<=64
    ensures S.Decode(Right(x,k))==S.Decode(x)/R.P(k)
  {
    var r:=if x<9223372036854775808 then x>>k else !((!x)>>k);
    Observe(x,k,r);
    r
  }
}
