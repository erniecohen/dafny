// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked modular multiplication; requires --integer-bitwise.
// Every body is checked with the default bitvector engine (bodies select Z3's own
// case split, O:smt.case_split=1). The induction works
// on an opaque product, so no native multiplier enters its integer reasoning.
// See docs/dev/integer-bitwise-references.md.
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
  // The product, hidden from the induction below.
  opaque function Product(x:bv8,y:bv8):bv8 { x*y }
  lemma {:induction false} {:isolate_assertions} Unfold(x:bv8,y:bv8)
    ensures Product(x,0)==0
    ensures Product(x,y+1)==Product(x,y)+x
  {
    reveal Product();
    calc { Product(x,y+1); == x*(y+1); == x*y+x; == Product(x,y)+x; }
  }
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv8,y:bv8)
    ensures Product(x,y) as int==(x as int*y as int)%256
    decreases y as int
  {
   if y==0 {
    Unfold(x,y);
    assert y as int==0;
   } else {
    var z:=y-1;
    B.Next8(z);
    assert z+1==y;
    assert z as int==y as int-1;
    Observe(x,z); Unfold(x,z);
    var p, r := Product(x,z), Product(x,y);
    assert r==p+x;
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,256);
   }
  }
  lemma {:induction false} TimesWitness(x:bv8,y:bv8,r:bv8)
    requires r==x*y
    ensures r as int==(x as int*y as int)%256
  { reveal Product(); Observe(x,y); }
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
  // The product, hidden from the induction below.
  opaque function Product(x:bv16,y:bv16):bv16 { x*y }
  lemma {:induction false} {:isolate_assertions} Unfold(x:bv16,y:bv16)
    ensures Product(x,0)==0
    ensures Product(x,y+1)==Product(x,y)+x
  {
    reveal Product();
    calc { Product(x,y+1); == x*(y+1); == x*y+x; == Product(x,y)+x; }
  }
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv16,y:bv16)
    ensures Product(x,y) as int==(x as int*y as int)%65536
    decreases y as int
  {
   if y==0 {
    Unfold(x,y);
    assert y as int==0;
   } else {
    var z:=y-1;
    B.Next16(z);
    assert z+1==y;
    assert z as int==y as int-1;
    Observe(x,z); Unfold(x,z);
    var p, r := Product(x,z), Product(x,y);
    assert r==p+x;
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,65536);
   }
  }
  lemma {:induction false} TimesWitness(x:bv16,y:bv16,r:bv16)
    requires r==x*y
    ensures r as int==(x as int*y as int)%65536
  { reveal Product(); Observe(x,y); }
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
  // The product, hidden from the induction below.
  opaque function Product(x:bv32,y:bv32):bv32 { x*y }
  lemma {:induction false} {:isolate_assertions} Unfold(x:bv32,y:bv32)
    ensures Product(x,0)==0
    ensures Product(x,y+1)==Product(x,y)+x
  {
    reveal Product();
    calc { Product(x,y+1); == x*(y+1); == x*y+x; == Product(x,y)+x; }
  }
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv32,y:bv32)
    ensures Product(x,y) as int==(x as int*y as int)%4294967296
    decreases y as int
  {
   if y==0 {
    Unfold(x,y);
    assert y as int==0;
   } else {
    var z:=y-1;
    B.Next32(z);
    assert z+1==y;
    assert z as int==y as int-1;
    Observe(x,z); Unfold(x,z);
    var p, r := Product(x,z), Product(x,y);
    assert r==p+x;
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,4294967296);
   }
  }
  lemma {:induction false} TimesWitness(x:bv32,y:bv32,r:bv32)
    requires r==x*y
    ensures r as int==(x as int*y as int)%4294967296
  { reveal Product(); Observe(x,y); }
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
  // The product, hidden from the induction below.
  opaque function Product(x:bv64,y:bv64):bv64 { x*y }
  lemma {:induction false} {:isolate_assertions} Unfold(x:bv64,y:bv64)
    ensures Product(x,0)==0
    ensures Product(x,y+1)==Product(x,y)+x
  {
    reveal Product();
    calc { Product(x,y+1); == x*(y+1); == x*y+x; == Product(x,y)+x; }
  }
  // Induct on the unsigned observation, using checked modular addition.
  lemma {:induction false} {:isolate_assertions} Observe(x:bv64,y:bv64)
    ensures Product(x,y) as int==(x as int*y as int)%18446744073709551616
    decreases y as int
  {
   if y==0 {
    Unfold(x,y);
    assert y as int==0;
   } else {
    var z:=y-1;
    B.Next64(z);
    assert z+1==y;
    assert z as int==y as int-1;
    Observe(x,z); Unfold(x,z);
    var p, r := Product(x,z), Product(x,y);
    assert r==p+x;
    S.PlusWitness(p,x,r);
    A.Reduce(x as int,y as int,p as int,r as int,18446744073709551616);
   }
  }
  lemma {:induction false} TimesWitness(x:bv64,y:bv64,r:bv64)
    requires r==x*y
    ensures r as int==(x as int*y as int)%18446744073709551616
  { reveal Product(); Observe(x,y); }
  lemma {:induction false} Times(x:bv64,y:bv64)
    ensures (x*y) as int==(x as int*y as int)%18446744073709551616
  { TimesWitness(x,y,x*y); }
}
