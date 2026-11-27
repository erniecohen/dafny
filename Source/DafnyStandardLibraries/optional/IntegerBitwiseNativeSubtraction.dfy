// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Explicitly included checked modular subtraction; requires --integer-bitwise.
include "IntegerBitwiseNativeShifts.dfy"

module Std.Arithmetic.IntegerBitwiseNativeSubtractionArithmetic {
  import R = IntegerBitwise
  lemma {:induction false} Reduce(a:int,b:int,p:int,r:int,m:int)
    requires m>0
    requires p==(a+m-1-b)%m
    requires r==(p+1)%m
    ensures r==(a-b)%m
  {
    R.ModShift(a+m-1-b,1,m);
    R.ModShift(a-b,m,m);
  }
}

module Std.Arithmetic.IntegerBitwiseNativeSubtraction8 {
  import B = IntegerBitwiseNative8
  import S = IntegerBitwiseNativeShifts8
  import A = IntegerBitwiseNativeSubtractionArithmetic
  lemma {:induction false} ComplementSum(x:bv8,y:bv8)
    ensures x-y==(x+!y)+1
  {}
  lemma {:induction false} {:isolate_assertions} MinusWitness(x:bv8,y:bv8,r:bv8)
    requires r==x-y
    ensures r as int==(x as int-y as int)%256
  {
    var p:=x+!y;
    ComplementSum(x,y);
    S.PlusWitness(x,!y,p);
    S.PlusWitness(p,1,r);
    B.NativeNot(y);
    A.Reduce(x as int,y as int,p as int,r as int,256);
  }
  lemma {:induction false} Minus(x:bv8,y:bv8)
    ensures (x-y) as int==(x as int-y as int)%256
  { MinusWitness(x,y,x-y); }
}

module Std.Arithmetic.IntegerBitwiseNativeSubtraction16 {
  import B = IntegerBitwiseNative16
  import S = IntegerBitwiseNativeShifts16
  import A = IntegerBitwiseNativeSubtractionArithmetic
  lemma {:induction false} ComplementSum(x:bv16,y:bv16)
    ensures x-y==(x+!y)+1
  {}
  lemma {:induction false} {:isolate_assertions} MinusWitness(x:bv16,y:bv16,r:bv16)
    requires r==x-y
    ensures r as int==(x as int-y as int)%65536
  {
    var p:=x+!y;
    ComplementSum(x,y);
    S.PlusWitness(x,!y,p);
    S.PlusWitness(p,1,r);
    B.NativeNot(y);
    A.Reduce(x as int,y as int,p as int,r as int,65536);
  }
  lemma {:induction false} Minus(x:bv16,y:bv16)
    ensures (x-y) as int==(x as int-y as int)%65536
  { MinusWitness(x,y,x-y); }
}

module Std.Arithmetic.IntegerBitwiseNativeSubtraction32 {
  import B = IntegerBitwiseNative32
  import S = IntegerBitwiseNativeShifts32
  import A = IntegerBitwiseNativeSubtractionArithmetic
  lemma {:induction false} ComplementSum(x:bv32,y:bv32)
    ensures x-y==(x+!y)+1
  {}
  lemma {:induction false} {:isolate_assertions} MinusWitness(x:bv32,y:bv32,r:bv32)
    requires r==x-y
    ensures r as int==(x as int-y as int)%4294967296
  {
    var p:=x+!y;
    ComplementSum(x,y);
    S.PlusWitness(x,!y,p);
    S.PlusWitness(p,1,r);
    B.NativeNot(y);
    A.Reduce(x as int,y as int,p as int,r as int,4294967296);
  }
  lemma {:induction false} Minus(x:bv32,y:bv32)
    ensures (x-y) as int==(x as int-y as int)%4294967296
  { MinusWitness(x,y,x-y); }
}

module Std.Arithmetic.IntegerBitwiseNativeSubtraction64 {
  import B = IntegerBitwiseNative64
  import S = IntegerBitwiseNativeShifts64
  import A = IntegerBitwiseNativeSubtractionArithmetic
  lemma {:induction false} ComplementSum(x:bv64,y:bv64)
    ensures x-y==(x+!y)+1
  {}
  lemma {:induction false} {:isolate_assertions} MinusWitness(x:bv64,y:bv64,r:bv64)
    requires r==x-y
    ensures r as int==(x as int-y as int)%18446744073709551616
  {
    var p:=x+!y;
    ComplementSum(x,y);
    S.PlusWitness(x,!y,p);
    S.PlusWitness(p,1,r);
    B.NativeNot(y);
    A.Reduce(x as int,y as int,p as int,r as int,18446744073709551616);
  }
  lemma {:induction false} Minus(x:bv64,y:bv64)
    ensures (x-y) as int==(x as int-y as int)%18446744073709551616
  { MinusWitness(x,y,x-y); }
}
