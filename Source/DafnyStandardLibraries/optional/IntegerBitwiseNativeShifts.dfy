// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked shifts and modular addition; requires --integer-bitwise.
include "IntegerBitwiseNative.dfy"

module Std.Arithmetic.IntegerBitwiseNativeShiftArithmetic {
  import R = IntegerBitwise
  lemma {:induction false} Reduce(a:int,b:int,s:int,c:int,m:int)
    requires m>0 && 0<=s<m
    requires a+b==s+c*m
    ensures s==(a+b)%m
  { R.Quotient(a+b,m,c,s); }
  lemma {:induction false} DoubleMod(a:int,m:int)
    requires m>0
    ensures ((a%m)+(a%m))%m==(2*a)%m
  { R.ModShift(a,a%m,m); R.ModShift(a,a,m); }
  lemma {:induction false} RightArithmetic(a:int,h:int,r:int,n:nat)
    requires n>0
    requires h==a/2
    requires r==h/R.P(n-1)
    ensures r==a/R.P(n)
  { reveal R.P(); R.DivProduct(a,2,R.P(n-1)); }
  lemma {:induction false} RightZeroArithmetic(a:int,r:int,n:nat)
    requires n==0 && r==a
    ensures r==a/R.P(n)
  { R.PowerZero(); }
  lemma {:induction false} LeftArithmetic(a:int,p:int,r:int,n:nat,m:int)
    requires n>0 && m>0
    requires p==(a*R.P(n-1))%m
    requires r==(p+p)%m
    ensures r==(a*R.P(n))%m
  { DoubleMod(a*R.P(n-1),m); reveal R.P(); }
  lemma {:induction false} ZeroArithmetic(a:int,r:int,n:nat,m:int)
    requires n==0 && m>0 && r==a && 0<=a<m
    ensures r==(a*R.P(n))%m
  { R.PowerZero(); }
}

module Std.Arithmetic.IntegerBitwiseNativeShiftCount4 {
  lemma {:induction false} Add4(a:bv4,b:bv4)
   ensures ((a as bv5)+(b as bv5)) as int==a as int+b as int
  {}
  lemma {:induction false} Widen4(x:bv4)
   ensures (x as bv5) as int==x as int
  {}
  lemma {:induction false} Inverse4(x:bv4)
   ensures (x as int) as bv4==x
  {}
  lemma {:induction false} {:isolate_assertions} Next4(x:bv4)
   requires x!=15
   ensures (x+1) as int==x as int+1
  { Add4(x,1); Widen4(x+1); assert (x as bv5)+1==(x+1) as bv5; }
  lemma {:induction false} {:isolate_assertions} RoundTrip4(a:nat)
   requires a<16
   ensures (a as bv4) as int==a
   decreases a
  {
   if a>0 {
    RoundTrip4(a-1);
    var p:=(a-1) as bv4;
    Next4(p); Inverse4(p+1);
   }
  }
  lemma {:induction false} {:isolate_assertions} Previous4(k:bv4)
   requires k>0
   ensures (k-1) as int==k as int-1
  { var p:=k-1; Next4(p); calc { k as int; == (p+1) as int; == p as int+1; } }
}

