// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked sign-preserving right shifts. Requires --integer-bitwise.
// Every body, the shift/conversion closure and public callers are checked
// with the default bitvector engine.
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

  lemma {:induction false} NonNegative(u:int,v:int)
    requires 0<=u && 0<=v
    ensures 0<=u*v
  {}

  // A quotient by a positive divisor lies between zero and the dividend.
  lemma {:induction false} {:isolate_assertions} Shrink(a:int,d:int,q:int)
    requires 0<=a && d>0 && q==a/d
    ensures 0<=q<=a
  {
    var r:=a%d;
    assert a==q*d+r && 0<=r<d;
    if q<0 {
      NonNegative(-(q+1),d);
      assert (q+1)*d==q*d+d;
      assert false;
    }
    NonNegative(q,d-1);
    assert q*d==q+q*(d-1);
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
    var d:=R.P(k);
    if x<128 {
      L.NativeRight(x,k);
      A.Shrink(x as int,d,r as int);
      assert S.Decode(r)==r as int && S.Decode(x)==x as int;
      assert S.Decode(r)==S.Decode(x)/d;
    } else {
      var n:=!x; var q:=n>>k;
      B.NativeNot(x); L.NativeRight(n,k); B.NativeNot(q);
      assert n as int==255-x as int && q as int==n as int/d && r as int==255-q as int;
      A.Shrink(n as int,d,q as int);
      A.NegativeQuotient(n as int,d);
      assert S.Decode(x)==-(n as int)-1 && S.Decode(r)==-(q as int)-1;
      assert S.Decode(r)==S.Decode(x)/d;
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
    var d:=R.P(k);
    if x<32768 {
      L.NativeRight(x,k);
      A.Shrink(x as int,d,r as int);
      assert S.Decode(r)==r as int && S.Decode(x)==x as int;
      assert S.Decode(r)==S.Decode(x)/d;
    } else {
      var n:=!x; var q:=n>>k;
      B.NativeNot(x); L.NativeRight(n,k); B.NativeNot(q);
      assert n as int==65535-x as int && q as int==n as int/d && r as int==65535-q as int;
      A.Shrink(n as int,d,q as int);
      A.NegativeQuotient(n as int,d);
      assert S.Decode(x)==-(n as int)-1 && S.Decode(r)==-(q as int)-1;
      assert S.Decode(r)==S.Decode(x)/d;
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
    var d:=R.P(k);
    if x<2147483648 {
      L.NativeRight(x,k);
      A.Shrink(x as int,d,r as int);
      assert S.Decode(r)==r as int && S.Decode(x)==x as int;
      assert S.Decode(r)==S.Decode(x)/d;
    } else {
      var n:=!x; var q:=n>>k;
      B.NativeNot(x); L.NativeRight(n,k); B.NativeNot(q);
      assert n as int==4294967295-x as int && q as int==n as int/d && r as int==4294967295-q as int;
      A.Shrink(n as int,d,q as int);
      A.NegativeQuotient(n as int,d);
      assert S.Decode(x)==-(n as int)-1 && S.Decode(r)==-(q as int)-1;
      assert S.Decode(r)==S.Decode(x)/d;
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
    var d:=R.P(k);
    if x<9223372036854775808 {
      L.NativeRight(x,k);
      A.Shrink(x as int,d,r as int);
      assert S.Decode(r)==r as int && S.Decode(x)==x as int;
      assert S.Decode(r)==S.Decode(x)/d;
    } else {
      var n:=!x; var q:=n>>k;
      B.NativeNot(x); L.NativeRight(n,k); B.NativeNot(q);
      assert n as int==18446744073709551615-x as int && q as int==n as int/d && r as int==18446744073709551615-q as int;
      A.Shrink(n as int,d,q as int);
      A.NegativeQuotient(n as int,d);
      assert S.Decode(x)==-(n as int)-1 && S.Decode(r)==-(q as int)-1;
      assert S.Decode(r)==S.Decode(x)/d;
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
