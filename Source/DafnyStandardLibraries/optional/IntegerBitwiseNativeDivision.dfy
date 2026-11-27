// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked unsigned division and remainder; requires --integer-bitwise.
// The proof consumes the dividend one binary digit at a time. Native division
// remains behind opaque observations until the leading-bit recurrence is checked.
// New bodies use the default bitvector engine with O:smt.case_split=1.
// See docs/dev/integer-bitwise-references.md.
include "IntegerBitwiseNativeShifts.dfy"
module Std.Arithmetic.IntegerBitwiseNativeDivisionArithmetic {
  import R=IntegerBitwise
  lemma {:induction false} Zero(a:int,d:int,q:int,r:int)
   requires a==0 && d>0 && q==0 && r==0
   ensures q==a/d && r==a%d
  {}
  lemma {:induction false} Exact(a:int,b:int,m:int,r:int)
   requires 0<=a && 0<=b && a+b<m
   requires r==(a+b)%m
   ensures r==a+b
  {}
  lemma {:induction false} NonNegative(a:int,b:int)
   requires 0<=a && 0<=b
   ensures 0<=a*b
  {}
  lemma {:induction false} {:isolate_assertions} Bounds(a:int,d:int,q:int,r:int)
   requires 0<=a && 0<d
   requires q==a/d && r==a%d
   ensures 0<=q<=a && 0<=r<=a && r<d
  {
   assert a==q*d+r && 0<=r<d;
   if q<0 { NonNegative(-(q+1),d); assert (q+1)*d==q*d+d; assert false; }
   NonNegative(q,d-1); assert q*d==q+q*(d-1);
  }
  // Euclidean uniqueness gives the integer long-division step.
  lemma {:induction false} {:isolate_assertions} Double(a:int,d:int,q:int,r:int)
   requires 0<=a && 0<d
   requires q==(a/2)/d && r==(a/2)%d
   ensures a/d==2*q+(if 2*r+a%2>=d then 1 else 0)
   ensures a%d==(if 2*r+a%2>=d then 2*r+a%2-d else 2*r+a%2)
  {
   var t:=2*r+a%2;
   var bit:=if t>=d then 1 else 0;
   var rest:=if t>=d then t-d else t;
   assert a/2==q*d+r && 0<=r<d;
   assert a==2*(a/2)+a%2 && 0<=a%2<2;
   assert a==(2*q+bit)*d+rest;
   assert 0<=rest<d;
   R.Quotient(a,d,2*q+bit,rest);
  }
  lemma {:induction false} {:isolate_assertions} Finish(a:int,d:int,q:int,r:int,t:int,v:int,z:int)
   requires 0<=a && 0<d
   requires q==(a/2)/d && r==(a/2)%d
   requires t==2*r+a%2
   requires v==2*q+(if t>=d then 1 else 0)
   requires z==(if t>=d then t-d else t)
   ensures v==a/d && z==a%d
  { Double(a,d,q,r); }
}
module Std.Arithmetic.IntegerBitwiseNativeDivision8 {
  import B=IntegerBitwiseNative8
  import A=IntegerBitwiseNativeDivisionArithmetic
  import S=IntegerBitwiseNativeShifts8
  // Hide native division while reasoning about integer observations.
  // The zero extension makes these internal functions total; public division
  // and remainder still require a nonzero divisor.
  opaque function Q(x:bv8,y:bv8):bv8
  { if y==0 then 0 else x/y }
  opaque function R(x:bv8,y:bv8):bv8
  { if y==0 then 0 else x%y }
  lemma {:induction false} WordZero(y:bv8)
   ensures (y==0)==(y as int==0)
  { B.Inverse8(y); }
  // Witness parameters keep native expressions outside the integer reduction.
  lemma {:induction false} ExactAdd(x:bv8,y:bv8,z:bv8)
   requires x as int+y as int<256
   requires z==x+y
   ensures z as int==x as int+y as int
  { S.PlusWitness(x,y,z); A.Exact(x as int,y as int,256,z as int); }
  lemma {:induction false} DoubleNative(x:bv8)
   ensures x+x==x<<1
  {}
  lemma {:induction false} NativeSubtract(x:bv8,y:bv8)
   requires x>=y
   ensures ((x-y) as bv9)+(y as bv9)==x as bv9
  {}
  lemma {:induction false} {:isolate_assertions} Subtract(x:bv8,y:bv8,z:bv8)
   requires x>=y && z==x-y
   ensures z as int==x as int-y as int
   ensures x as int>=y as int
  { NativeSubtract(x,y); B.NoOverflow8(x-y,y); }
  lemma {:induction false} Zero(y:bv8)
   requires y!=0
   ensures Q(0,y)==0 && R(0,y)==0
  { reveal Q(),R(); }