module Std.Arithmetic.IntegerBitwiseNativeShifts8 {
  import B = IntegerBitwiseNative8
  import C = IntegerBitwiseNativeShiftCount4
  import A = IntegerBitwiseNativeShiftArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  // The widened sum records carry without presuming integer/BV arithmetic laws.
  lemma {:induction false} Carry(x:bv8,y:bv8)
    ensures (x as bv9)+(y as bv9) == ((x+y) as bv9) + (if x<=!y then 0 else 256)
  {}
  lemma {:induction false} CarryValue(x:bv8)
    ensures ((x as bv9)+256) as int==x as int+256
  {}
  lemma {:induction false} {:isolate_assertions} Plus(x:bv8,y:bv8)
    ensures (x+y) as int==(x as int+y as int)%256
  {
    Carry(x,y); B.Add8(x,y); B.Widen8(x+y);
    if x<=!y { A.Reduce(x as int,y as int,(x+y) as int,0,256); }
    else { CarryValue(x+y); A.Reduce(x as int,y as int,(x+y) as int,1,256); }
  }
  lemma {:induction false} PlusWitness(x:bv8,y:bv8,r:bv8)
    requires r==x+y
    ensures r as int==(x as int+y as int)%256
  { Plus(x,y); }
  // Counts use the same small native width as the existing shift rewriter.
  lemma {:induction false} CountBound(k:bv4)
    ensures (k<=8)==(k as int<=8)
  {}
  lemma {:induction false} RightStep(x:bv8,k:bv4)
    requires 0<k<=8
    ensures x>>k==(x>>1)>>(k-1)
  {}
  lemma {:induction false} LeftStep(x:bv8,k:bv4)
    requires 0<k<=8
    ensures x<<k==(x<<(k-1))+(x<<(k-1))
  {}
  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv8,k:bv4,r:bv8)
    requires k<=8
    requires r==x>>k
    ensures r as int==(x as int)/R.P(k as int)
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.RightZeroArithmetic(x as int,r as int,k as int);
    } else {
      RightStep(x,k); assert r==(x>>1)>>(k-1);
      C.Previous4(k); B.DigitSplit8(x);
      RightWitness(x>>1,k-1,r);
      A.RightArithmetic(x as int,(x>>1) as int,r as int,k as int);
    }
  }
  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv8,k:bv4,r:bv8)
    requires k<=8
    requires r==x<<k
    ensures r as int==((x as int)*R.P(k as int))%256
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.ZeroArithmetic(x as int,r as int,k as int,256);
    } else {
      LeftStep(x,k); var p:=x<<(k-1); assert r==p+p;
      C.Previous4(k); LeftWitness(x,k-1,p);
      PlusWitness(p,p,r);
      A.LeftArithmetic(x as int,p as int,r as int,k as int,256);
    }
  }
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv8,k:nat)
    requires k<=8
    ensures (x>>k) as int==(x as int)/R.P(k)
  { C.RoundTrip4(k); CountBound(k as bv4); RightWitness(x,k as bv4,x>>k); }
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv8,k:nat)
    requires k<=8
    ensures (x<<k) as int==((x as int)*R.P(k))%256
  { C.RoundTrip4(k); CountBound(k as bv4); LeftWitness(x,k as bv4,x<<k); }
  lemma {:induction false} {:isolate_assertions} LeftShift(x:bv8,k:nat)
    requires k<=8
    ensures (x<<k) as int==IntegerBitwiseModel.shlNat(8,x as int,k)
  {
    B.Power(); NativeLeft(x,k); F.LeftShift(8,x as int,k);
    R.NormId(8,x as int); reveal R.shlNat();
    if k==8 { R.MulMod(x as int,256,1); }
  }
  lemma {:induction false} {:isolate_assertions} RightShift(x:bv8,k:nat)
    requires k<=8
    ensures (x>>k) as int==IntegerBitwiseModel.shrNat(8,x as int,k)
  {
    B.Power(); NativeRight(x,k); F.RightShift(8,x as int,k);
    R.NormId(8,x as int); reveal R.shrNat();
  }
}

module Std.Arithmetic.IntegerBitwiseNativeShiftCount5 {
  lemma {:induction false} Add5(a:bv5,b:bv5)
   ensures ((a as bv6)+(b as bv6)) as int==a as int+b as int
  {}
  lemma {:induction false} Widen5(x:bv5)
   ensures (x as bv6) as int==x as int
  {}
  lemma {:induction false} Inverse5(x:bv5)
   ensures (x as int) as bv5==x
  {}
  lemma {:induction false} {:isolate_assertions} Next5(x:bv5)
   requires x!=31
   ensures (x+1) as int==x as int+1
  { Add5(x,1); Widen5(x+1); assert (x as bv6)+1==(x+1) as bv6; }
  lemma {:induction false} {:isolate_assertions} RoundTrip5(a:nat)
   requires a<32
   ensures (a as bv5) as int==a
   decreases a
  {
   if a>0 {
    RoundTrip5(a-1);
    var p:=(a-1) as bv5;
    Next5(p); Inverse5(p+1);
   }
  }
  lemma {:induction false} {:isolate_assertions} Previous5(k:bv5)
   requires k>0
   ensures (k-1) as int==k as int-1
  { var p:=k-1; Next5(p); calc { k as int; == (p+1) as int; == p as int+1; } }
}

