// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked modular multiplication; requires --integer-bitwise.
// Check these bodies with --solver-option O:smt.bv.solver=2; clients use the
// default engine. The included shift/bitwise bodies have a separate default-
// engine gate. See docs/dev/integer-bitwise-references.md.
include "IntegerBitwiseNativeShifts.dfy"
module Std.Arithmetic.IntegerBitwiseNativeMultiplicationArithmetic {
  import R = IntegerBitwise
  lemma {:induction false} Reduce(a:int,b:int,p:int,r:int,m:int)
    requires m>0
    requires p==(a*(b-1))%m
    requires r==(p+a)%m
    ensures r==(a*b)%m
  { R.ModShift(a*(b-1),a,m); }
}
module Std.Arithmetic.IntegerBitwiseNativeMultiplication8 {
  import B = IntegerBitwiseNative8
  import S = IntegerBitwiseNativeShifts8
  import A = IntegerBitwiseNativeMultiplicationArithmetic
  lemma {:induction false} Step(x:bv8,y:bv8)
    ensures x*y==x*(y-1)+x
  {}
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} TimesWitness(x:bv8,y:bv8,r:bv8)
    requires r==x*y
    ensures r as int==(x as int*y as int)%256
    decreases y as int
  {
   if y==0 {
    assert y as int==0; assert r==0;
   } else {
    B.Next8(y-1);
    assert (y-1) as int==y as int-1;
    var p:=x*(y-1);
    TimesWitness(x,y-1,p); Step(x,y);
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,256);
   }
  }
  lemma {:induction false} Times(x:bv8,y:bv8)
    ensures (x*y) as int==(x as int*y as int)%256
  { TimesWitness(x,y,x*y); }
}
module Std.Arithmetic.IntegerBitwiseNativeMultiplication16 {
  import B = IntegerBitwiseNative16
  import S = IntegerBitwiseNativeShifts16
  import A = IntegerBitwiseNativeMultiplicationArithmetic
  lemma {:induction false} Step(x:bv16,y:bv16)
    ensures x*y==x*(y-1)+x
  {}
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} TimesWitness(x:bv16,y:bv16,r:bv16)
    requires r==x*y
    ensures r as int==(x as int*y as int)%65536
    decreases y as int
  {
   if y==0 {
    assert y as int==0; assert r==0;
   } else {
    B.Next16(y-1);
    assert (y-1) as int==y as int-1;
    var p:=x*(y-1);
    TimesWitness(x,y-1,p); Step(x,y);
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,65536);
   }
  }
  lemma {:induction false} Times(x:bv16,y:bv16)
    ensures (x*y) as int==(x as int*y as int)%65536
  { TimesWitness(x,y,x*y); }
}
module Std.Arithmetic.IntegerBitwiseNativeMultiplication32 {
  import B = IntegerBitwiseNative32
  import S = IntegerBitwiseNativeShifts32
  import A = IntegerBitwiseNativeMultiplicationArithmetic
  lemma {:induction false} Step(x:bv32,y:bv32)
    ensures x*y==x*(y-1)+x
  {}
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} TimesWitness(x:bv32,y:bv32,r:bv32)
    requires r==x*y
    ensures r as int==(x as int*y as int)%4294967296
    decreases y as int
  {
   if y==0 {
    assert y as int==0; assert r==0;
   } else {
    B.Next32(y-1);
    assert (y-1) as int==y as int-1;
    var p:=x*(y-1);
    TimesWitness(x,y-1,p); Step(x,y);
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,4294967296);
   }
  }
  lemma {:induction false} Times(x:bv32,y:bv32)
    ensures (x*y) as int==(x as int*y as int)%4294967296
  { TimesWitness(x,y,x*y); }
}
module Std.Arithmetic.IntegerBitwiseNativeMultiplication64 {
  import B = IntegerBitwiseNative64
  import S = IntegerBitwiseNativeShifts64
  import A = IntegerBitwiseNativeMultiplicationArithmetic
  lemma {:induction false} Step(x:bv64,y:bv64)
    ensures x*y==x*(y-1)+x
  {}
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} TimesWitness(x:bv64,y:bv64,r:bv64)
    requires r==x*y
    ensures r as int==(x as int*y as int)%18446744073709551616
    decreases y as int
  {
   if y==0 {
    assert y as int==0; assert r==0;
   } else {
    B.Next64(y-1);
    assert (y-1) as int==y as int-1;
    var p:=x*(y-1);
    TimesWitness(x,y-1,p); Step(x,y);
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,18446744073709551616);
   }
  }
  lemma {:induction false} Times(x:bv64,y:bv64)
    ensures (x*y) as int==(x as int*y as int)%18446744073709551616
  { TimesWitness(x,y,x*y); }
}