  // Each possible leading divisor bit has a checked native recurrence.
  // The literal prefix lets the default bitvector engine simplify division.
  lemma {:induction false} {:isolate_assertions} Leading7(x:bv8,d:bv7)
  ensures Q(x,((d as bv8)|128))==(Q(x>>1,((d as bv8)|128))<<1)+(if (R(x>>1,((d as bv8)|128))<<1)+(x&1)>=((d as bv8)|128) then 1 else 0)
  ensures R(x,((d as bv8)|128))==(if ((R(x>>1,((d as bv8)|128))<<1)+(x&1))>=((d as bv8)|128) then ((R(x>>1,((d as bv8)|128))<<1)+(x&1))-((d as bv8)|128) else ((R(x>>1,((d as bv8)|128))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading6(x:bv8,d:bv6)
  ensures Q(x,((d as bv8)|64))==(Q(x>>1,((d as bv8)|64))<<1)+(if (R(x>>1,((d as bv8)|64))<<1)+(x&1)>=((d as bv8)|64) then 1 else 0)
  ensures R(x,((d as bv8)|64))==(if ((R(x>>1,((d as bv8)|64))<<1)+(x&1))>=((d as bv8)|64) then ((R(x>>1,((d as bv8)|64))<<1)+(x&1))-((d as bv8)|64) else ((R(x>>1,((d as bv8)|64))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading5(x:bv8,d:bv5)
  ensures Q(x,((d as bv8)|32))==(Q(x>>1,((d as bv8)|32))<<1)+(if (R(x>>1,((d as bv8)|32))<<1)+(x&1)>=((d as bv8)|32) then 1 else 0)
  ensures R(x,((d as bv8)|32))==(if ((R(x>>1,((d as bv8)|32))<<1)+(x&1))>=((d as bv8)|32) then ((R(x>>1,((d as bv8)|32))<<1)+(x&1))-((d as bv8)|32) else ((R(x>>1,((d as bv8)|32))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading4(x:bv8,d:bv4)
  ensures Q(x,((d as bv8)|16))==(Q(x>>1,((d as bv8)|16))<<1)+(if (R(x>>1,((d as bv8)|16))<<1)+(x&1)>=((d as bv8)|16) then 1 else 0)
  ensures R(x,((d as bv8)|16))==(if ((R(x>>1,((d as bv8)|16))<<1)+(x&1))>=((d as bv8)|16) then ((R(x>>1,((d as bv8)|16))<<1)+(x&1))-((d as bv8)|16) else ((R(x>>1,((d as bv8)|16))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading3(x:bv8,d:bv3)
  ensures Q(x,((d as bv8)|8))==(Q(x>>1,((d as bv8)|8))<<1)+(if (R(x>>1,((d as bv8)|8))<<1)+(x&1)>=((d as bv8)|8) then 1 else 0)
  ensures R(x,((d as bv8)|8))==(if ((R(x>>1,((d as bv8)|8))<<1)+(x&1))>=((d as bv8)|8) then ((R(x>>1,((d as bv8)|8))<<1)+(x&1))-((d as bv8)|8) else ((R(x>>1,((d as bv8)|8))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading2(x:bv8,d:bv2)
  ensures Q(x,((d as bv8)|4))==(Q(x>>1,((d as bv8)|4))<<1)+(if (R(x>>1,((d as bv8)|4))<<1)+(x&1)>=((d as bv8)|4) then 1 else 0)
  ensures R(x,((d as bv8)|4))==(if ((R(x>>1,((d as bv8)|4))<<1)+(x&1))>=((d as bv8)|4) then ((R(x>>1,((d as bv8)|4))<<1)+(x&1))-((d as bv8)|4) else ((R(x>>1,((d as bv8)|4))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading1(x:bv8,d:bv1)
  ensures Q(x,((d as bv8)|2))==(Q(x>>1,((d as bv8)|2))<<1)+(if (R(x>>1,((d as bv8)|2))<<1)+(x&1)>=((d as bv8)|2) then 1 else 0)
  ensures R(x,((d as bv8)|2))==(if ((R(x>>1,((d as bv8)|2))<<1)+(x&1))>=((d as bv8)|2) then ((R(x>>1,((d as bv8)|2))<<1)+(x&1))-((d as bv8)|2) else ((R(x>>1,((d as bv8)|2))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading0(x:bv8)
  ensures Q(x,(1 as bv8))==(Q(x>>1,(1 as bv8))<<1)+(if (R(x>>1,(1 as bv8))<<1)+(x&1)>=(1 as bv8) then 1 else 0)
  ensures R(x,(1 as bv8))==(if ((R(x>>1,(1 as bv8))<<1)+(x&1))>=(1 as bv8) then ((R(x>>1,(1 as bv8))<<1)+(x&1))-(1 as bv8) else ((R(x>>1,(1 as bv8))<<1)+(x&1)))
  { reveal Q(),R(); }
  // Dispatch on the highest set divisor bit. Mask before narrowing so the
  // conversion is checked even when the leading bit is set. The final dispatcher
  // has no upper-bound premise and covers every nonzero divisor.
  lemma {:induction false} {:isolate_assertions} Dispatch0(x:bv8,y:bv8)
   requires y!=0
   requires y<2
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   assert y==1;
   Leading0(x);
  }
  lemma {:induction false} {:isolate_assertions} Dispatch1(x:bv8,y:bv8)
   requires y!=0
   requires y<4
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2 {
    var d:=(y&1) as bv1;
    assert y==((d as bv8)|2);
    Leading1(x,d);
   } else {
    Dispatch0(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch2(x:bv8,y:bv8)
   requires y!=0
   requires y<8
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4 {
    var d:=(y&3) as bv2;
    assert y==((d as bv8)|4);
    Leading2(x,d);
   } else {
    Dispatch1(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch3(x:bv8,y:bv8)
   requires y!=0
   requires y<16
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8 {
    var d:=(y&7) as bv3;
    assert y==((d as bv8)|8);
    Leading3(x,d);
   } else {
    Dispatch2(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch4(x:bv8,y:bv8)
   requires y!=0
   requires y<32
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16 {
    var d:=(y&15) as bv4;
    assert y==((d as bv8)|16);
    Leading4(x,d);
   } else {
    Dispatch3(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch5(x:bv8,y:bv8)
   requires y!=0
   requires y<64
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32 {
    var d:=(y&31) as bv5;
    assert y==((d as bv8)|32);
    Leading5(x,d);
   } else {
    Dispatch4(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch6(x:bv8,y:bv8)
   requires y!=0
   requires y<128
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=64 {
    var d:=(y&63) as bv6;
    assert y==((d as bv8)|64);
    Leading6(x,d);
   } else {
    Dispatch5(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch7(x:bv8,y:bv8)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=128 {
    var d:=(y&127) as bv7;
    assert y==((d as bv8)|128);
    Leading7(x,d);
   } else {
    Dispatch6(x,y);
   }
  }
  lemma {:induction false} NativeStep(x:bv8,y:bv8)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)+Q(x>>1,y))+(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then (R(x>>1,y)+R(x>>1,y))+(x&1)-y else (R(x>>1,y)+R(x>>1,y))+(x&1))
  { Dispatch7(x,y); DoubleNative(Q(x>>1,y)); DoubleNative(R(x>>1,y));
  }
  lemma {:induction false} {:isolate_assertions} StepWitness(x:bv8,y:bv8,q:bv8,r:bv8,t:bv8,u:bv8)
   requires y!=0
   requires q==Q(x>>1,y) && r==R(x>>1,y)
   requires t==r+r+(x&1) && u==q+q
   ensures Q(x,y)==(if t>=y then u+1 else u)
   ensures R(x,y)==(if t>=y then t-y else t)
  { NativeStep(x,y); }
  // The recursive call establishes quotient and remainder for the high digits.
  // Their bounds justify exact doubling; append the low digit, then subtract
  // the divisor when needed. All recursion decreases the unsigned dividend.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv8,y:bv8)
   requires y!=0
   ensures Q(x,y) as int==(x as int)/(y as int)
   ensures R(x,y) as int==(x as int)%(y as int)
   decreases x as int
  {
   WordZero(y);
   if x==0 { Zero(y); assert Q(x,y) as int==0 && R(x,y) as int==0;
    A.Zero(x as int,y as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int); } else {
    var h,l:=x>>1,x&1;
    B.DigitSplit8(x);
    assert l==0 || l==1;
    Observe(h,y);
    var q,r:=Q(h,y),R(h,y);
    A.Bounds(h as int,y as int,q as int,r as int);
    var rr,t,u:=r+r,r+r+l,q+q;
    ExactAdd(r,r,rr); ExactAdd(rr,l,t);
    ExactAdd(q,q,u);
    StepWitness(x,y,q,r,t,u);
    if t>=y {
     assert Q(x,y)==u+1 && R(x,y)==t-y;
     Subtract(t,y,R(x,y));
     ExactAdd(u,1,Q(x,y));
     assert t as int>=y as int;
    } else {
     assert Q(x,y)==u && R(x,y)==t;
     ExactAdd(q,q,Q(x,y));
     Subtract(y,t,y-t); WordZero(y-t);
     assert y-t!=0;
     assert t as int<y as int;
    }
    assert (t>=y)==(t as int>=y as int);
    assert Q(x,y) as int==2*(q as int)+(if t>=y then 1 else 0);
    assert R(x,y) as int==(if t>=y then t as int-y as int else t as int);
    A.Finish(x as int,y as int,q as int,r as int,t as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int);
    assert R(x,y) as int==(x as int)%(y as int);
   }
  }
  lemma {:induction false} Divide(x:bv8,y:bv8)
   requires y!=0
   ensures (x/y) as int==(x as int)/(y as int)
  { Observe(x,y); reveal Q(); }
  lemma {:induction false} Remainder(x:bv8,y:bv8)
   requires y!=0
   ensures (x%y) as int==(x as int)%(y as int)
  { Observe(x,y); reveal R(); }
}
module Std.Arithmetic.IntegerBitwiseNativeDivision16 {
  import B=IntegerBitwiseNative16
  import A=IntegerBitwiseNativeDivisionArithmetic
  import S=IntegerBitwiseNativeShifts16
  // Hide native division while reasoning about integer observations.
  // The zero extension makes these internal functions total; public division
  // and remainder still require a nonzero divisor.
  opaque function Q(x:bv16,y:bv16):bv16
  { if y==0 then 0 else x/y }
  opaque function R(x:bv16,y:bv16):bv16
  { if y==0 then 0 else x%y }
  lemma {:induction false} WordZero(y:bv16)
   ensures (y==0)==(y as int==0)
  { B.Inverse16(y); }
  // Witness parameters keep native expressions outside the integer reduction.
  lemma {:induction false} ExactAdd(x:bv16,y:bv16,z:bv16)
   requires x as int+y as int<65536
   requires z==x+y
   ensures z as int==x as int+y as int
  { S.PlusWitness(x,y,z); A.Exact(x as int,y as int,65536,z as int); }
  lemma {:induction false} DoubleNative(x:bv16)
   ensures x+x==x<<1
  {}
  lemma {:induction false} NativeSubtract(x:bv16,y:bv16)
   requires x>=y
   ensures ((x-y) as bv17)+(y as bv17)==x as bv17
  {}
  lemma {:induction false} {:isolate_assertions} Subtract(x:bv16,y:bv16,z:bv16)
   requires x>=y && z==x-y
   ensures z as int==x as int-y as int
   ensures x as int>=y as int
  { NativeSubtract(x,y); B.NoOverflow16(x-y,y); }
  lemma {:induction false} Zero(y:bv16)
   requires y!=0
   ensures Q(0,y)==0 && R(0,y)==0
  { reveal Q(),R(); }

  // Each possible leading divisor bit has a checked native recurrence.
  // The literal prefix lets the default bitvector engine simplify division.
  lemma {:induction false} {:isolate_assertions} Leading15(x:bv16,d:bv15)
  ensures Q(x,((d as bv16)|32768))==(Q(x>>1,((d as bv16)|32768))<<1)+(if (R(x>>1,((d as bv16)|32768))<<1)+(x&1)>=((d as bv16)|32768) then 1 else 0)
  ensures R(x,((d as bv16)|32768))==(if ((R(x>>1,((d as bv16)|32768))<<1)+(x&1))>=((d as bv16)|32768) then ((R(x>>1,((d as bv16)|32768))<<1)+(x&1))-((d as bv16)|32768) else ((R(x>>1,((d as bv16)|32768))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading14(x:bv16,d:bv14)
  ensures Q(x,((d as bv16)|16384))==(Q(x>>1,((d as bv16)|16384))<<1)+(if (R(x>>1,((d as bv16)|16384))<<1)+(x&1)>=((d as bv16)|16384) then 1 else 0)
  ensures R(x,((d as bv16)|16384))==(if ((R(x>>1,((d as bv16)|16384))<<1)+(x&1))>=((d as bv16)|16384) then ((R(x>>1,((d as bv16)|16384))<<1)+(x&1))-((d as bv16)|16384) else ((R(x>>1,((d as bv16)|16384))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading13(x:bv16,d:bv13)
  ensures Q(x,((d as bv16)|8192))==(Q(x>>1,((d as bv16)|8192))<<1)+(if (R(x>>1,((d as bv16)|8192))<<1)+(x&1)>=((d as bv16)|8192) then 1 else 0)
  ensures R(x,((d as bv16)|8192))==(if ((R(x>>1,((d as bv16)|8192))<<1)+(x&1))>=((d as bv16)|8192) then ((R(x>>1,((d as bv16)|8192))<<1)+(x&1))-((d as bv16)|8192) else ((R(x>>1,((d as bv16)|8192))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading12(x:bv16,d:bv12)
  ensures Q(x,((d as bv16)|4096))==(Q(x>>1,((d as bv16)|4096))<<1)+(if (R(x>>1,((d as bv16)|4096))<<1)+(x&1)>=((d as bv16)|4096) then 1 else 0)
  ensures R(x,((d as bv16)|4096))==(if ((R(x>>1,((d as bv16)|4096))<<1)+(x&1))>=((d as bv16)|4096) then ((R(x>>1,((d as bv16)|4096))<<1)+(x&1))-((d as bv16)|4096) else ((R(x>>1,((d as bv16)|4096))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading11(x:bv16,d:bv11)
  ensures Q(x,((d as bv16)|2048))==(Q(x>>1,((d as bv16)|2048))<<1)+(if (R(x>>1,((d as bv16)|2048))<<1)+(x&1)>=((d as bv16)|2048) then 1 else 0)
  ensures R(x,((d as bv16)|2048))==(if ((R(x>>1,((d as bv16)|2048))<<1)+(x&1))>=((d as bv16)|2048) then ((R(x>>1,((d as bv16)|2048))<<1)+(x&1))-((d as bv16)|2048) else ((R(x>>1,((d as bv16)|2048))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading10(x:bv16,d:bv10)
  ensures Q(x,((d as bv16)|1024))==(Q(x>>1,((d as bv16)|1024))<<1)+(if (R(x>>1,((d as bv16)|1024))<<1)+(x&1)>=((d as bv16)|1024) then 1 else 0)
  ensures R(x,((d as bv16)|1024))==(if ((R(x>>1,((d as bv16)|1024))<<1)+(x&1))>=((d as bv16)|1024) then ((R(x>>1,((d as bv16)|1024))<<1)+(x&1))-((d as bv16)|1024) else ((R(x>>1,((d as bv16)|1024))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading9(x:bv16,d:bv9)
  ensures Q(x,((d as bv16)|512))==(Q(x>>1,((d as bv16)|512))<<1)+(if (R(x>>1,((d as bv16)|512))<<1)+(x&1)>=((d as bv16)|512) then 1 else 0)
  ensures R(x,((d as bv16)|512))==(if ((R(x>>1,((d as bv16)|512))<<1)+(x&1))>=((d as bv16)|512) then ((R(x>>1,((d as bv16)|512))<<1)+(x&1))-((d as bv16)|512) else ((R(x>>1,((d as bv16)|512))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading8(x:bv16,d:bv8)
  ensures Q(x,((d as bv16)|256))==(Q(x>>1,((d as bv16)|256))<<1)+(if (R(x>>1,((d as bv16)|256))<<1)+(x&1)>=((d as bv16)|256) then 1 else 0)
  ensures R(x,((d as bv16)|256))==(if ((R(x>>1,((d as bv16)|256))<<1)+(x&1))>=((d as bv16)|256) then ((R(x>>1,((d as bv16)|256))<<1)+(x&1))-((d as bv16)|256) else ((R(x>>1,((d as bv16)|256))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading7(x:bv16,d:bv7)
  ensures Q(x,((d as bv16)|128))==(Q(x>>1,((d as bv16)|128))<<1)+(if (R(x>>1,((d as bv16)|128))<<1)+(x&1)>=((d as bv16)|128) then 1 else 0)
  ensures R(x,((d as bv16)|128))==(if ((R(x>>1,((d as bv16)|128))<<1)+(x&1))>=((d as bv16)|128) then ((R(x>>1,((d as bv16)|128))<<1)+(x&1))-((d as bv16)|128) else ((R(x>>1,((d as bv16)|128))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading6(x:bv16,d:bv6)
  ensures Q(x,((d as bv16)|64))==(Q(x>>1,((d as bv16)|64))<<1)+(if (R(x>>1,((d as bv16)|64))<<1)+(x&1)>=((d as bv16)|64) then 1 else 0)
  ensures R(x,((d as bv16)|64))==(if ((R(x>>1,((d as bv16)|64))<<1)+(x&1))>=((d as bv16)|64) then ((R(x>>1,((d as bv16)|64))<<1)+(x&1))-((d as bv16)|64) else ((R(x>>1,((d as bv16)|64))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading5(x:bv16,d:bv5)
  ensures Q(x,((d as bv16)|32))==(Q(x>>1,((d as bv16)|32))<<1)+(if (R(x>>1,((d as bv16)|32))<<1)+(x&1)>=((d as bv16)|32) then 1 else 0)
  ensures R(x,((d as bv16)|32))==(if ((R(x>>1,((d as bv16)|32))<<1)+(x&1))>=((d as bv16)|32) then ((R(x>>1,((d as bv16)|32))<<1)+(x&1))-((d as bv16)|32) else ((R(x>>1,((d as bv16)|32))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading4(x:bv16,d:bv4)
  ensures Q(x,((d as bv16)|16))==(Q(x>>1,((d as bv16)|16))<<1)+(if (R(x>>1,((d as bv16)|16))<<1)+(x&1)>=((d as bv16)|16) then 1 else 0)
  ensures R(x,((d as bv16)|16))==(if ((R(x>>1,((d as bv16)|16))<<1)+(x&1))>=((d as bv16)|16) then ((R(x>>1,((d as bv16)|16))<<1)+(x&1))-((d as bv16)|16) else ((R(x>>1,((d as bv16)|16))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading3(x:bv16,d:bv3)
  ensures Q(x,((d as bv16)|8))==(Q(x>>1,((d as bv16)|8))<<1)+(if (R(x>>1,((d as bv16)|8))<<1)+(x&1)>=((d as bv16)|8) then 1 else 0)
  ensures R(x,((d as bv16)|8))==(if ((R(x>>1,((d as bv16)|8))<<1)+(x&1))>=((d as bv16)|8) then ((R(x>>1,((d as bv16)|8))<<1)+(x&1))-((d as bv16)|8) else ((R(x>>1,((d as bv16)|8))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading2(x:bv16,d:bv2)
  ensures Q(x,((d as bv16)|4))==(Q(x>>1,((d as bv16)|4))<<1)+(if (R(x>>1,((d as bv16)|4))<<1)+(x&1)>=((d as bv16)|4) then 1 else 0)
  ensures R(x,((d as bv16)|4))==(if ((R(x>>1,((d as bv16)|4))<<1)+(x&1))>=((d as bv16)|4) then ((R(x>>1,((d as bv16)|4))<<1)+(x&1))-((d as bv16)|4) else ((R(x>>1,((d as bv16)|4))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading1(x:bv16,d:bv1)
  ensures Q(x,((d as bv16)|2))==(Q(x>>1,((d as bv16)|2))<<1)+(if (R(x>>1,((d as bv16)|2))<<1)+(x&1)>=((d as bv16)|2) then 1 else 0)
  ensures R(x,((d as bv16)|2))==(if ((R(x>>1,((d as bv16)|2))<<1)+(x&1))>=((d as bv16)|2) then ((R(x>>1,((d as bv16)|2))<<1)+(x&1))-((d as bv16)|2) else ((R(x>>1,((d as bv16)|2))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading0(x:bv16)
  ensures Q(x,(1 as bv16))==(Q(x>>1,(1 as bv16))<<1)+(if (R(x>>1,(1 as bv16))<<1)+(x&1)>=(1 as bv16) then 1 else 0)
  ensures R(x,(1 as bv16))==(if ((R(x>>1,(1 as bv16))<<1)+(x&1))>=(1 as bv16) then ((R(x>>1,(1 as bv16))<<1)+(x&1))-(1 as bv16) else ((R(x>>1,(1 as bv16))<<1)+(x&1)))
  { reveal Q(),R(); }
  // Dispatch on the highest set divisor bit. Mask before narrowing so the
  // conversion is checked even when the leading bit is set. The final dispatcher
  // has no upper-bound premise and covers every nonzero divisor.
  lemma {:induction false} {:isolate_assertions} Dispatch0(x:bv16,y:bv16)
   requires y!=0
   requires y<2
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   assert y==1;
   Leading0(x);
  }
  lemma {:induction false} {:isolate_assertions} Dispatch1(x:bv16,y:bv16)
   requires y!=0
   requires y<4
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2 {
    var d:=(y&1) as bv1;
    assert y==((d as bv16)|2);
    Leading1(x,d);
   } else {
    Dispatch0(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch2(x:bv16,y:bv16)
   requires y!=0
   requires y<8
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4 {
    var d:=(y&3) as bv2;
    assert y==((d as bv16)|4);
    Leading2(x,d);
   } else {
    Dispatch1(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch3(x:bv16,y:bv16)
   requires y!=0
   requires y<16
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8 {
    var d:=(y&7) as bv3;
    assert y==((d as bv16)|8);
    Leading3(x,d);
   } else {
    Dispatch2(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch4(x:bv16,y:bv16)
   requires y!=0
   requires y<32
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16 {
    var d:=(y&15) as bv4;
    assert y==((d as bv16)|16);
    Leading4(x,d);
   } else {
    Dispatch3(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch5(x:bv16,y:bv16)
   requires y!=0
   requires y<64
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32 {
    var d:=(y&31) as bv5;
    assert y==((d as bv16)|32);
    Leading5(x,d);
   } else {
    Dispatch4(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch6(x:bv16,y:bv16)
   requires y!=0
   requires y<128
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=64 {
    var d:=(y&63) as bv6;
    assert y==((d as bv16)|64);
    Leading6(x,d);
   } else {
    Dispatch5(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch7(x:bv16,y:bv16)
   requires y!=0
   requires y<256
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=128 {
    var d:=(y&127) as bv7;
    assert y==((d as bv16)|128);
    Leading7(x,d);
   } else {
    Dispatch6(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch8(x:bv16,y:bv16)
   requires y!=0
   requires y<512
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=256 {
    var d:=(y&255) as bv8;
    assert y==((d as bv16)|256);
    Leading8(x,d);
   } else {
    Dispatch7(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch9(x:bv16,y:bv16)
   requires y!=0
   requires y<1024
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=512 {
    var d:=(y&511) as bv9;
    assert y==((d as bv16)|512);
    Leading9(x,d);
   } else {
    Dispatch8(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch10(x:bv16,y:bv16)
   requires y!=0
   requires y<2048
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1024 {
    var d:=(y&1023) as bv10;
    assert y==((d as bv16)|1024);
    Leading10(x,d);
   } else {
    Dispatch9(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch11(x:bv16,y:bv16)
   requires y!=0
   requires y<4096
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2048 {
    var d:=(y&2047) as bv11;
    assert y==((d as bv16)|2048);
    Leading11(x,d);
   } else {
    Dispatch10(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch12(x:bv16,y:bv16)
   requires y!=0
   requires y<8192
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4096 {
    var d:=(y&4095) as bv12;
    assert y==((d as bv16)|4096);
    Leading12(x,d);
   } else {
    Dispatch11(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch13(x:bv16,y:bv16)
   requires y!=0
   requires y<16384
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8192 {
    var d:=(y&8191) as bv13;
    assert y==((d as bv16)|8192);
    Leading13(x,d);
   } else {
    Dispatch12(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch14(x:bv16,y:bv16)
   requires y!=0
   requires y<32768
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16384 {
    var d:=(y&16383) as bv14;
    assert y==((d as bv16)|16384);
    Leading14(x,d);
   } else {
    Dispatch13(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch15(x:bv16,y:bv16)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32768 {
    var d:=(y&32767) as bv15;
    assert y==((d as bv16)|32768);
    Leading15(x,d);
   } else {
    Dispatch14(x,y);
   }
  }
  lemma {:induction false} NativeStep(x:bv16,y:bv16)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)+Q(x>>1,y))+(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then (R(x>>1,y)+R(x>>1,y))+(x&1)-y else (R(x>>1,y)+R(x>>1,y))+(x&1))
  { Dispatch15(x,y); DoubleNative(Q(x>>1,y)); DoubleNative(R(x>>1,y));
  }
  lemma {:induction false} {:isolate_assertions} StepWitness(x:bv16,y:bv16,q:bv16,r:bv16,t:bv16,u:bv16)
   requires y!=0
   requires q==Q(x>>1,y) && r==R(x>>1,y)
   requires t==r+r+(x&1) && u==q+q
   ensures Q(x,y)==(if t>=y then u+1 else u)
   ensures R(x,y)==(if t>=y then t-y else t)
  { NativeStep(x,y); }
  // The recursive call establishes quotient and remainder for the high digits.
  // Their bounds justify exact doubling; append the low digit, then subtract
  // the divisor when needed. All recursion decreases the unsigned dividend.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv16,y:bv16)
   requires y!=0
   ensures Q(x,y) as int==(x as int)/(y as int)
   ensures R(x,y) as int==(x as int)%(y as int)
   decreases x as int
  {
   WordZero(y);
   if x==0 { Zero(y); assert Q(x,y) as int==0 && R(x,y) as int==0;
    A.Zero(x as int,y as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int); } else {
    var h,l:=x>>1,x&1;
    B.DigitSplit16(x);
    assert l==0 || l==1;
    Observe(h,y);
    var q,r:=Q(h,y),R(h,y);
    A.Bounds(h as int,y as int,q as int,r as int);
    var rr,t,u:=r+r,r+r+l,q+q;
    ExactAdd(r,r,rr); ExactAdd(rr,l,t);
    ExactAdd(q,q,u);
    StepWitness(x,y,q,r,t,u);
    if t>=y {
     assert Q(x,y)==u+1 && R(x,y)==t-y;
     Subtract(t,y,R(x,y));
     ExactAdd(u,1,Q(x,y));
     assert t as int>=y as int;
    } else {
     assert Q(x,y)==u && R(x,y)==t;
     ExactAdd(q,q,Q(x,y));
     Subtract(y,t,y-t); WordZero(y-t);
     assert y-t!=0;
     assert t as int<y as int;
    }
    assert (t>=y)==(t as int>=y as int);
    assert Q(x,y) as int==2*(q as int)+(if t>=y then 1 else 0);
    assert R(x,y) as int==(if t>=y then t as int-y as int else t as int);
    A.Finish(x as int,y as int,q as int,r as int,t as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int);
    assert R(x,y) as int==(x as int)%(y as int);
   }
  }
  lemma {:induction false} Divide(x:bv16,y:bv16)
   requires y!=0
   ensures (x/y) as int==(x as int)/(y as int)
  { Observe(x,y); reveal Q(); }
  lemma {:induction false} Remainder(x:bv16,y:bv16)
   requires y!=0
   ensures (x%y) as int==(x as int)%(y as int)
  { Observe(x,y); reveal R(); }
}
module Std.Arithmetic.IntegerBitwiseNativeDivision32 {
  import B=IntegerBitwiseNative32
  import A=IntegerBitwiseNativeDivisionArithmetic
  import S=IntegerBitwiseNativeShifts32
  // Hide native division while reasoning about integer observations.
  // The zero extension makes these internal functions total; public division
  // and remainder still require a nonzero divisor.
  opaque function Q(x:bv32,y:bv32):bv32
  { if y==0 then 0 else x/y }
  opaque function R(x:bv32,y:bv32):bv32
  { if y==0 then 0 else x%y }
  lemma {:induction false} WordZero(y:bv32)
   ensures (y==0)==(y as int==0)
  { B.Inverse32(y); }
  // Witness parameters keep native expressions outside the integer reduction.
  lemma {:induction false} ExactAdd(x:bv32,y:bv32,z:bv32)
   requires x as int+y as int<4294967296
   requires z==x+y
   ensures z as int==x as int+y as int
  { S.PlusWitness(x,y,z); A.Exact(x as int,y as int,4294967296,z as int); }
  lemma {:induction false} DoubleNative(x:bv32)
   ensures x+x==x<<1
  {}
  lemma {:induction false} NativeSubtract(x:bv32,y:bv32)
   requires x>=y
   ensures ((x-y) as bv33)+(y as bv33)==x as bv33
  {}
  lemma {:induction false} {:isolate_assertions} Subtract(x:bv32,y:bv32,z:bv32)
   requires x>=y && z==x-y
   ensures z as int==x as int-y as int
   ensures x as int>=y as int
  { NativeSubtract(x,y); B.NoOverflow32(x-y,y); }
  lemma {:induction false} Zero(y:bv32)
   requires y!=0
   ensures Q(0,y)==0 && R(0,y)==0
  { reveal Q(),R(); }

  // Each possible leading divisor bit has a checked native recurrence.
  // The literal prefix lets the default bitvector engine simplify division.
  lemma {:induction false} {:isolate_assertions} Leading31(x:bv32,d:bv31)
  ensures Q(x,((d as bv32)|2147483648))==(Q(x>>1,((d as bv32)|2147483648))<<1)+(if (R(x>>1,((d as bv32)|2147483648))<<1)+(x&1)>=((d as bv32)|2147483648) then 1 else 0)
  ensures R(x,((d as bv32)|2147483648))==(if ((R(x>>1,((d as bv32)|2147483648))<<1)+(x&1))>=((d as bv32)|2147483648) then ((R(x>>1,((d as bv32)|2147483648))<<1)+(x&1))-((d as bv32)|2147483648) else ((R(x>>1,((d as bv32)|2147483648))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading30(x:bv32,d:bv30)
  ensures Q(x,((d as bv32)|1073741824))==(Q(x>>1,((d as bv32)|1073741824))<<1)+(if (R(x>>1,((d as bv32)|1073741824))<<1)+(x&1)>=((d as bv32)|1073741824) then 1 else 0)
  ensures R(x,((d as bv32)|1073741824))==(if ((R(x>>1,((d as bv32)|1073741824))<<1)+(x&1))>=((d as bv32)|1073741824) then ((R(x>>1,((d as bv32)|1073741824))<<1)+(x&1))-((d as bv32)|1073741824) else ((R(x>>1,((d as bv32)|1073741824))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading29(x:bv32,d:bv29)
  ensures Q(x,((d as bv32)|536870912))==(Q(x>>1,((d as bv32)|536870912))<<1)+(if (R(x>>1,((d as bv32)|536870912))<<1)+(x&1)>=((d as bv32)|536870912) then 1 else 0)
  ensures R(x,((d as bv32)|536870912))==(if ((R(x>>1,((d as bv32)|536870912))<<1)+(x&1))>=((d as bv32)|536870912) then ((R(x>>1,((d as bv32)|536870912))<<1)+(x&1))-((d as bv32)|536870912) else ((R(x>>1,((d as bv32)|536870912))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading28(x:bv32,d:bv28)
  ensures Q(x,((d as bv32)|268435456))==(Q(x>>1,((d as bv32)|268435456))<<1)+(if (R(x>>1,((d as bv32)|268435456))<<1)+(x&1)>=((d as bv32)|268435456) then 1 else 0)
  ensures R(x,((d as bv32)|268435456))==(if ((R(x>>1,((d as bv32)|268435456))<<1)+(x&1))>=((d as bv32)|268435456) then ((R(x>>1,((d as bv32)|268435456))<<1)+(x&1))-((d as bv32)|268435456) else ((R(x>>1,((d as bv32)|268435456))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading27(x:bv32,d:bv27)
  ensures Q(x,((d as bv32)|134217728))==(Q(x>>1,((d as bv32)|134217728))<<1)+(if (R(x>>1,((d as bv32)|134217728))<<1)+(x&1)>=((d as bv32)|134217728) then 1 else 0)
  ensures R(x,((d as bv32)|134217728))==(if ((R(x>>1,((d as bv32)|134217728))<<1)+(x&1))>=((d as bv32)|134217728) then ((R(x>>1,((d as bv32)|134217728))<<1)+(x&1))-((d as bv32)|134217728) else ((R(x>>1,((d as bv32)|134217728))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading26(x:bv32,d:bv26)
  ensures Q(x,((d as bv32)|67108864))==(Q(x>>1,((d as bv32)|67108864))<<1)+(if (R(x>>1,((d as bv32)|67108864))<<1)+(x&1)>=((d as bv32)|67108864) then 1 else 0)
  ensures R(x,((d as bv32)|67108864))==(if ((R(x>>1,((d as bv32)|67108864))<<1)+(x&1))>=((d as bv32)|67108864) then ((R(x>>1,((d as bv32)|67108864))<<1)+(x&1))-((d as bv32)|67108864) else ((R(x>>1,((d as bv32)|67108864))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading25(x:bv32,d:bv25)
  ensures Q(x,((d as bv32)|33554432))==(Q(x>>1,((d as bv32)|33554432))<<1)+(if (R(x>>1,((d as bv32)|33554432))<<1)+(x&1)>=((d as bv32)|33554432) then 1 else 0)
  ensures R(x,((d as bv32)|33554432))==(if ((R(x>>1,((d as bv32)|33554432))<<1)+(x&1))>=((d as bv32)|33554432) then ((R(x>>1,((d as bv32)|33554432))<<1)+(x&1))-((d as bv32)|33554432) else ((R(x>>1,((d as bv32)|33554432))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading24(x:bv32,d:bv24)
  ensures Q(x,((d as bv32)|16777216))==(Q(x>>1,((d as bv32)|16777216))<<1)+(if (R(x>>1,((d as bv32)|16777216))<<1)+(x&1)>=((d as bv32)|16777216) then 1 else 0)
  ensures R(x,((d as bv32)|16777216))==(if ((R(x>>1,((d as bv32)|16777216))<<1)+(x&1))>=((d as bv32)|16777216) then ((R(x>>1,((d as bv32)|16777216))<<1)+(x&1))-((d as bv32)|16777216) else ((R(x>>1,((d as bv32)|16777216))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading23(x:bv32,d:bv23)
  ensures Q(x,((d as bv32)|8388608))==(Q(x>>1,((d as bv32)|8388608))<<1)+(if (R(x>>1,((d as bv32)|8388608))<<1)+(x&1)>=((d as bv32)|8388608) then 1 else 0)
  ensures R(x,((d as bv32)|8388608))==(if ((R(x>>1,((d as bv32)|8388608))<<1)+(x&1))>=((d as bv32)|8388608) then ((R(x>>1,((d as bv32)|8388608))<<1)+(x&1))-((d as bv32)|8388608) else ((R(x>>1,((d as bv32)|8388608))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading22(x:bv32,d:bv22)
  ensures Q(x,((d as bv32)|4194304))==(Q(x>>1,((d as bv32)|4194304))<<1)+(if (R(x>>1,((d as bv32)|4194304))<<1)+(x&1)>=((d as bv32)|4194304) then 1 else 0)
  ensures R(x,((d as bv32)|4194304))==(if ((R(x>>1,((d as bv32)|4194304))<<1)+(x&1))>=((d as bv32)|4194304) then ((R(x>>1,((d as bv32)|4194304))<<1)+(x&1))-((d as bv32)|4194304) else ((R(x>>1,((d as bv32)|4194304))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading21(x:bv32,d:bv21)
  ensures Q(x,((d as bv32)|2097152))==(Q(x>>1,((d as bv32)|2097152))<<1)+(if (R(x>>1,((d as bv32)|2097152))<<1)+(x&1)>=((d as bv32)|2097152) then 1 else 0)
  ensures R(x,((d as bv32)|2097152))==(if ((R(x>>1,((d as bv32)|2097152))<<1)+(x&1))>=((d as bv32)|2097152) then ((R(x>>1,((d as bv32)|2097152))<<1)+(x&1))-((d as bv32)|2097152) else ((R(x>>1,((d as bv32)|2097152))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading20(x:bv32,d:bv20)
  ensures Q(x,((d as bv32)|1048576))==(Q(x>>1,((d as bv32)|1048576))<<1)+(if (R(x>>1,((d as bv32)|1048576))<<1)+(x&1)>=((d as bv32)|1048576) then 1 else 0)
  ensures R(x,((d as bv32)|1048576))==(if ((R(x>>1,((d as bv32)|1048576))<<1)+(x&1))>=((d as bv32)|1048576) then ((R(x>>1,((d as bv32)|1048576))<<1)+(x&1))-((d as bv32)|1048576) else ((R(x>>1,((d as bv32)|1048576))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading19(x:bv32,d:bv19)
  ensures Q(x,((d as bv32)|524288))==(Q(x>>1,((d as bv32)|524288))<<1)+(if (R(x>>1,((d as bv32)|524288))<<1)+(x&1)>=((d as bv32)|524288) then 1 else 0)
  ensures R(x,((d as bv32)|524288))==(if ((R(x>>1,((d as bv32)|524288))<<1)+(x&1))>=((d as bv32)|524288) then ((R(x>>1,((d as bv32)|524288))<<1)+(x&1))-((d as bv32)|524288) else ((R(x>>1,((d as bv32)|524288))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading18(x:bv32,d:bv18)
  ensures Q(x,((d as bv32)|262144))==(Q(x>>1,((d as bv32)|262144))<<1)+(if (R(x>>1,((d as bv32)|262144))<<1)+(x&1)>=((d as bv32)|262144) then 1 else 0)
  ensures R(x,((d as bv32)|262144))==(if ((R(x>>1,((d as bv32)|262144))<<1)+(x&1))>=((d as bv32)|262144) then ((R(x>>1,((d as bv32)|262144))<<1)+(x&1))-((d as bv32)|262144) else ((R(x>>1,((d as bv32)|262144))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading17(x:bv32,d:bv17)
  ensures Q(x,((d as bv32)|131072))==(Q(x>>1,((d as bv32)|131072))<<1)+(if (R(x>>1,((d as bv32)|131072))<<1)+(x&1)>=((d as bv32)|131072) then 1 else 0)
  ensures R(x,((d as bv32)|131072))==(if ((R(x>>1,((d as bv32)|131072))<<1)+(x&1))>=((d as bv32)|131072) then ((R(x>>1,((d as bv32)|131072))<<1)+(x&1))-((d as bv32)|131072) else ((R(x>>1,((d as bv32)|131072))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading16(x:bv32,d:bv16)
  ensures Q(x,((d as bv32)|65536))==(Q(x>>1,((d as bv32)|65536))<<1)+(if (R(x>>1,((d as bv32)|65536))<<1)+(x&1)>=((d as bv32)|65536) then 1 else 0)
  ensures R(x,((d as bv32)|65536))==(if ((R(x>>1,((d as bv32)|65536))<<1)+(x&1))>=((d as bv32)|65536) then ((R(x>>1,((d as bv32)|65536))<<1)+(x&1))-((d as bv32)|65536) else ((R(x>>1,((d as bv32)|65536))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading15(x:bv32,d:bv15)
  ensures Q(x,((d as bv32)|32768))==(Q(x>>1,((d as bv32)|32768))<<1)+(if (R(x>>1,((d as bv32)|32768))<<1)+(x&1)>=((d as bv32)|32768) then 1 else 0)
  ensures R(x,((d as bv32)|32768))==(if ((R(x>>1,((d as bv32)|32768))<<1)+(x&1))>=((d as bv32)|32768) then ((R(x>>1,((d as bv32)|32768))<<1)+(x&1))-((d as bv32)|32768) else ((R(x>>1,((d as bv32)|32768))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading14(x:bv32,d:bv14)
  ensures Q(x,((d as bv32)|16384))==(Q(x>>1,((d as bv32)|16384))<<1)+(if (R(x>>1,((d as bv32)|16384))<<1)+(x&1)>=((d as bv32)|16384) then 1 else 0)
  ensures R(x,((d as bv32)|16384))==(if ((R(x>>1,((d as bv32)|16384))<<1)+(x&1))>=((d as bv32)|16384) then ((R(x>>1,((d as bv32)|16384))<<1)+(x&1))-((d as bv32)|16384) else ((R(x>>1,((d as bv32)|16384))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading13(x:bv32,d:bv13)
  ensures Q(x,((d as bv32)|8192))==(Q(x>>1,((d as bv32)|8192))<<1)+(if (R(x>>1,((d as bv32)|8192))<<1)+(x&1)>=((d as bv32)|8192) then 1 else 0)
  ensures R(x,((d as bv32)|8192))==(if ((R(x>>1,((d as bv32)|8192))<<1)+(x&1))>=((d as bv32)|8192) then ((R(x>>1,((d as bv32)|8192))<<1)+(x&1))-((d as bv32)|8192) else ((R(x>>1,((d as bv32)|8192))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading12(x:bv32,d:bv12)
  ensures Q(x,((d as bv32)|4096))==(Q(x>>1,((d as bv32)|4096))<<1)+(if (R(x>>1,((d as bv32)|4096))<<1)+(x&1)>=((d as bv32)|4096) then 1 else 0)
  ensures R(x,((d as bv32)|4096))==(if ((R(x>>1,((d as bv32)|4096))<<1)+(x&1))>=((d as bv32)|4096) then ((R(x>>1,((d as bv32)|4096))<<1)+(x&1))-((d as bv32)|4096) else ((R(x>>1,((d as bv32)|4096))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading11(x:bv32,d:bv11)
  ensures Q(x,((d as bv32)|2048))==(Q(x>>1,((d as bv32)|2048))<<1)+(if (R(x>>1,((d as bv32)|2048))<<1)+(x&1)>=((d as bv32)|2048) then 1 else 0)
  ensures R(x,((d as bv32)|2048))==(if ((R(x>>1,((d as bv32)|2048))<<1)+(x&1))>=((d as bv32)|2048) then ((R(x>>1,((d as bv32)|2048))<<1)+(x&1))-((d as bv32)|2048) else ((R(x>>1,((d as bv32)|2048))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading10(x:bv32,d:bv10)
  ensures Q(x,((d as bv32)|1024))==(Q(x>>1,((d as bv32)|1024))<<1)+(if (R(x>>1,((d as bv32)|1024))<<1)+(x&1)>=((d as bv32)|1024) then 1 else 0)
  ensures R(x,((d as bv32)|1024))==(if ((R(x>>1,((d as bv32)|1024))<<1)+(x&1))>=((d as bv32)|1024) then ((R(x>>1,((d as bv32)|1024))<<1)+(x&1))-((d as bv32)|1024) else ((R(x>>1,((d as bv32)|1024))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading9(x:bv32,d:bv9)
  ensures Q(x,((d as bv32)|512))==(Q(x>>1,((d as bv32)|512))<<1)+(if (R(x>>1,((d as bv32)|512))<<1)+(x&1)>=((d as bv32)|512) then 1 else 0)
  ensures R(x,((d as bv32)|512))==(if ((R(x>>1,((d as bv32)|512))<<1)+(x&1))>=((d as bv32)|512) then ((R(x>>1,((d as bv32)|512))<<1)+(x&1))-((d as bv32)|512) else ((R(x>>1,((d as bv32)|512))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading8(x:bv32,d:bv8)
  ensures Q(x,((d as bv32)|256))==(Q(x>>1,((d as bv32)|256))<<1)+(if (R(x>>1,((d as bv32)|256))<<1)+(x&1)>=((d as bv32)|256) then 1 else 0)
  ensures R(x,((d as bv32)|256))==(if ((R(x>>1,((d as bv32)|256))<<1)+(x&1))>=((d as bv32)|256) then ((R(x>>1,((d as bv32)|256))<<1)+(x&1))-((d as bv32)|256) else ((R(x>>1,((d as bv32)|256))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading7(x:bv32,d:bv7)
  ensures Q(x,((d as bv32)|128))==(Q(x>>1,((d as bv32)|128))<<1)+(if (R(x>>1,((d as bv32)|128))<<1)+(x&1)>=((d as bv32)|128) then 1 else 0)
  ensures R(x,((d as bv32)|128))==(if ((R(x>>1,((d as bv32)|128))<<1)+(x&1))>=((d as bv32)|128) then ((R(x>>1,((d as bv32)|128))<<1)+(x&1))-((d as bv32)|128) else ((R(x>>1,((d as bv32)|128))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading6(x:bv32,d:bv6)
  ensures Q(x,((d as bv32)|64))==(Q(x>>1,((d as bv32)|64))<<1)+(if (R(x>>1,((d as bv32)|64))<<1)+(x&1)>=((d as bv32)|64) then 1 else 0)
  ensures R(x,((d as bv32)|64))==(if ((R(x>>1,((d as bv32)|64))<<1)+(x&1))>=((d as bv32)|64) then ((R(x>>1,((d as bv32)|64))<<1)+(x&1))-((d as bv32)|64) else ((R(x>>1,((d as bv32)|64))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading5(x:bv32,d:bv5)
  ensures Q(x,((d as bv32)|32))==(Q(x>>1,((d as bv32)|32))<<1)+(if (R(x>>1,((d as bv32)|32))<<1)+(x&1)>=((d as bv32)|32) then 1 else 0)
  ensures R(x,((d as bv32)|32))==(if ((R(x>>1,((d as bv32)|32))<<1)+(x&1))>=((d as bv32)|32) then ((R(x>>1,((d as bv32)|32))<<1)+(x&1))-((d as bv32)|32) else ((R(x>>1,((d as bv32)|32))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading4(x:bv32,d:bv4)
  ensures Q(x,((d as bv32)|16))==(Q(x>>1,((d as bv32)|16))<<1)+(if (R(x>>1,((d as bv32)|16))<<1)+(x&1)>=((d as bv32)|16) then 1 else 0)
  ensures R(x,((d as bv32)|16))==(if ((R(x>>1,((d as bv32)|16))<<1)+(x&1))>=((d as bv32)|16) then ((R(x>>1,((d as bv32)|16))<<1)+(x&1))-((d as bv32)|16) else ((R(x>>1,((d as bv32)|16))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading3(x:bv32,d:bv3)
  ensures Q(x,((d as bv32)|8))==(Q(x>>1,((d as bv32)|8))<<1)+(if (R(x>>1,((d as bv32)|8))<<1)+(x&1)>=((d as bv32)|8) then 1 else 0)
  ensures R(x,((d as bv32)|8))==(if ((R(x>>1,((d as bv32)|8))<<1)+(x&1))>=((d as bv32)|8) then ((R(x>>1,((d as bv32)|8))<<1)+(x&1))-((d as bv32)|8) else ((R(x>>1,((d as bv32)|8))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading2(x:bv32,d:bv2)
  ensures Q(x,((d as bv32)|4))==(Q(x>>1,((d as bv32)|4))<<1)+(if (R(x>>1,((d as bv32)|4))<<1)+(x&1)>=((d as bv32)|4) then 1 else 0)
  ensures R(x,((d as bv32)|4))==(if ((R(x>>1,((d as bv32)|4))<<1)+(x&1))>=((d as bv32)|4) then ((R(x>>1,((d as bv32)|4))<<1)+(x&1))-((d as bv32)|4) else ((R(x>>1,((d as bv32)|4))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading1(x:bv32,d:bv1)
  ensures Q(x,((d as bv32)|2))==(Q(x>>1,((d as bv32)|2))<<1)+(if (R(x>>1,((d as bv32)|2))<<1)+(x&1)>=((d as bv32)|2) then 1 else 0)
  ensures R(x,((d as bv32)|2))==(if ((R(x>>1,((d as bv32)|2))<<1)+(x&1))>=((d as bv32)|2) then ((R(x>>1,((d as bv32)|2))<<1)+(x&1))-((d as bv32)|2) else ((R(x>>1,((d as bv32)|2))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading0(x:bv32)
  ensures Q(x,(1 as bv32))==(Q(x>>1,(1 as bv32))<<1)+(if (R(x>>1,(1 as bv32))<<1)+(x&1)>=(1 as bv32) then 1 else 0)
  ensures R(x,(1 as bv32))==(if ((R(x>>1,(1 as bv32))<<1)+(x&1))>=(1 as bv32) then ((R(x>>1,(1 as bv32))<<1)+(x&1))-(1 as bv32) else ((R(x>>1,(1 as bv32))<<1)+(x&1)))
  { reveal Q(),R(); }
  // Dispatch on the highest set divisor bit. Mask before narrowing so the
  // conversion is checked even when the leading bit is set. The final dispatcher
  // has no upper-bound premise and covers every nonzero divisor.
  lemma {:induction false} {:isolate_assertions} Dispatch0(x:bv32,y:bv32)
   requires y!=0
   requires y<2
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   assert y==1;
   Leading0(x);
  }
  lemma {:induction false} {:isolate_assertions} Dispatch1(x:bv32,y:bv32)
   requires y!=0
   requires y<4
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2 {
    var d:=(y&1) as bv1;
    assert y==((d as bv32)|2);
    Leading1(x,d);
   } else {
    Dispatch0(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch2(x:bv32,y:bv32)
   requires y!=0
   requires y<8
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4 {
    var d:=(y&3) as bv2;
    assert y==((d as bv32)|4);
    Leading2(x,d);
   } else {
    Dispatch1(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch3(x:bv32,y:bv32)
   requires y!=0
   requires y<16
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8 {
    var d:=(y&7) as bv3;
    assert y==((d as bv32)|8);
    Leading3(x,d);
   } else {
    Dispatch2(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch4(x:bv32,y:bv32)
   requires y!=0
   requires y<32
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16 {
    var d:=(y&15) as bv4;
    assert y==((d as bv32)|16);
    Leading4(x,d);
   } else {
    Dispatch3(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch5(x:bv32,y:bv32)
   requires y!=0
   requires y<64
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32 {
    var d:=(y&31) as bv5;
    assert y==((d as bv32)|32);
    Leading5(x,d);
   } else {
    Dispatch4(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch6(x:bv32,y:bv32)
   requires y!=0
   requires y<128
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=64 {
    var d:=(y&63) as bv6;
    assert y==((d as bv32)|64);
    Leading6(x,d);
   } else {
    Dispatch5(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch7(x:bv32,y:bv32)
   requires y!=0
   requires y<256
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=128 {
    var d:=(y&127) as bv7;
    assert y==((d as bv32)|128);
    Leading7(x,d);
   } else {
    Dispatch6(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch8(x:bv32,y:bv32)
   requires y!=0
   requires y<512
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=256 {
    var d:=(y&255) as bv8;
    assert y==((d as bv32)|256);
    Leading8(x,d);
   } else {
    Dispatch7(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch9(x:bv32,y:bv32)
   requires y!=0
   requires y<1024
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=512 {
    var d:=(y&511) as bv9;
    assert y==((d as bv32)|512);
    Leading9(x,d);
   } else {
    Dispatch8(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch10(x:bv32,y:bv32)
   requires y!=0
   requires y<2048
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1024 {
    var d:=(y&1023) as bv10;
    assert y==((d as bv32)|1024);
    Leading10(x,d);
   } else {
    Dispatch9(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch11(x:bv32,y:bv32)
   requires y!=0
   requires y<4096
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2048 {
    var d:=(y&2047) as bv11;
    assert y==((d as bv32)|2048);
    Leading11(x,d);
   } else {
    Dispatch10(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch12(x:bv32,y:bv32)
   requires y!=0
   requires y<8192
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4096 {
    var d:=(y&4095) as bv12;
    assert y==((d as bv32)|4096);
    Leading12(x,d);
   } else {
    Dispatch11(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch13(x:bv32,y:bv32)
   requires y!=0
   requires y<16384
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8192 {
    var d:=(y&8191) as bv13;
    assert y==((d as bv32)|8192);
    Leading13(x,d);
   } else {
    Dispatch12(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch14(x:bv32,y:bv32)
   requires y!=0
   requires y<32768
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16384 {
    var d:=(y&16383) as bv14;
    assert y==((d as bv32)|16384);
    Leading14(x,d);
   } else {
    Dispatch13(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch15(x:bv32,y:bv32)
   requires y!=0
   requires y<65536
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32768 {
    var d:=(y&32767) as bv15;
    assert y==((d as bv32)|32768);
    Leading15(x,d);
   } else {
    Dispatch14(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch16(x:bv32,y:bv32)
   requires y!=0
   requires y<131072
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=65536 {
    var d:=(y&65535) as bv16;
    assert y==((d as bv32)|65536);
    Leading16(x,d);
   } else {
    Dispatch15(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch17(x:bv32,y:bv32)
   requires y!=0
   requires y<262144
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=131072 {
    var d:=(y&131071) as bv17;
    assert y==((d as bv32)|131072);
    Leading17(x,d);
   } else {
    Dispatch16(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch18(x:bv32,y:bv32)
   requires y!=0
   requires y<524288
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=262144 {
    var d:=(y&262143) as bv18;
    assert y==((d as bv32)|262144);
    Leading18(x,d);
   } else {
    Dispatch17(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch19(x:bv32,y:bv32)
   requires y!=0
   requires y<1048576
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=524288 {
    var d:=(y&524287) as bv19;
    assert y==((d as bv32)|524288);
    Leading19(x,d);
   } else {
    Dispatch18(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch20(x:bv32,y:bv32)
   requires y!=0
   requires y<2097152
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1048576 {
    var d:=(y&1048575) as bv20;
    assert y==((d as bv32)|1048576);
    Leading20(x,d);
   } else {
    Dispatch19(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch21(x:bv32,y:bv32)
   requires y!=0
   requires y<4194304
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2097152 {
    var d:=(y&2097151) as bv21;
    assert y==((d as bv32)|2097152);
    Leading21(x,d);
   } else {
    Dispatch20(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch22(x:bv32,y:bv32)
   requires y!=0
   requires y<8388608
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4194304 {
    var d:=(y&4194303) as bv22;
    assert y==((d as bv32)|4194304);
    Leading22(x,d);
   } else {
    Dispatch21(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch23(x:bv32,y:bv32)
   requires y!=0
   requires y<16777216
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8388608 {
    var d:=(y&8388607) as bv23;
    assert y==((d as bv32)|8388608);
    Leading23(x,d);
   } else {
    Dispatch22(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch24(x:bv32,y:bv32)
   requires y!=0
   requires y<33554432
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16777216 {
    var d:=(y&16777215) as bv24;
    assert y==((d as bv32)|16777216);
    Leading24(x,d);
   } else {
    Dispatch23(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch25(x:bv32,y:bv32)
   requires y!=0
   requires y<67108864
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=33554432 {
    var d:=(y&33554431) as bv25;
    assert y==((d as bv32)|33554432);
    Leading25(x,d);
   } else {
    Dispatch24(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch26(x:bv32,y:bv32)
   requires y!=0
   requires y<134217728
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=67108864 {
    var d:=(y&67108863) as bv26;
    assert y==((d as bv32)|67108864);
    Leading26(x,d);
   } else {
    Dispatch25(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch27(x:bv32,y:bv32)
   requires y!=0
   requires y<268435456
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=134217728 {
    var d:=(y&134217727) as bv27;
    assert y==((d as bv32)|134217728);
    Leading27(x,d);
   } else {
    Dispatch26(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch28(x:bv32,y:bv32)
   requires y!=0
   requires y<536870912
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=268435456 {
    var d:=(y&268435455) as bv28;
    assert y==((d as bv32)|268435456);
    Leading28(x,d);
   } else {
    Dispatch27(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch29(x:bv32,y:bv32)
   requires y!=0
   requires y<1073741824
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=536870912 {
    var d:=(y&536870911) as bv29;
    assert y==((d as bv32)|536870912);
    Leading29(x,d);
   } else {
    Dispatch28(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch30(x:bv32,y:bv32)
   requires y!=0
   requires y<2147483648
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1073741824 {
    var d:=(y&1073741823) as bv30;
    assert y==((d as bv32)|1073741824);
    Leading30(x,d);
   } else {
    Dispatch29(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch31(x:bv32,y:bv32)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2147483648 {
    var d:=(y&2147483647) as bv31;
    assert y==((d as bv32)|2147483648);
    Leading31(x,d);
   } else {
    Dispatch30(x,y);
   }
  }
  lemma {:induction false} NativeStep(x:bv32,y:bv32)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)+Q(x>>1,y))+(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then (R(x>>1,y)+R(x>>1,y))+(x&1)-y else (R(x>>1,y)+R(x>>1,y))+(x&1))
  { Dispatch31(x,y); DoubleNative(Q(x>>1,y)); DoubleNative(R(x>>1,y));
  }
  lemma {:induction false} {:isolate_assertions} StepWitness(x:bv32,y:bv32,q:bv32,r:bv32,t:bv32,u:bv32)
   requires y!=0
   requires q==Q(x>>1,y) && r==R(x>>1,y)
   requires t==r+r+(x&1) && u==q+q
   ensures Q(x,y)==(if t>=y then u+1 else u)
   ensures R(x,y)==(if t>=y then t-y else t)
  { NativeStep(x,y); }
  // The recursive call establishes quotient and remainder for the high digits.
  // Their bounds justify exact doubling; append the low digit, then subtract
  // the divisor when needed. All recursion decreases the unsigned dividend.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv32,y:bv32)
   requires y!=0
   ensures Q(x,y) as int==(x as int)/(y as int)
   ensures R(x,y) as int==(x as int)%(y as int)
   decreases x as int
  {
   WordZero(y);
   if x==0 { Zero(y); assert Q(x,y) as int==0 && R(x,y) as int==0;
    A.Zero(x as int,y as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int); } else {
    var h,l:=x>>1,x&1;
    B.DigitSplit32(x);
    assert l==0 || l==1;
    Observe(h,y);
    var q,r:=Q(h,y),R(h,y);
    A.Bounds(h as int,y as int,q as int,r as int);
    var rr,t,u:=r+r,r+r+l,q+q;
    ExactAdd(r,r,rr); ExactAdd(rr,l,t);
    ExactAdd(q,q,u);
    StepWitness(x,y,q,r,t,u);
    if t>=y {
     assert Q(x,y)==u+1 && R(x,y)==t-y;
     Subtract(t,y,R(x,y));
     ExactAdd(u,1,Q(x,y));
     assert t as int>=y as int;
    } else {
     assert Q(x,y)==u && R(x,y)==t;
     ExactAdd(q,q,Q(x,y));
     Subtract(y,t,y-t); WordZero(y-t);
     assert y-t!=0;
     assert t as int<y as int;
    }
    assert (t>=y)==(t as int>=y as int);
    assert Q(x,y) as int==2*(q as int)+(if t>=y then 1 else 0);
    assert R(x,y) as int==(if t>=y then t as int-y as int else t as int);
    A.Finish(x as int,y as int,q as int,r as int,t as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int);
    assert R(x,y) as int==(x as int)%(y as int);
   }
  }
  lemma {:induction false} Divide(x:bv32,y:bv32)
   requires y!=0
   ensures (x/y) as int==(x as int)/(y as int)
  { Observe(x,y); reveal Q(); }
  lemma {:induction false} Remainder(x:bv32,y:bv32)
   requires y!=0
   ensures (x%y) as int==(x as int)%(y as int)
  { Observe(x,y); reveal R(); }
}
module Std.Arithmetic.IntegerBitwiseNativeDivision64 {
  import B=IntegerBitwiseNative64
  import A=IntegerBitwiseNativeDivisionArithmetic
  import S=IntegerBitwiseNativeShifts64
  // Hide native division while reasoning about integer observations.
  // The zero extension makes these internal functions total; public division
  // and remainder still require a nonzero divisor.
  opaque function Q(x:bv64,y:bv64):bv64
  { if y==0 then 0 else x/y }
  opaque function R(x:bv64,y:bv64):bv64
  { if y==0 then 0 else x%y }
  lemma {:induction false} WordZero(y:bv64)
   ensures (y==0)==(y as int==0)
  { B.Inverse64(y); }
  // Witness parameters keep native expressions outside the integer reduction.
  lemma {:induction false} ExactAdd(x:bv64,y:bv64,z:bv64)
   requires x as int+y as int<18446744073709551616
   requires z==x+y
   ensures z as int==x as int+y as int
  { S.PlusWitness(x,y,z); A.Exact(x as int,y as int,18446744073709551616,z as int); }
  lemma {:induction false} DoubleNative(x:bv64)
   ensures x+x==x<<1
  {}
  lemma {:induction false} NativeSubtract(x:bv64,y:bv64)
   requires x>=y
   ensures ((x-y) as bv65)+(y as bv65)==x as bv65
  {}
  lemma {:induction false} {:isolate_assertions} Subtract(x:bv64,y:bv64,z:bv64)
   requires x>=y && z==x-y
   ensures z as int==x as int-y as int
   ensures x as int>=y as int
  { NativeSubtract(x,y); B.NoOverflow64(x-y,y); }
  lemma {:induction false} Zero(y:bv64)
   requires y!=0
   ensures Q(0,y)==0 && R(0,y)==0
  { reveal Q(),R(); }

  // Each possible leading divisor bit has a checked native recurrence.
  // The literal prefix lets the default bitvector engine simplify division.
  lemma {:induction false} {:isolate_assertions} Leading63(x:bv64,d:bv63)
  ensures Q(x,((d as bv64)|9223372036854775808))==(Q(x>>1,((d as bv64)|9223372036854775808))<<1)+(if (R(x>>1,((d as bv64)|9223372036854775808))<<1)+(x&1)>=((d as bv64)|9223372036854775808) then 1 else 0)
  ensures R(x,((d as bv64)|9223372036854775808))==(if ((R(x>>1,((d as bv64)|9223372036854775808))<<1)+(x&1))>=((d as bv64)|9223372036854775808) then ((R(x>>1,((d as bv64)|9223372036854775808))<<1)+(x&1))-((d as bv64)|9223372036854775808) else ((R(x>>1,((d as bv64)|9223372036854775808))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading62(x:bv64,d:bv62)
  ensures Q(x,((d as bv64)|4611686018427387904))==(Q(x>>1,((d as bv64)|4611686018427387904))<<1)+(if (R(x>>1,((d as bv64)|4611686018427387904))<<1)+(x&1)>=((d as bv64)|4611686018427387904) then 1 else 0)
  ensures R(x,((d as bv64)|4611686018427387904))==(if ((R(x>>1,((d as bv64)|4611686018427387904))<<1)+(x&1))>=((d as bv64)|4611686018427387904) then ((R(x>>1,((d as bv64)|4611686018427387904))<<1)+(x&1))-((d as bv64)|4611686018427387904) else ((R(x>>1,((d as bv64)|4611686018427387904))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading61(x:bv64,d:bv61)
  ensures Q(x,((d as bv64)|2305843009213693952))==(Q(x>>1,((d as bv64)|2305843009213693952))<<1)+(if (R(x>>1,((d as bv64)|2305843009213693952))<<1)+(x&1)>=((d as bv64)|2305843009213693952) then 1 else 0)
  ensures R(x,((d as bv64)|2305843009213693952))==(if ((R(x>>1,((d as bv64)|2305843009213693952))<<1)+(x&1))>=((d as bv64)|2305843009213693952) then ((R(x>>1,((d as bv64)|2305843009213693952))<<1)+(x&1))-((d as bv64)|2305843009213693952) else ((R(x>>1,((d as bv64)|2305843009213693952))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading60(x:bv64,d:bv60)
  ensures Q(x,((d as bv64)|1152921504606846976))==(Q(x>>1,((d as bv64)|1152921504606846976))<<1)+(if (R(x>>1,((d as bv64)|1152921504606846976))<<1)+(x&1)>=((d as bv64)|1152921504606846976) then 1 else 0)
  ensures R(x,((d as bv64)|1152921504606846976))==(if ((R(x>>1,((d as bv64)|1152921504606846976))<<1)+(x&1))>=((d as bv64)|1152921504606846976) then ((R(x>>1,((d as bv64)|1152921504606846976))<<1)+(x&1))-((d as bv64)|1152921504606846976) else ((R(x>>1,((d as bv64)|1152921504606846976))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading59(x:bv64,d:bv59)
  ensures Q(x,((d as bv64)|576460752303423488))==(Q(x>>1,((d as bv64)|576460752303423488))<<1)+(if (R(x>>1,((d as bv64)|576460752303423488))<<1)+(x&1)>=((d as bv64)|576460752303423488) then 1 else 0)
  ensures R(x,((d as bv64)|576460752303423488))==(if ((R(x>>1,((d as bv64)|576460752303423488))<<1)+(x&1))>=((d as bv64)|576460752303423488) then ((R(x>>1,((d as bv64)|576460752303423488))<<1)+(x&1))-((d as bv64)|576460752303423488) else ((R(x>>1,((d as bv64)|576460752303423488))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading58(x:bv64,d:bv58)
  ensures Q(x,((d as bv64)|288230376151711744))==(Q(x>>1,((d as bv64)|288230376151711744))<<1)+(if (R(x>>1,((d as bv64)|288230376151711744))<<1)+(x&1)>=((d as bv64)|288230376151711744) then 1 else 0)
  ensures R(x,((d as bv64)|288230376151711744))==(if ((R(x>>1,((d as bv64)|288230376151711744))<<1)+(x&1))>=((d as bv64)|288230376151711744) then ((R(x>>1,((d as bv64)|288230376151711744))<<1)+(x&1))-((d as bv64)|288230376151711744) else ((R(x>>1,((d as bv64)|288230376151711744))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading57(x:bv64,d:bv57)
  ensures Q(x,((d as bv64)|144115188075855872))==(Q(x>>1,((d as bv64)|144115188075855872))<<1)+(if (R(x>>1,((d as bv64)|144115188075855872))<<1)+(x&1)>=((d as bv64)|144115188075855872) then 1 else 0)
  ensures R(x,((d as bv64)|144115188075855872))==(if ((R(x>>1,((d as bv64)|144115188075855872))<<1)+(x&1))>=((d as bv64)|144115188075855872) then ((R(x>>1,((d as bv64)|144115188075855872))<<1)+(x&1))-((d as bv64)|144115188075855872) else ((R(x>>1,((d as bv64)|144115188075855872))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading56(x:bv64,d:bv56)
  ensures Q(x,((d as bv64)|72057594037927936))==(Q(x>>1,((d as bv64)|72057594037927936))<<1)+(if (R(x>>1,((d as bv64)|72057594037927936))<<1)+(x&1)>=((d as bv64)|72057594037927936) then 1 else 0)
  ensures R(x,((d as bv64)|72057594037927936))==(if ((R(x>>1,((d as bv64)|72057594037927936))<<1)+(x&1))>=((d as bv64)|72057594037927936) then ((R(x>>1,((d as bv64)|72057594037927936))<<1)+(x&1))-((d as bv64)|72057594037927936) else ((R(x>>1,((d as bv64)|72057594037927936))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading55(x:bv64,d:bv55)
  ensures Q(x,((d as bv64)|36028797018963968))==(Q(x>>1,((d as bv64)|36028797018963968))<<1)+(if (R(x>>1,((d as bv64)|36028797018963968))<<1)+(x&1)>=((d as bv64)|36028797018963968) then 1 else 0)
  ensures R(x,((d as bv64)|36028797018963968))==(if ((R(x>>1,((d as bv64)|36028797018963968))<<1)+(x&1))>=((d as bv64)|36028797018963968) then ((R(x>>1,((d as bv64)|36028797018963968))<<1)+(x&1))-((d as bv64)|36028797018963968) else ((R(x>>1,((d as bv64)|36028797018963968))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading54(x:bv64,d:bv54)
  ensures Q(x,((d as bv64)|18014398509481984))==(Q(x>>1,((d as bv64)|18014398509481984))<<1)+(if (R(x>>1,((d as bv64)|18014398509481984))<<1)+(x&1)>=((d as bv64)|18014398509481984) then 1 else 0)
  ensures R(x,((d as bv64)|18014398509481984))==(if ((R(x>>1,((d as bv64)|18014398509481984))<<1)+(x&1))>=((d as bv64)|18014398509481984) then ((R(x>>1,((d as bv64)|18014398509481984))<<1)+(x&1))-((d as bv64)|18014398509481984) else ((R(x>>1,((d as bv64)|18014398509481984))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading53(x:bv64,d:bv53)
  ensures Q(x,((d as bv64)|9007199254740992))==(Q(x>>1,((d as bv64)|9007199254740992))<<1)+(if (R(x>>1,((d as bv64)|9007199254740992))<<1)+(x&1)>=((d as bv64)|9007199254740992) then 1 else 0)
  ensures R(x,((d as bv64)|9007199254740992))==(if ((R(x>>1,((d as bv64)|9007199254740992))<<1)+(x&1))>=((d as bv64)|9007199254740992) then ((R(x>>1,((d as bv64)|9007199254740992))<<1)+(x&1))-((d as bv64)|9007199254740992) else ((R(x>>1,((d as bv64)|9007199254740992))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading52(x:bv64,d:bv52)
  ensures Q(x,((d as bv64)|4503599627370496))==(Q(x>>1,((d as bv64)|4503599627370496))<<1)+(if (R(x>>1,((d as bv64)|4503599627370496))<<1)+(x&1)>=((d as bv64)|4503599627370496) then 1 else 0)
  ensures R(x,((d as bv64)|4503599627370496))==(if ((R(x>>1,((d as bv64)|4503599627370496))<<1)+(x&1))>=((d as bv64)|4503599627370496) then ((R(x>>1,((d as bv64)|4503599627370496))<<1)+(x&1))-((d as bv64)|4503599627370496) else ((R(x>>1,((d as bv64)|4503599627370496))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading51(x:bv64,d:bv51)
  ensures Q(x,((d as bv64)|2251799813685248))==(Q(x>>1,((d as bv64)|2251799813685248))<<1)+(if (R(x>>1,((d as bv64)|2251799813685248))<<1)+(x&1)>=((d as bv64)|2251799813685248) then 1 else 0)
  ensures R(x,((d as bv64)|2251799813685248))==(if ((R(x>>1,((d as bv64)|2251799813685248))<<1)+(x&1))>=((d as bv64)|2251799813685248) then ((R(x>>1,((d as bv64)|2251799813685248))<<1)+(x&1))-((d as bv64)|2251799813685248) else ((R(x>>1,((d as bv64)|2251799813685248))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading50(x:bv64,d:bv50)
  ensures Q(x,((d as bv64)|1125899906842624))==(Q(x>>1,((d as bv64)|1125899906842624))<<1)+(if (R(x>>1,((d as bv64)|1125899906842624))<<1)+(x&1)>=((d as bv64)|1125899906842624) then 1 else 0)
  ensures R(x,((d as bv64)|1125899906842624))==(if ((R(x>>1,((d as bv64)|1125899906842624))<<1)+(x&1))>=((d as bv64)|1125899906842624) then ((R(x>>1,((d as bv64)|1125899906842624))<<1)+(x&1))-((d as bv64)|1125899906842624) else ((R(x>>1,((d as bv64)|1125899906842624))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading49(x:bv64,d:bv49)
  ensures Q(x,((d as bv64)|562949953421312))==(Q(x>>1,((d as bv64)|562949953421312))<<1)+(if (R(x>>1,((d as bv64)|562949953421312))<<1)+(x&1)>=((d as bv64)|562949953421312) then 1 else 0)
  ensures R(x,((d as bv64)|562949953421312))==(if ((R(x>>1,((d as bv64)|562949953421312))<<1)+(x&1))>=((d as bv64)|562949953421312) then ((R(x>>1,((d as bv64)|562949953421312))<<1)+(x&1))-((d as bv64)|562949953421312) else ((R(x>>1,((d as bv64)|562949953421312))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading48(x:bv64,d:bv48)
  ensures Q(x,((d as bv64)|281474976710656))==(Q(x>>1,((d as bv64)|281474976710656))<<1)+(if (R(x>>1,((d as bv64)|281474976710656))<<1)+(x&1)>=((d as bv64)|281474976710656) then 1 else 0)
  ensures R(x,((d as bv64)|281474976710656))==(if ((R(x>>1,((d as bv64)|281474976710656))<<1)+(x&1))>=((d as bv64)|281474976710656) then ((R(x>>1,((d as bv64)|281474976710656))<<1)+(x&1))-((d as bv64)|281474976710656) else ((R(x>>1,((d as bv64)|281474976710656))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading47(x:bv64,d:bv47)
  ensures Q(x,((d as bv64)|140737488355328))==(Q(x>>1,((d as bv64)|140737488355328))<<1)+(if (R(x>>1,((d as bv64)|140737488355328))<<1)+(x&1)>=((d as bv64)|140737488355328) then 1 else 0)
  ensures R(x,((d as bv64)|140737488355328))==(if ((R(x>>1,((d as bv64)|140737488355328))<<1)+(x&1))>=((d as bv64)|140737488355328) then ((R(x>>1,((d as bv64)|140737488355328))<<1)+(x&1))-((d as bv64)|140737488355328) else ((R(x>>1,((d as bv64)|140737488355328))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading46(x:bv64,d:bv46)
  ensures Q(x,((d as bv64)|70368744177664))==(Q(x>>1,((d as bv64)|70368744177664))<<1)+(if (R(x>>1,((d as bv64)|70368744177664))<<1)+(x&1)>=((d as bv64)|70368744177664) then 1 else 0)
  ensures R(x,((d as bv64)|70368744177664))==(if ((R(x>>1,((d as bv64)|70368744177664))<<1)+(x&1))>=((d as bv64)|70368744177664) then ((R(x>>1,((d as bv64)|70368744177664))<<1)+(x&1))-((d as bv64)|70368744177664) else ((R(x>>1,((d as bv64)|70368744177664))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading45(x:bv64,d:bv45)
  ensures Q(x,((d as bv64)|35184372088832))==(Q(x>>1,((d as bv64)|35184372088832))<<1)+(if (R(x>>1,((d as bv64)|35184372088832))<<1)+(x&1)>=((d as bv64)|35184372088832) then 1 else 0)
  ensures R(x,((d as bv64)|35184372088832))==(if ((R(x>>1,((d as bv64)|35184372088832))<<1)+(x&1))>=((d as bv64)|35184372088832) then ((R(x>>1,((d as bv64)|35184372088832))<<1)+(x&1))-((d as bv64)|35184372088832) else ((R(x>>1,((d as bv64)|35184372088832))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading44(x:bv64,d:bv44)
  ensures Q(x,((d as bv64)|17592186044416))==(Q(x>>1,((d as bv64)|17592186044416))<<1)+(if (R(x>>1,((d as bv64)|17592186044416))<<1)+(x&1)>=((d as bv64)|17592186044416) then 1 else 0)
  ensures R(x,((d as bv64)|17592186044416))==(if ((R(x>>1,((d as bv64)|17592186044416))<<1)+(x&1))>=((d as bv64)|17592186044416) then ((R(x>>1,((d as bv64)|17592186044416))<<1)+(x&1))-((d as bv64)|17592186044416) else ((R(x>>1,((d as bv64)|17592186044416))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading43(x:bv64,d:bv43)
  ensures Q(x,((d as bv64)|8796093022208))==(Q(x>>1,((d as bv64)|8796093022208))<<1)+(if (R(x>>1,((d as bv64)|8796093022208))<<1)+(x&1)>=((d as bv64)|8796093022208) then 1 else 0)
  ensures R(x,((d as bv64)|8796093022208))==(if ((R(x>>1,((d as bv64)|8796093022208))<<1)+(x&1))>=((d as bv64)|8796093022208) then ((R(x>>1,((d as bv64)|8796093022208))<<1)+(x&1))-((d as bv64)|8796093022208) else ((R(x>>1,((d as bv64)|8796093022208))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading42(x:bv64,d:bv42)
  ensures Q(x,((d as bv64)|4398046511104))==(Q(x>>1,((d as bv64)|4398046511104))<<1)+(if (R(x>>1,((d as bv64)|4398046511104))<<1)+(x&1)>=((d as bv64)|4398046511104) then 1 else 0)
  ensures R(x,((d as bv64)|4398046511104))==(if ((R(x>>1,((d as bv64)|4398046511104))<<1)+(x&1))>=((d as bv64)|4398046511104) then ((R(x>>1,((d as bv64)|4398046511104))<<1)+(x&1))-((d as bv64)|4398046511104) else ((R(x>>1,((d as bv64)|4398046511104))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading41(x:bv64,d:bv41)
  ensures Q(x,((d as bv64)|2199023255552))==(Q(x>>1,((d as bv64)|2199023255552))<<1)+(if (R(x>>1,((d as bv64)|2199023255552))<<1)+(x&1)>=((d as bv64)|2199023255552) then 1 else 0)
  ensures R(x,((d as bv64)|2199023255552))==(if ((R(x>>1,((d as bv64)|2199023255552))<<1)+(x&1))>=((d as bv64)|2199023255552) then ((R(x>>1,((d as bv64)|2199023255552))<<1)+(x&1))-((d as bv64)|2199023255552) else ((R(x>>1,((d as bv64)|2199023255552))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading40(x:bv64,d:bv40)
  ensures Q(x,((d as bv64)|1099511627776))==(Q(x>>1,((d as bv64)|1099511627776))<<1)+(if (R(x>>1,((d as bv64)|1099511627776))<<1)+(x&1)>=((d as bv64)|1099511627776) then 1 else 0)
  ensures R(x,((d as bv64)|1099511627776))==(if ((R(x>>1,((d as bv64)|1099511627776))<<1)+(x&1))>=((d as bv64)|1099511627776) then ((R(x>>1,((d as bv64)|1099511627776))<<1)+(x&1))-((d as bv64)|1099511627776) else ((R(x>>1,((d as bv64)|1099511627776))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading39(x:bv64,d:bv39)
  ensures Q(x,((d as bv64)|549755813888))==(Q(x>>1,((d as bv64)|549755813888))<<1)+(if (R(x>>1,((d as bv64)|549755813888))<<1)+(x&1)>=((d as bv64)|549755813888) then 1 else 0)
  ensures R(x,((d as bv64)|549755813888))==(if ((R(x>>1,((d as bv64)|549755813888))<<1)+(x&1))>=((d as bv64)|549755813888) then ((R(x>>1,((d as bv64)|549755813888))<<1)+(x&1))-((d as bv64)|549755813888) else ((R(x>>1,((d as bv64)|549755813888))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading38(x:bv64,d:bv38)
  ensures Q(x,((d as bv64)|274877906944))==(Q(x>>1,((d as bv64)|274877906944))<<1)+(if (R(x>>1,((d as bv64)|274877906944))<<1)+(x&1)>=((d as bv64)|274877906944) then 1 else 0)
  ensures R(x,((d as bv64)|274877906944))==(if ((R(x>>1,((d as bv64)|274877906944))<<1)+(x&1))>=((d as bv64)|274877906944) then ((R(x>>1,((d as bv64)|274877906944))<<1)+(x&1))-((d as bv64)|274877906944) else ((R(x>>1,((d as bv64)|274877906944))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading37(x:bv64,d:bv37)
  ensures Q(x,((d as bv64)|137438953472))==(Q(x>>1,((d as bv64)|137438953472))<<1)+(if (R(x>>1,((d as bv64)|137438953472))<<1)+(x&1)>=((d as bv64)|137438953472) then 1 else 0)
  ensures R(x,((d as bv64)|137438953472))==(if ((R(x>>1,((d as bv64)|137438953472))<<1)+(x&1))>=((d as bv64)|137438953472) then ((R(x>>1,((d as bv64)|137438953472))<<1)+(x&1))-((d as bv64)|137438953472) else ((R(x>>1,((d as bv64)|137438953472))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading36(x:bv64,d:bv36)
  ensures Q(x,((d as bv64)|68719476736))==(Q(x>>1,((d as bv64)|68719476736))<<1)+(if (R(x>>1,((d as bv64)|68719476736))<<1)+(x&1)>=((d as bv64)|68719476736) then 1 else 0)
  ensures R(x,((d as bv64)|68719476736))==(if ((R(x>>1,((d as bv64)|68719476736))<<1)+(x&1))>=((d as bv64)|68719476736) then ((R(x>>1,((d as bv64)|68719476736))<<1)+(x&1))-((d as bv64)|68719476736) else ((R(x>>1,((d as bv64)|68719476736))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading35(x:bv64,d:bv35)
  ensures Q(x,((d as bv64)|34359738368))==(Q(x>>1,((d as bv64)|34359738368))<<1)+(if (R(x>>1,((d as bv64)|34359738368))<<1)+(x&1)>=((d as bv64)|34359738368) then 1 else 0)
  ensures R(x,((d as bv64)|34359738368))==(if ((R(x>>1,((d as bv64)|34359738368))<<1)+(x&1))>=((d as bv64)|34359738368) then ((R(x>>1,((d as bv64)|34359738368))<<1)+(x&1))-((d as bv64)|34359738368) else ((R(x>>1,((d as bv64)|34359738368))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading34(x:bv64,d:bv34)
  ensures Q(x,((d as bv64)|17179869184))==(Q(x>>1,((d as bv64)|17179869184))<<1)+(if (R(x>>1,((d as bv64)|17179869184))<<1)+(x&1)>=((d as bv64)|17179869184) then 1 else 0)
  ensures R(x,((d as bv64)|17179869184))==(if ((R(x>>1,((d as bv64)|17179869184))<<1)+(x&1))>=((d as bv64)|17179869184) then ((R(x>>1,((d as bv64)|17179869184))<<1)+(x&1))-((d as bv64)|17179869184) else ((R(x>>1,((d as bv64)|17179869184))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading33(x:bv64,d:bv33)
  ensures Q(x,((d as bv64)|8589934592))==(Q(x>>1,((d as bv64)|8589934592))<<1)+(if (R(x>>1,((d as bv64)|8589934592))<<1)+(x&1)>=((d as bv64)|8589934592) then 1 else 0)
  ensures R(x,((d as bv64)|8589934592))==(if ((R(x>>1,((d as bv64)|8589934592))<<1)+(x&1))>=((d as bv64)|8589934592) then ((R(x>>1,((d as bv64)|8589934592))<<1)+(x&1))-((d as bv64)|8589934592) else ((R(x>>1,((d as bv64)|8589934592))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading32(x:bv64,d:bv32)
  ensures Q(x,((d as bv64)|4294967296))==(Q(x>>1,((d as bv64)|4294967296))<<1)+(if (R(x>>1,((d as bv64)|4294967296))<<1)+(x&1)>=((d as bv64)|4294967296) then 1 else 0)
  ensures R(x,((d as bv64)|4294967296))==(if ((R(x>>1,((d as bv64)|4294967296))<<1)+(x&1))>=((d as bv64)|4294967296) then ((R(x>>1,((d as bv64)|4294967296))<<1)+(x&1))-((d as bv64)|4294967296) else ((R(x>>1,((d as bv64)|4294967296))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading31(x:bv64,d:bv31)
  ensures Q(x,((d as bv64)|2147483648))==(Q(x>>1,((d as bv64)|2147483648))<<1)+(if (R(x>>1,((d as bv64)|2147483648))<<1)+(x&1)>=((d as bv64)|2147483648) then 1 else 0)
  ensures R(x,((d as bv64)|2147483648))==(if ((R(x>>1,((d as bv64)|2147483648))<<1)+(x&1))>=((d as bv64)|2147483648) then ((R(x>>1,((d as bv64)|2147483648))<<1)+(x&1))-((d as bv64)|2147483648) else ((R(x>>1,((d as bv64)|2147483648))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading30(x:bv64,d:bv30)
  ensures Q(x,((d as bv64)|1073741824))==(Q(x>>1,((d as bv64)|1073741824))<<1)+(if (R(x>>1,((d as bv64)|1073741824))<<1)+(x&1)>=((d as bv64)|1073741824) then 1 else 0)
  ensures R(x,((d as bv64)|1073741824))==(if ((R(x>>1,((d as bv64)|1073741824))<<1)+(x&1))>=((d as bv64)|1073741824) then ((R(x>>1,((d as bv64)|1073741824))<<1)+(x&1))-((d as bv64)|1073741824) else ((R(x>>1,((d as bv64)|1073741824))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading29(x:bv64,d:bv29)
  ensures Q(x,((d as bv64)|536870912))==(Q(x>>1,((d as bv64)|536870912))<<1)+(if (R(x>>1,((d as bv64)|536870912))<<1)+(x&1)>=((d as bv64)|536870912) then 1 else 0)
  ensures R(x,((d as bv64)|536870912))==(if ((R(x>>1,((d as bv64)|536870912))<<1)+(x&1))>=((d as bv64)|536870912) then ((R(x>>1,((d as bv64)|536870912))<<1)+(x&1))-((d as bv64)|536870912) else ((R(x>>1,((d as bv64)|536870912))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading28(x:bv64,d:bv28)
  ensures Q(x,((d as bv64)|268435456))==(Q(x>>1,((d as bv64)|268435456))<<1)+(if (R(x>>1,((d as bv64)|268435456))<<1)+(x&1)>=((d as bv64)|268435456) then 1 else 0)
  ensures R(x,((d as bv64)|268435456))==(if ((R(x>>1,((d as bv64)|268435456))<<1)+(x&1))>=((d as bv64)|268435456) then ((R(x>>1,((d as bv64)|268435456))<<1)+(x&1))-((d as bv64)|268435456) else ((R(x>>1,((d as bv64)|268435456))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading27(x:bv64,d:bv27)
  ensures Q(x,((d as bv64)|134217728))==(Q(x>>1,((d as bv64)|134217728))<<1)+(if (R(x>>1,((d as bv64)|134217728))<<1)+(x&1)>=((d as bv64)|134217728) then 1 else 0)
  ensures R(x,((d as bv64)|134217728))==(if ((R(x>>1,((d as bv64)|134217728))<<1)+(x&1))>=((d as bv64)|134217728) then ((R(x>>1,((d as bv64)|134217728))<<1)+(x&1))-((d as bv64)|134217728) else ((R(x>>1,((d as bv64)|134217728))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading26(x:bv64,d:bv26)
  ensures Q(x,((d as bv64)|67108864))==(Q(x>>1,((d as bv64)|67108864))<<1)+(if (R(x>>1,((d as bv64)|67108864))<<1)+(x&1)>=((d as bv64)|67108864) then 1 else 0)
  ensures R(x,((d as bv64)|67108864))==(if ((R(x>>1,((d as bv64)|67108864))<<1)+(x&1))>=((d as bv64)|67108864) then ((R(x>>1,((d as bv64)|67108864))<<1)+(x&1))-((d as bv64)|67108864) else ((R(x>>1,((d as bv64)|67108864))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading25(x:bv64,d:bv25)
  ensures Q(x,((d as bv64)|33554432))==(Q(x>>1,((d as bv64)|33554432))<<1)+(if (R(x>>1,((d as bv64)|33554432))<<1)+(x&1)>=((d as bv64)|33554432) then 1 else 0)
  ensures R(x,((d as bv64)|33554432))==(if ((R(x>>1,((d as bv64)|33554432))<<1)+(x&1))>=((d as bv64)|33554432) then ((R(x>>1,((d as bv64)|33554432))<<1)+(x&1))-((d as bv64)|33554432) else ((R(x>>1,((d as bv64)|33554432))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading24(x:bv64,d:bv24)
  ensures Q(x,((d as bv64)|16777216))==(Q(x>>1,((d as bv64)|16777216))<<1)+(if (R(x>>1,((d as bv64)|16777216))<<1)+(x&1)>=((d as bv64)|16777216) then 1 else 0)
  ensures R(x,((d as bv64)|16777216))==(if ((R(x>>1,((d as bv64)|16777216))<<1)+(x&1))>=((d as bv64)|16777216) then ((R(x>>1,((d as bv64)|16777216))<<1)+(x&1))-((d as bv64)|16777216) else ((R(x>>1,((d as bv64)|16777216))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading23(x:bv64,d:bv23)
  ensures Q(x,((d as bv64)|8388608))==(Q(x>>1,((d as bv64)|8388608))<<1)+(if (R(x>>1,((d as bv64)|8388608))<<1)+(x&1)>=((d as bv64)|8388608) then 1 else 0)
  ensures R(x,((d as bv64)|8388608))==(if ((R(x>>1,((d as bv64)|8388608))<<1)+(x&1))>=((d as bv64)|8388608) then ((R(x>>1,((d as bv64)|8388608))<<1)+(x&1))-((d as bv64)|8388608) else ((R(x>>1,((d as bv64)|8388608))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading22(x:bv64,d:bv22)
  ensures Q(x,((d as bv64)|4194304))==(Q(x>>1,((d as bv64)|4194304))<<1)+(if (R(x>>1,((d as bv64)|4194304))<<1)+(x&1)>=((d as bv64)|4194304) then 1 else 0)
  ensures R(x,((d as bv64)|4194304))==(if ((R(x>>1,((d as bv64)|4194304))<<1)+(x&1))>=((d as bv64)|4194304) then ((R(x>>1,((d as bv64)|4194304))<<1)+(x&1))-((d as bv64)|4194304) else ((R(x>>1,((d as bv64)|4194304))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading21(x:bv64,d:bv21)
  ensures Q(x,((d as bv64)|2097152))==(Q(x>>1,((d as bv64)|2097152))<<1)+(if (R(x>>1,((d as bv64)|2097152))<<1)+(x&1)>=((d as bv64)|2097152) then 1 else 0)
  ensures R(x,((d as bv64)|2097152))==(if ((R(x>>1,((d as bv64)|2097152))<<1)+(x&1))>=((d as bv64)|2097152) then ((R(x>>1,((d as bv64)|2097152))<<1)+(x&1))-((d as bv64)|2097152) else ((R(x>>1,((d as bv64)|2097152))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading20(x:bv64,d:bv20)
  ensures Q(x,((d as bv64)|1048576))==(Q(x>>1,((d as bv64)|1048576))<<1)+(if (R(x>>1,((d as bv64)|1048576))<<1)+(x&1)>=((d as bv64)|1048576) then 1 else 0)
  ensures R(x,((d as bv64)|1048576))==(if ((R(x>>1,((d as bv64)|1048576))<<1)+(x&1))>=((d as bv64)|1048576) then ((R(x>>1,((d as bv64)|1048576))<<1)+(x&1))-((d as bv64)|1048576) else ((R(x>>1,((d as bv64)|1048576))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading19(x:bv64,d:bv19)
  ensures Q(x,((d as bv64)|524288))==(Q(x>>1,((d as bv64)|524288))<<1)+(if (R(x>>1,((d as bv64)|524288))<<1)+(x&1)>=((d as bv64)|524288) then 1 else 0)
  ensures R(x,((d as bv64)|524288))==(if ((R(x>>1,((d as bv64)|524288))<<1)+(x&1))>=((d as bv64)|524288) then ((R(x>>1,((d as bv64)|524288))<<1)+(x&1))-((d as bv64)|524288) else ((R(x>>1,((d as bv64)|524288))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading18(x:bv64,d:bv18)
  ensures Q(x,((d as bv64)|262144))==(Q(x>>1,((d as bv64)|262144))<<1)+(if (R(x>>1,((d as bv64)|262144))<<1)+(x&1)>=((d as bv64)|262144) then 1 else 0)
  ensures R(x,((d as bv64)|262144))==(if ((R(x>>1,((d as bv64)|262144))<<1)+(x&1))>=((d as bv64)|262144) then ((R(x>>1,((d as bv64)|262144))<<1)+(x&1))-((d as bv64)|262144) else ((R(x>>1,((d as bv64)|262144))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading17(x:bv64,d:bv17)
  ensures Q(x,((d as bv64)|131072))==(Q(x>>1,((d as bv64)|131072))<<1)+(if (R(x>>1,((d as bv64)|131072))<<1)+(x&1)>=((d as bv64)|131072) then 1 else 0)
  ensures R(x,((d as bv64)|131072))==(if ((R(x>>1,((d as bv64)|131072))<<1)+(x&1))>=((d as bv64)|131072) then ((R(x>>1,((d as bv64)|131072))<<1)+(x&1))-((d as bv64)|131072) else ((R(x>>1,((d as bv64)|131072))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading16(x:bv64,d:bv16)
  ensures Q(x,((d as bv64)|65536))==(Q(x>>1,((d as bv64)|65536))<<1)+(if (R(x>>1,((d as bv64)|65536))<<1)+(x&1)>=((d as bv64)|65536) then 1 else 0)
  ensures R(x,((d as bv64)|65536))==(if ((R(x>>1,((d as bv64)|65536))<<1)+(x&1))>=((d as bv64)|65536) then ((R(x>>1,((d as bv64)|65536))<<1)+(x&1))-((d as bv64)|65536) else ((R(x>>1,((d as bv64)|65536))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading15(x:bv64,d:bv15)
  ensures Q(x,((d as bv64)|32768))==(Q(x>>1,((d as bv64)|32768))<<1)+(if (R(x>>1,((d as bv64)|32768))<<1)+(x&1)>=((d as bv64)|32768) then 1 else 0)
  ensures R(x,((d as bv64)|32768))==(if ((R(x>>1,((d as bv64)|32768))<<1)+(x&1))>=((d as bv64)|32768) then ((R(x>>1,((d as bv64)|32768))<<1)+(x&1))-((d as bv64)|32768) else ((R(x>>1,((d as bv64)|32768))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading14(x:bv64,d:bv14)
  ensures Q(x,((d as bv64)|16384))==(Q(x>>1,((d as bv64)|16384))<<1)+(if (R(x>>1,((d as bv64)|16384))<<1)+(x&1)>=((d as bv64)|16384) then 1 else 0)
  ensures R(x,((d as bv64)|16384))==(if ((R(x>>1,((d as bv64)|16384))<<1)+(x&1))>=((d as bv64)|16384) then ((R(x>>1,((d as bv64)|16384))<<1)+(x&1))-((d as bv64)|16384) else ((R(x>>1,((d as bv64)|16384))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading13(x:bv64,d:bv13)
  ensures Q(x,((d as bv64)|8192))==(Q(x>>1,((d as bv64)|8192))<<1)+(if (R(x>>1,((d as bv64)|8192))<<1)+(x&1)>=((d as bv64)|8192) then 1 else 0)
  ensures R(x,((d as bv64)|8192))==(if ((R(x>>1,((d as bv64)|8192))<<1)+(x&1))>=((d as bv64)|8192) then ((R(x>>1,((d as bv64)|8192))<<1)+(x&1))-((d as bv64)|8192) else ((R(x>>1,((d as bv64)|8192))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading12(x:bv64,d:bv12)
  ensures Q(x,((d as bv64)|4096))==(Q(x>>1,((d as bv64)|4096))<<1)+(if (R(x>>1,((d as bv64)|4096))<<1)+(x&1)>=((d as bv64)|4096) then 1 else 0)
  ensures R(x,((d as bv64)|4096))==(if ((R(x>>1,((d as bv64)|4096))<<1)+(x&1))>=((d as bv64)|4096) then ((R(x>>1,((d as bv64)|4096))<<1)+(x&1))-((d as bv64)|4096) else ((R(x>>1,((d as bv64)|4096))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading11(x:bv64,d:bv11)
  ensures Q(x,((d as bv64)|2048))==(Q(x>>1,((d as bv64)|2048))<<1)+(if (R(x>>1,((d as bv64)|2048))<<1)+(x&1)>=((d as bv64)|2048) then 1 else 0)
  ensures R(x,((d as bv64)|2048))==(if ((R(x>>1,((d as bv64)|2048))<<1)+(x&1))>=((d as bv64)|2048) then ((R(x>>1,((d as bv64)|2048))<<1)+(x&1))-((d as bv64)|2048) else ((R(x>>1,((d as bv64)|2048))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading10(x:bv64,d:bv10)
  ensures Q(x,((d as bv64)|1024))==(Q(x>>1,((d as bv64)|1024))<<1)+(if (R(x>>1,((d as bv64)|1024))<<1)+(x&1)>=((d as bv64)|1024) then 1 else 0)
  ensures R(x,((d as bv64)|1024))==(if ((R(x>>1,((d as bv64)|1024))<<1)+(x&1))>=((d as bv64)|1024) then ((R(x>>1,((d as bv64)|1024))<<1)+(x&1))-((d as bv64)|1024) else ((R(x>>1,((d as bv64)|1024))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading9(x:bv64,d:bv9)
  ensures Q(x,((d as bv64)|512))==(Q(x>>1,((d as bv64)|512))<<1)+(if (R(x>>1,((d as bv64)|512))<<1)+(x&1)>=((d as bv64)|512) then 1 else 0)
  ensures R(x,((d as bv64)|512))==(if ((R(x>>1,((d as bv64)|512))<<1)+(x&1))>=((d as bv64)|512) then ((R(x>>1,((d as bv64)|512))<<1)+(x&1))-((d as bv64)|512) else ((R(x>>1,((d as bv64)|512))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading8(x:bv64,d:bv8)
  ensures Q(x,((d as bv64)|256))==(Q(x>>1,((d as bv64)|256))<<1)+(if (R(x>>1,((d as bv64)|256))<<1)+(x&1)>=((d as bv64)|256) then 1 else 0)
  ensures R(x,((d as bv64)|256))==(if ((R(x>>1,((d as bv64)|256))<<1)+(x&1))>=((d as bv64)|256) then ((R(x>>1,((d as bv64)|256))<<1)+(x&1))-((d as bv64)|256) else ((R(x>>1,((d as bv64)|256))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading7(x:bv64,d:bv7)
  ensures Q(x,((d as bv64)|128))==(Q(x>>1,((d as bv64)|128))<<1)+(if (R(x>>1,((d as bv64)|128))<<1)+(x&1)>=((d as bv64)|128) then 1 else 0)
  ensures R(x,((d as bv64)|128))==(if ((R(x>>1,((d as bv64)|128))<<1)+(x&1))>=((d as bv64)|128) then ((R(x>>1,((d as bv64)|128))<<1)+(x&1))-((d as bv64)|128) else ((R(x>>1,((d as bv64)|128))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading6(x:bv64,d:bv6)
  ensures Q(x,((d as bv64)|64))==(Q(x>>1,((d as bv64)|64))<<1)+(if (R(x>>1,((d as bv64)|64))<<1)+(x&1)>=((d as bv64)|64) then 1 else 0)
  ensures R(x,((d as bv64)|64))==(if ((R(x>>1,((d as bv64)|64))<<1)+(x&1))>=((d as bv64)|64) then ((R(x>>1,((d as bv64)|64))<<1)+(x&1))-((d as bv64)|64) else ((R(x>>1,((d as bv64)|64))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading5(x:bv64,d:bv5)
  ensures Q(x,((d as bv64)|32))==(Q(x>>1,((d as bv64)|32))<<1)+(if (R(x>>1,((d as bv64)|32))<<1)+(x&1)>=((d as bv64)|32) then 1 else 0)
  ensures R(x,((d as bv64)|32))==(if ((R(x>>1,((d as bv64)|32))<<1)+(x&1))>=((d as bv64)|32) then ((R(x>>1,((d as bv64)|32))<<1)+(x&1))-((d as bv64)|32) else ((R(x>>1,((d as bv64)|32))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading4(x:bv64,d:bv4)
  ensures Q(x,((d as bv64)|16))==(Q(x>>1,((d as bv64)|16))<<1)+(if (R(x>>1,((d as bv64)|16))<<1)+(x&1)>=((d as bv64)|16) then 1 else 0)
  ensures R(x,((d as bv64)|16))==(if ((R(x>>1,((d as bv64)|16))<<1)+(x&1))>=((d as bv64)|16) then ((R(x>>1,((d as bv64)|16))<<1)+(x&1))-((d as bv64)|16) else ((R(x>>1,((d as bv64)|16))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading3(x:bv64,d:bv3)
  ensures Q(x,((d as bv64)|8))==(Q(x>>1,((d as bv64)|8))<<1)+(if (R(x>>1,((d as bv64)|8))<<1)+(x&1)>=((d as bv64)|8) then 1 else 0)
  ensures R(x,((d as bv64)|8))==(if ((R(x>>1,((d as bv64)|8))<<1)+(x&1))>=((d as bv64)|8) then ((R(x>>1,((d as bv64)|8))<<1)+(x&1))-((d as bv64)|8) else ((R(x>>1,((d as bv64)|8))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading2(x:bv64,d:bv2)
  ensures Q(x,((d as bv64)|4))==(Q(x>>1,((d as bv64)|4))<<1)+(if (R(x>>1,((d as bv64)|4))<<1)+(x&1)>=((d as bv64)|4) then 1 else 0)
  ensures R(x,((d as bv64)|4))==(if ((R(x>>1,((d as bv64)|4))<<1)+(x&1))>=((d as bv64)|4) then ((R(x>>1,((d as bv64)|4))<<1)+(x&1))-((d as bv64)|4) else ((R(x>>1,((d as bv64)|4))<<1)+(x&1)))
  { reveal Q(),R(); }
  lemma {:induction false} {:isolate_assertions} Leading1(x:bv64,d:bv1)
  ensures Q(x,((d as bv64)|2))==(Q(x>>1,((d as bv64)|2))<<1)+(if (R(x>>1,((d as bv64)|2))<<1)+(x&1)>=((d as bv64)|2) then 1 else 0)
  ensures R(x,((d as bv64)|2))==(if ((R(x>>1,((d as bv64)|2))<<1)+(x&1))>=((d as bv64)|2) then ((R(x>>1,((d as bv64)|2))<<1)+(x&1))-((d as bv64)|2) else ((R(x>>1,((d as bv64)|2))<<1)+(x&1)))
  { if d==0 { reveal Q(),R(); } else { assert d==1; reveal Q(),R(); } }
  lemma {:induction false} {:isolate_assertions} Leading0(x:bv64)
  ensures Q(x,(1 as bv64))==(Q(x>>1,(1 as bv64))<<1)+(if (R(x>>1,(1 as bv64))<<1)+(x&1)>=(1 as bv64) then 1 else 0)
  ensures R(x,(1 as bv64))==(if ((R(x>>1,(1 as bv64))<<1)+(x&1))>=(1 as bv64) then ((R(x>>1,(1 as bv64))<<1)+(x&1))-(1 as bv64) else ((R(x>>1,(1 as bv64))<<1)+(x&1)))
  { reveal Q(),R(); }
  // Dispatch on the highest set divisor bit. Mask before narrowing so the
  // conversion is checked even when the leading bit is set. The final dispatcher
  // has no upper-bound premise and covers every nonzero divisor.
  lemma {:induction false} {:isolate_assertions} Dispatch0(x:bv64,y:bv64)
   requires y!=0
   requires y<2
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   assert y==1;
   Leading0(x);
  }
  lemma {:induction false} {:isolate_assertions} Dispatch1(x:bv64,y:bv64)
   requires y!=0
   requires y<4
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2 {
    var d:=(y&1) as bv1;
    assert y==((d as bv64)|2);
    Leading1(x,d);
   } else {
    Dispatch0(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch2(x:bv64,y:bv64)
   requires y!=0
   requires y<8
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4 {
    var d:=(y&3) as bv2;
    assert y==((d as bv64)|4);
    Leading2(x,d);
   } else {
    Dispatch1(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch3(x:bv64,y:bv64)
   requires y!=0
   requires y<16
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8 {
    var d:=(y&7) as bv3;
    assert y==((d as bv64)|8);
    Leading3(x,d);
   } else {
    Dispatch2(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch4(x:bv64,y:bv64)
   requires y!=0
   requires y<32
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16 {
    var d:=(y&15) as bv4;
    assert y==((d as bv64)|16);
    Leading4(x,d);
   } else {
    Dispatch3(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch5(x:bv64,y:bv64)
   requires y!=0
   requires y<64
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32 {
    var d:=(y&31) as bv5;
    assert y==((d as bv64)|32);
    Leading5(x,d);
   } else {
    Dispatch4(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch6(x:bv64,y:bv64)
   requires y!=0
   requires y<128
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=64 {
    var d:=(y&63) as bv6;
    assert y==((d as bv64)|64);
    Leading6(x,d);
   } else {
    Dispatch5(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch7(x:bv64,y:bv64)
   requires y!=0
   requires y<256
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=128 {
    var d:=(y&127) as bv7;
    assert y==((d as bv64)|128);
    Leading7(x,d);
   } else {
    Dispatch6(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch8(x:bv64,y:bv64)
   requires y!=0
   requires y<512
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=256 {
    var d:=(y&255) as bv8;
    assert y==((d as bv64)|256);
    Leading8(x,d);
   } else {
    Dispatch7(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch9(x:bv64,y:bv64)
   requires y!=0
   requires y<1024
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=512 {
    var d:=(y&511) as bv9;
    assert y==((d as bv64)|512);
    Leading9(x,d);
   } else {
    Dispatch8(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch10(x:bv64,y:bv64)
   requires y!=0
   requires y<2048
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1024 {
    var d:=(y&1023) as bv10;
    assert y==((d as bv64)|1024);
    Leading10(x,d);
   } else {
    Dispatch9(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch11(x:bv64,y:bv64)
   requires y!=0
   requires y<4096
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2048 {
    var d:=(y&2047) as bv11;
    assert y==((d as bv64)|2048);
    Leading11(x,d);
   } else {
    Dispatch10(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch12(x:bv64,y:bv64)
   requires y!=0
   requires y<8192
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4096 {
    var d:=(y&4095) as bv12;
    assert y==((d as bv64)|4096);
    Leading12(x,d);
   } else {
    Dispatch11(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch13(x:bv64,y:bv64)
   requires y!=0
   requires y<16384
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8192 {
    var d:=(y&8191) as bv13;
    assert y==((d as bv64)|8192);
    Leading13(x,d);
   } else {
    Dispatch12(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch14(x:bv64,y:bv64)
   requires y!=0
   requires y<32768
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16384 {
    var d:=(y&16383) as bv14;
    assert y==((d as bv64)|16384);
    Leading14(x,d);
   } else {
    Dispatch13(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch15(x:bv64,y:bv64)
   requires y!=0
   requires y<65536
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=32768 {
    var d:=(y&32767) as bv15;
    assert y==((d as bv64)|32768);
    Leading15(x,d);
   } else {
    Dispatch14(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch16(x:bv64,y:bv64)
   requires y!=0
   requires y<131072
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=65536 {
    var d:=(y&65535) as bv16;
    assert y==((d as bv64)|65536);
    Leading16(x,d);
   } else {
    Dispatch15(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch17(x:bv64,y:bv64)
   requires y!=0
   requires y<262144
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=131072 {
    var d:=(y&131071) as bv17;
    assert y==((d as bv64)|131072);
    Leading17(x,d);
   } else {
    Dispatch16(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch18(x:bv64,y:bv64)
   requires y!=0
   requires y<524288
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=262144 {
    var d:=(y&262143) as bv18;
    assert y==((d as bv64)|262144);
    Leading18(x,d);
   } else {
    Dispatch17(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch19(x:bv64,y:bv64)
   requires y!=0
   requires y<1048576
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=524288 {
    var d:=(y&524287) as bv19;
    assert y==((d as bv64)|524288);
    Leading19(x,d);
   } else {
    Dispatch18(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch20(x:bv64,y:bv64)
   requires y!=0
   requires y<2097152
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1048576 {
    var d:=(y&1048575) as bv20;
    assert y==((d as bv64)|1048576);
    Leading20(x,d);
   } else {
    Dispatch19(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch21(x:bv64,y:bv64)
   requires y!=0
   requires y<4194304
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2097152 {
    var d:=(y&2097151) as bv21;
    assert y==((d as bv64)|2097152);
    Leading21(x,d);
   } else {
    Dispatch20(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch22(x:bv64,y:bv64)
   requires y!=0
   requires y<8388608
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4194304 {
    var d:=(y&4194303) as bv22;
    assert y==((d as bv64)|4194304);
    Leading22(x,d);
   } else {
    Dispatch21(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch23(x:bv64,y:bv64)
   requires y!=0
   requires y<16777216
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8388608 {
    var d:=(y&8388607) as bv23;
    assert y==((d as bv64)|8388608);
    Leading23(x,d);
   } else {
    Dispatch22(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch24(x:bv64,y:bv64)
   requires y!=0
   requires y<33554432
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=16777216 {
    var d:=(y&16777215) as bv24;
    assert y==((d as bv64)|16777216);
    Leading24(x,d);
   } else {
    Dispatch23(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch25(x:bv64,y:bv64)
   requires y!=0
   requires y<67108864
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=33554432 {
    var d:=(y&33554431) as bv25;
    assert y==((d as bv64)|33554432);
    Leading25(x,d);
   } else {
    Dispatch24(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch26(x:bv64,y:bv64)
   requires y!=0
   requires y<134217728
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=67108864 {
    var d:=(y&67108863) as bv26;
    assert y==((d as bv64)|67108864);
    Leading26(x,d);
   } else {
    Dispatch25(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch27(x:bv64,y:bv64)
   requires y!=0
   requires y<268435456
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=134217728 {
    var d:=(y&134217727) as bv27;
    assert y==((d as bv64)|134217728);
    Leading27(x,d);
   } else {
    Dispatch26(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch28(x:bv64,y:bv64)
   requires y!=0
   requires y<536870912
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=268435456 {
    var d:=(y&268435455) as bv28;
    assert y==((d as bv64)|268435456);
    Leading28(x,d);
   } else {
    Dispatch27(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch29(x:bv64,y:bv64)
   requires y!=0
   requires y<1073741824
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=536870912 {
    var d:=(y&536870911) as bv29;
    assert y==((d as bv64)|536870912);
    Leading29(x,d);
   } else {
    Dispatch28(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch30(x:bv64,y:bv64)
   requires y!=0
   requires y<2147483648
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1073741824 {
    var d:=(y&1073741823) as bv30;
    assert y==((d as bv64)|1073741824);
    Leading30(x,d);
   } else {
    Dispatch29(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch31(x:bv64,y:bv64)
   requires y!=0
   requires y<4294967296
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2147483648 {
    var d:=(y&2147483647) as bv31;
    assert y==((d as bv64)|2147483648);
    Leading31(x,d);
   } else {
    Dispatch30(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch32(x:bv64,y:bv64)
   requires y!=0
   requires y<8589934592
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4294967296 {
    var d:=(y&4294967295) as bv32;
    assert y==((d as bv64)|4294967296);
    Leading32(x,d);
   } else {
    Dispatch31(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch33(x:bv64,y:bv64)
   requires y!=0
   requires y<17179869184
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8589934592 {
    var d:=(y&8589934591) as bv33;
    assert y==((d as bv64)|8589934592);
    Leading33(x,d);
   } else {
    Dispatch32(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch34(x:bv64,y:bv64)
   requires y!=0
   requires y<34359738368
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=17179869184 {
    var d:=(y&17179869183) as bv34;
    assert y==((d as bv64)|17179869184);
    Leading34(x,d);
   } else {
    Dispatch33(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch35(x:bv64,y:bv64)
   requires y!=0
   requires y<68719476736
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=34359738368 {
    var d:=(y&34359738367) as bv35;
    assert y==((d as bv64)|34359738368);
    Leading35(x,d);
   } else {
    Dispatch34(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch36(x:bv64,y:bv64)
   requires y!=0
   requires y<137438953472
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=68719476736 {
    var d:=(y&68719476735) as bv36;
    assert y==((d as bv64)|68719476736);
    Leading36(x,d);
   } else {
    Dispatch35(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch37(x:bv64,y:bv64)
   requires y!=0
   requires y<274877906944
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=137438953472 {
    var d:=(y&137438953471) as bv37;
    assert y==((d as bv64)|137438953472);
    Leading37(x,d);
   } else {
    Dispatch36(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch38(x:bv64,y:bv64)
   requires y!=0
   requires y<549755813888
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=274877906944 {
    var d:=(y&274877906943) as bv38;
    assert y==((d as bv64)|274877906944);
    Leading38(x,d);
   } else {
    Dispatch37(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch39(x:bv64,y:bv64)
   requires y!=0
   requires y<1099511627776
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=549755813888 {
    var d:=(y&549755813887) as bv39;
    assert y==((d as bv64)|549755813888);
    Leading39(x,d);
   } else {
    Dispatch38(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch40(x:bv64,y:bv64)
   requires y!=0
   requires y<2199023255552
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1099511627776 {
    var d:=(y&1099511627775) as bv40;
    assert y==((d as bv64)|1099511627776);
    Leading40(x,d);
   } else {
    Dispatch39(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch41(x:bv64,y:bv64)
   requires y!=0
   requires y<4398046511104
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2199023255552 {
    var d:=(y&2199023255551) as bv41;
    assert y==((d as bv64)|2199023255552);
    Leading41(x,d);
   } else {
    Dispatch40(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch42(x:bv64,y:bv64)
   requires y!=0
   requires y<8796093022208
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4398046511104 {
    var d:=(y&4398046511103) as bv42;
    assert y==((d as bv64)|4398046511104);
    Leading42(x,d);
   } else {
    Dispatch41(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch43(x:bv64,y:bv64)
   requires y!=0
   requires y<17592186044416
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=8796093022208 {
    var d:=(y&8796093022207) as bv43;
    assert y==((d as bv64)|8796093022208);
    Leading43(x,d);
   } else {
    Dispatch42(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch44(x:bv64,y:bv64)
   requires y!=0
   requires y<35184372088832
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=17592186044416 {
    var d:=(y&17592186044415) as bv44;
    assert y==((d as bv64)|17592186044416);
    Leading44(x,d);
   } else {
    Dispatch43(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch45(x:bv64,y:bv64)
   requires y!=0
   requires y<70368744177664
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=35184372088832 {
    var d:=(y&35184372088831) as bv45;
    assert y==((d as bv64)|35184372088832);
    Leading45(x,d);
   } else {
    Dispatch44(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch46(x:bv64,y:bv64)
   requires y!=0
   requires y<140737488355328
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=70368744177664 {
    var d:=(y&70368744177663) as bv46;
    assert y==((d as bv64)|70368744177664);
    Leading46(x,d);
   } else {
    Dispatch45(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch47(x:bv64,y:bv64)
   requires y!=0
   requires y<281474976710656
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=140737488355328 {
    var d:=(y&140737488355327) as bv47;
    assert y==((d as bv64)|140737488355328);
    Leading47(x,d);
   } else {
    Dispatch46(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch48(x:bv64,y:bv64)
   requires y!=0
   requires y<562949953421312
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=281474976710656 {
    var d:=(y&281474976710655) as bv48;
    assert y==((d as bv64)|281474976710656);
    Leading48(x,d);
   } else {
    Dispatch47(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch49(x:bv64,y:bv64)
   requires y!=0
   requires y<1125899906842624
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=562949953421312 {
    var d:=(y&562949953421311) as bv49;
    assert y==((d as bv64)|562949953421312);
    Leading49(x,d);
   } else {
    Dispatch48(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch50(x:bv64,y:bv64)
   requires y!=0
   requires y<2251799813685248
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1125899906842624 {
    var d:=(y&1125899906842623) as bv50;
    assert y==((d as bv64)|1125899906842624);
    Leading50(x,d);
   } else {
    Dispatch49(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch51(x:bv64,y:bv64)
   requires y!=0
   requires y<4503599627370496
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2251799813685248 {
    var d:=(y&2251799813685247) as bv51;
    assert y==((d as bv64)|2251799813685248);
    Leading51(x,d);
   } else {
    Dispatch50(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch52(x:bv64,y:bv64)
   requires y!=0
   requires y<9007199254740992
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4503599627370496 {
    var d:=(y&4503599627370495) as bv52;
    assert y==((d as bv64)|4503599627370496);
    Leading52(x,d);
   } else {
    Dispatch51(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch53(x:bv64,y:bv64)
   requires y!=0
   requires y<18014398509481984
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=9007199254740992 {
    var d:=(y&9007199254740991) as bv53;
    assert y==((d as bv64)|9007199254740992);
    Leading53(x,d);
   } else {
    Dispatch52(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch54(x:bv64,y:bv64)
   requires y!=0
   requires y<36028797018963968
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=18014398509481984 {
    var d:=(y&18014398509481983) as bv54;
    assert y==((d as bv64)|18014398509481984);
    Leading54(x,d);
   } else {
    Dispatch53(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch55(x:bv64,y:bv64)
   requires y!=0
   requires y<72057594037927936
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=36028797018963968 {
    var d:=(y&36028797018963967) as bv55;
    assert y==((d as bv64)|36028797018963968);
    Leading55(x,d);
   } else {
    Dispatch54(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch56(x:bv64,y:bv64)
   requires y!=0
   requires y<144115188075855872
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=72057594037927936 {
    var d:=(y&72057594037927935) as bv56;
    assert y==((d as bv64)|72057594037927936);
    Leading56(x,d);
   } else {
    Dispatch55(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch57(x:bv64,y:bv64)
   requires y!=0
   requires y<288230376151711744
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=144115188075855872 {
    var d:=(y&144115188075855871) as bv57;
    assert y==((d as bv64)|144115188075855872);
    Leading57(x,d);
   } else {
    Dispatch56(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch58(x:bv64,y:bv64)
   requires y!=0
   requires y<576460752303423488
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=288230376151711744 {
    var d:=(y&288230376151711743) as bv58;
    assert y==((d as bv64)|288230376151711744);
    Leading58(x,d);
   } else {
    Dispatch57(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch59(x:bv64,y:bv64)
   requires y!=0
   requires y<1152921504606846976
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=576460752303423488 {
    var d:=(y&576460752303423487) as bv59;
    assert y==((d as bv64)|576460752303423488);
    Leading59(x,d);
   } else {
    Dispatch58(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch60(x:bv64,y:bv64)
   requires y!=0
   requires y<2305843009213693952
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=1152921504606846976 {
    var d:=(y&1152921504606846975) as bv60;
    assert y==((d as bv64)|1152921504606846976);
    Leading60(x,d);
   } else {
    Dispatch59(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch61(x:bv64,y:bv64)
   requires y!=0
   requires y<4611686018427387904
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=2305843009213693952 {
    var d:=(y&2305843009213693951) as bv61;
    assert y==((d as bv64)|2305843009213693952);
    Leading61(x,d);
   } else {
    Dispatch60(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch62(x:bv64,y:bv64)
   requires y!=0
   requires y<9223372036854775808
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=4611686018427387904 {
    var d:=(y&4611686018427387903) as bv62;
    assert y==((d as bv64)|4611686018427387904);
    Leading62(x,d);
   } else {
    Dispatch61(x,y);
   }
  }
  lemma {:induction false} {:isolate_assertions} Dispatch63(x:bv64,y:bv64)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)<<1)+(if (R(x>>1,y)<<1)+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)<<1)+(x&1)>=y then (R(x>>1,y)<<1)+(x&1)-y else (R(x>>1,y)<<1)+(x&1))
  {
   if y>=9223372036854775808 {
    var d:=(y&9223372036854775807) as bv63;
    assert y==((d as bv64)|9223372036854775808);
    Leading63(x,d);
   } else {
    Dispatch62(x,y);
   }
  }
  lemma {:induction false} NativeStep(x:bv64,y:bv64)
   requires y!=0
   ensures Q(x,y)==(Q(x>>1,y)+Q(x>>1,y))+(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then 1 else 0)
   ensures R(x,y)==(if (R(x>>1,y)+R(x>>1,y))+(x&1)>=y then (R(x>>1,y)+R(x>>1,y))+(x&1)-y else (R(x>>1,y)+R(x>>1,y))+(x&1))
  { Dispatch63(x,y); DoubleNative(Q(x>>1,y)); DoubleNative(R(x>>1,y));
  }
  lemma {:induction false} {:isolate_assertions} StepWitness(x:bv64,y:bv64,q:bv64,r:bv64,t:bv64,u:bv64)
   requires y!=0
   requires q==Q(x>>1,y) && r==R(x>>1,y)
   requires t==r+r+(x&1) && u==q+q
   ensures Q(x,y)==(if t>=y then u+1 else u)
   ensures R(x,y)==(if t>=y then t-y else t)
  { NativeStep(x,y); }
  // The recursive call establishes quotient and remainder for the high digits.
  // Their bounds justify exact doubling; append the low digit, then subtract
  // the divisor when needed. All recursion decreases the unsigned dividend.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv64,y:bv64)
   requires y!=0
   ensures Q(x,y) as int==(x as int)/(y as int)
   ensures R(x,y) as int==(x as int)%(y as int)
   decreases x as int
  {
   WordZero(y);
   if x==0 { Zero(y); assert Q(x,y) as int==0 && R(x,y) as int==0;
    A.Zero(x as int,y as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int); } else {
    var h,l:=x>>1,x&1;
    B.DigitSplit64(x);
    assert l==0 || l==1;
    Observe(h,y);
    var q,r:=Q(h,y),R(h,y);
    A.Bounds(h as int,y as int,q as int,r as int);
    var rr,t,u:=r+r,r+r+l,q+q;
    ExactAdd(r,r,rr); ExactAdd(rr,l,t);
    ExactAdd(q,q,u);
    StepWitness(x,y,q,r,t,u);
    if t>=y {
     assert Q(x,y)==u+1 && R(x,y)==t-y;
     Subtract(t,y,R(x,y));
     ExactAdd(u,1,Q(x,y));
     assert t as int>=y as int;
    } else {
     assert Q(x,y)==u && R(x,y)==t;
     ExactAdd(q,q,Q(x,y));
     Subtract(y,t,y-t); WordZero(y-t);
     assert y-t!=0;
     assert t as int<y as int;
    }
    assert (t>=y)==(t as int>=y as int);
    assert Q(x,y) as int==2*(q as int)+(if t>=y then 1 else 0);
    assert R(x,y) as int==(if t>=y then t as int-y as int else t as int);
    A.Finish(x as int,y as int,q as int,r as int,t as int,Q(x,y) as int,R(x,y) as int);
    assert Q(x,y) as int==(x as int)/(y as int);
    assert R(x,y) as int==(x as int)%(y as int);
   }
  }
  lemma {:induction false} Divide(x:bv64,y:bv64)
   requires y!=0
   ensures (x/y) as int==(x as int)/(y as int)
  { Observe(x,y); reveal Q(); }
  lemma {:induction false} Remainder(x:bv64,y:bv64)
   requires y!=0
   ensures (x%y) as int==(x as int)%(y as int)
  { Observe(x,y); reveal R(); }
}