module Std.Arithmetic.IntegerBitwiseNativeShifts16 {
  import B = IntegerBitwiseNative16
  import C = IntegerBitwiseNativeShiftCount5
  import A = IntegerBitwiseNativeShiftArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  // The widened sum records carry without presuming integer/BV arithmetic laws.
  lemma {:induction false} Carry(x:bv16,y:bv16)
    ensures (x as bv17)+(y as bv17) == ((x+y) as bv17) + (if x<=!y then 0 else 65536)
  {}
  lemma {:induction false} CarryValue(x:bv16)
    ensures ((x as bv17)+65536) as int==x as int+65536
  {}
  lemma {:induction false} {:isolate_assertions} Plus(x:bv16,y:bv16)
    ensures (x+y) as int==(x as int+y as int)%65536
  {
    Carry(x,y); B.Add16(x,y); B.Widen16(x+y);
    if x<=!y { A.Reduce(x as int,y as int,(x+y) as int,0,65536); }
    else { CarryValue(x+y); A.Reduce(x as int,y as int,(x+y) as int,1,65536); }
  }
  lemma {:induction false} PlusWitness(x:bv16,y:bv16,r:bv16)
    requires r==x+y
    ensures r as int==(x as int+y as int)%65536
  { Plus(x,y); }
  // Counts use the same small native width as the existing shift rewriter.
  lemma {:induction false} CountBound(k:bv5)
    ensures (k<=16)==(k as int<=16)
  {}
  lemma {:induction false} RightStep(x:bv16,k:bv5)
    requires 0<k<=16
    ensures x>>k==(x>>1)>>(k-1)
  {}
  lemma {:induction false} LeftStep(x:bv16,k:bv5)
    requires 0<k<=16
    ensures x<<k==(x<<(k-1))+(x<<(k-1))
  {}
  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv16,k:bv5,r:bv16)
    requires k<=16
    requires r==x>>k
    ensures r as int==(x as int)/R.P(k as int)
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.RightZeroArithmetic(x as int,r as int,k as int);
    } else {
      RightStep(x,k); assert r==(x>>1)>>(k-1);
      C.Previous5(k); B.DigitSplit16(x);
      RightWitness(x>>1,k-1,r);
      A.RightArithmetic(x as int,(x>>1) as int,r as int,k as int);
    }
  }
  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv16,k:bv5,r:bv16)
    requires k<=16
    requires r==x<<k
    ensures r as int==((x as int)*R.P(k as int))%65536
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.ZeroArithmetic(x as int,r as int,k as int,65536);
    } else {
      LeftStep(x,k); var p:=x<<(k-1); assert r==p+p;
      C.Previous5(k); LeftWitness(x,k-1,p);
      PlusWitness(p,p,r);
      A.LeftArithmetic(x as int,p as int,r as int,k as int,65536);
    }
  }
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv16,k:nat)
    requires k<=16
    ensures (x>>k) as int==(x as int)/R.P(k)
  { C.RoundTrip5(k); CountBound(k as bv5); RightWitness(x,k as bv5,x>>k); }
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv16,k:nat)
    requires k<=16
    ensures (x<<k) as int==((x as int)*R.P(k))%65536
  { C.RoundTrip5(k); CountBound(k as bv5); LeftWitness(x,k as bv5,x<<k); }
  lemma {:induction false} {:isolate_assertions} LeftShift(x:bv16,k:nat)
    requires k<=16
    ensures (x<<k) as int==IntegerBitwiseModel.shlNat(16,x as int,k)
  {
    B.Power(); NativeLeft(x,k); F.LeftShift(16,x as int,k);
    R.NormId(16,x as int); reveal R.shlNat();
    if k==16 { R.MulMod(x as int,65536,1); }
  }
  lemma {:induction false} {:isolate_assertions} RightShift(x:bv16,k:nat)
    requires k<=16
    ensures (x>>k) as int==IntegerBitwiseModel.shrNat(16,x as int,k)
  {
    B.Power(); NativeRight(x,k); F.RightShift(16,x as int,k);
    R.NormId(16,x as int); reveal R.shrNat();
  }
}

module Std.Arithmetic.IntegerBitwiseNativeShiftCount6 {
  lemma {:induction false} Add6(a:bv6,b:bv6)
   ensures ((a as bv7)+(b as bv7)) as int==a as int+b as int
  {}
  lemma {:induction false} Widen6(x:bv6)
   ensures (x as bv7) as int==x as int
  {}
  lemma {:induction false} Inverse6(x:bv6)
   ensures (x as int) as bv6==x
  {}
  lemma {:induction false} {:isolate_assertions} Next6(x:bv6)
   requires x!=63
   ensures (x+1) as int==x as int+1
  { Add6(x,1); Widen6(x+1); assert (x as bv7)+1==(x+1) as bv7; }
  lemma {:induction false} {:isolate_assertions} RoundTrip6(a:nat)
   requires a<64
   ensures (a as bv6) as int==a
   decreases a
  {
   if a>0 {
    RoundTrip6(a-1);
    var p:=(a-1) as bv6;
    Next6(p); Inverse6(p+1);
   }
  }
  lemma {:induction false} {:isolate_assertions} Previous6(k:bv6)
   requires k>0
   ensures (k-1) as int==k as int-1
  { var p:=k-1; Next6(p); calc { k as int; == (p+1) as int; == p as int+1; } }
}

module Std.Arithmetic.IntegerBitwiseNativeShifts32 {
  import B = IntegerBitwiseNative32
  import C = IntegerBitwiseNativeShiftCount6
  import A = IntegerBitwiseNativeShiftArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  // The widened sum records carry without presuming integer/BV arithmetic laws.
  lemma {:induction false} Carry(x:bv32,y:bv32)
    ensures (x as bv33)+(y as bv33) == ((x+y) as bv33) + (if x<=!y then 0 else 4294967296)
  {}
  lemma {:induction false} CarryValue(x:bv32)
    ensures ((x as bv33)+4294967296) as int==x as int+4294967296
  {}
  lemma {:induction false} {:isolate_assertions} Plus(x:bv32,y:bv32)
    ensures (x+y) as int==(x as int+y as int)%4294967296
  {
    Carry(x,y); B.Add32(x,y); B.Widen32(x+y);
    if x<=!y { A.Reduce(x as int,y as int,(x+y) as int,0,4294967296); }
    else { CarryValue(x+y); A.Reduce(x as int,y as int,(x+y) as int,1,4294967296); }
  }
  lemma {:induction false} PlusWitness(x:bv32,y:bv32,r:bv32)
    requires r==x+y
    ensures r as int==(x as int+y as int)%4294967296
  { Plus(x,y); }
  // Counts use the same small native width as the existing shift rewriter.
  lemma {:induction false} CountBound(k:bv6)
    ensures (k<=32)==(k as int<=32)
  {}
  lemma {:induction false} RightStep(x:bv32,k:bv6)
    requires 0<k<=32
    ensures x>>k==(x>>1)>>(k-1)
  {}
  lemma {:induction false} LeftStep(x:bv32,k:bv6)
    requires 0<k<=32
    ensures x<<k==(x<<(k-1))+(x<<(k-1))
  {}
  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv32,k:bv6,r:bv32)
    requires k<=32
    requires r==x>>k
    ensures r as int==(x as int)/R.P(k as int)
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.RightZeroArithmetic(x as int,r as int,k as int);
    } else {
      RightStep(x,k); assert r==(x>>1)>>(k-1);
      C.Previous6(k); B.DigitSplit32(x);
      RightWitness(x>>1,k-1,r);
      A.RightArithmetic(x as int,(x>>1) as int,r as int,k as int);
    }
  }
  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv32,k:bv6,r:bv32)
    requires k<=32
    requires r==x<<k
    ensures r as int==((x as int)*R.P(k as int))%4294967296
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.ZeroArithmetic(x as int,r as int,k as int,4294967296);
    } else {
      LeftStep(x,k); var p:=x<<(k-1); assert r==p+p;
      C.Previous6(k); LeftWitness(x,k-1,p);
      PlusWitness(p,p,r);
      A.LeftArithmetic(x as int,p as int,r as int,k as int,4294967296);
    }
  }
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv32,k:nat)
    requires k<=32
    ensures (x>>k) as int==(x as int)/R.P(k)
  { C.RoundTrip6(k); CountBound(k as bv6); RightWitness(x,k as bv6,x>>k); }
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv32,k:nat)
    requires k<=32
    ensures (x<<k) as int==((x as int)*R.P(k))%4294967296
  { C.RoundTrip6(k); CountBound(k as bv6); LeftWitness(x,k as bv6,x<<k); }
  lemma {:induction false} {:isolate_assertions} LeftShift(x:bv32,k:nat)
    requires k<=32
    ensures (x<<k) as int==IntegerBitwiseModel.shlNat(32,x as int,k)
  {
    B.Power(); NativeLeft(x,k); F.LeftShift(32,x as int,k);
    R.NormId(32,x as int); reveal R.shlNat();
    if k==32 { R.MulMod(x as int,4294967296,1); }
  }
  lemma {:induction false} {:isolate_assertions} RightShift(x:bv32,k:nat)
    requires k<=32
    ensures (x>>k) as int==IntegerBitwiseModel.shrNat(32,x as int,k)
  {
    B.Power(); NativeRight(x,k); F.RightShift(32,x as int,k);
    R.NormId(32,x as int); reveal R.shrNat();
  }
}

module Std.Arithmetic.IntegerBitwiseNativeShiftCount7 {
  lemma {:induction false} Add7(a:bv7,b:bv7)
   ensures ((a as bv8)+(b as bv8)) as int==a as int+b as int
  {}
  lemma {:induction false} Widen7(x:bv7)
   ensures (x as bv8) as int==x as int
  {}
  lemma {:induction false} Inverse7(x:bv7)
   ensures (x as int) as bv7==x
  {}
  lemma {:induction false} {:isolate_assertions} Next7(x:bv7)
   requires x!=127
   ensures (x+1) as int==x as int+1
  { Add7(x,1); Widen7(x+1); assert (x as bv8)+1==(x+1) as bv8; }
  lemma {:induction false} {:isolate_assertions} RoundTrip7(a:nat)
   requires a<128
   ensures (a as bv7) as int==a
   decreases a
  {
   if a>0 {
    RoundTrip7(a-1);
    var p:=(a-1) as bv7;
    Next7(p); Inverse7(p+1);
   }
  }
  lemma {:induction false} {:isolate_assertions} Previous7(k:bv7)
   requires k>0
   ensures (k-1) as int==k as int-1
  { var p:=k-1; Next7(p); calc { k as int; == (p+1) as int; == p as int+1; } }
}

module Std.Arithmetic.IntegerBitwiseNativeShifts64 {
  import B = IntegerBitwiseNative64
  import C = IntegerBitwiseNativeShiftCount7
  import A = IntegerBitwiseNativeShiftArithmetic
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  // The widened sum records carry without presuming integer/BV arithmetic laws.
  lemma {:induction false} Carry(x:bv64,y:bv64)
    ensures (x as bv65)+(y as bv65) == ((x+y) as bv65) + (if x<=!y then 0 else 18446744073709551616)
  {}
  lemma {:induction false} CarryValue(x:bv64)
    ensures ((x as bv65)+18446744073709551616) as int==x as int+18446744073709551616
  {}
  lemma {:induction false} {:isolate_assertions} Plus(x:bv64,y:bv64)
    ensures (x+y) as int==(x as int+y as int)%18446744073709551616
  {
    Carry(x,y); B.Add64(x,y); B.Widen64(x+y);
    if x<=!y { A.Reduce(x as int,y as int,(x+y) as int,0,18446744073709551616); }
    else { CarryValue(x+y); A.Reduce(x as int,y as int,(x+y) as int,1,18446744073709551616); }
  }
  lemma {:induction false} PlusWitness(x:bv64,y:bv64,r:bv64)
    requires r==x+y
    ensures r as int==(x as int+y as int)%18446744073709551616
  { Plus(x,y); }
  // Counts use the same small native width as the existing shift rewriter.
  lemma {:induction false} CountBound(k:bv7)
    ensures (k<=64)==(k as int<=64)
  {}
  lemma {:induction false} RightStep(x:bv64,k:bv7)
    requires 0<k<=64
    ensures x>>k==(x>>1)>>(k-1)
  {}
  lemma {:induction false} LeftStep(x:bv64,k:bv7)
    requires 0<k<=64
    ensures x<<k==(x<<(k-1))+(x<<(k-1))
  {}
  lemma {:induction false} {:isolate_assertions} RightWitness(x:bv64,k:bv7,r:bv64)
    requires k<=64
    requires r==x>>k
    ensures r as int==(x as int)/R.P(k as int)
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.RightZeroArithmetic(x as int,r as int,k as int);
    } else {
      RightStep(x,k); assert r==(x>>1)>>(k-1);
      C.Previous7(k); B.DigitSplit64(x);
      RightWitness(x>>1,k-1,r);
      A.RightArithmetic(x as int,(x>>1) as int,r as int,k as int);
    }
  }
  lemma {:induction false} {:isolate_assertions} LeftWitness(x:bv64,k:bv7,r:bv64)
    requires k<=64
    requires r==x<<k
    ensures r as int==((x as int)*R.P(k as int))%18446744073709551616
    decreases k as int
  {
    if k==0 {
      assert k as int==0; assert r==x; assert r as int==x as int;
      A.ZeroArithmetic(x as int,r as int,k as int,18446744073709551616);
    } else {
      LeftStep(x,k); var p:=x<<(k-1); assert r==p+p;
      C.Previous7(k); LeftWitness(x,k-1,p);
      PlusWitness(p,p,r);
      A.LeftArithmetic(x as int,p as int,r as int,k as int,18446744073709551616);
    }
  }
  lemma {:induction false} {:isolate_assertions} NativeRight(x:bv64,k:nat)
    requires k<=64
    ensures (x>>k) as int==(x as int)/R.P(k)
  { C.RoundTrip7(k); CountBound(k as bv7); RightWitness(x,k as bv7,x>>k); }
  lemma {:induction false} {:isolate_assertions} NativeLeft(x:bv64,k:nat)
    requires k<=64
    ensures (x<<k) as int==((x as int)*R.P(k))%18446744073709551616
  { C.RoundTrip7(k); CountBound(k as bv7); LeftWitness(x,k as bv7,x<<k); }
  lemma {:induction false} {:isolate_assertions} LeftShift(x:bv64,k:nat)
    requires k<=64
    ensures (x<<k) as int==IntegerBitwiseModel.shlNat(64,x as int,k)
  {
    B.Power(); NativeLeft(x,k); F.LeftShift(64,x as int,k);
    R.NormId(64,x as int); reveal R.shlNat();
    if k==64 { R.MulMod(x as int,18446744073709551616,1); }
  }
  lemma {:induction false} {:isolate_assertions} RightShift(x:bv64,k:nat)
    requires k<=64
    ensures (x>>k) as int==IntegerBitwiseModel.shrNat(64,x as int,k)
  {
    B.Power(); NativeRight(x,k); F.RightShift(64,x as int,k);
    R.NormId(64,x as int); reveal R.shrNat();
  }
}
