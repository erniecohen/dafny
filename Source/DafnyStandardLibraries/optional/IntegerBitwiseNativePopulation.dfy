// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked native population counting; requires --integer-bitwise.
include "IntegerBitwiseNative.dfy"

module Std.Arithmetic.IntegerBitwiseNativePopulation8 {
  import B = IntegerBitwiseNative8
  import C = IntegerBitwiseNative8
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  lemma {:induction false} Step(n:nat,x:bv8)
    requires n>0
    ensures R.popNat(n,x as int)==(x&1) as int+R.popNat(n-1,(x>>1) as int)
  { B.DigitSplit8(x); reveal R.popNat(); }

  method {:isolate_assertions} Count(x:bv8) returns(c:bv8)
    ensures c as int==IntegerBitwiseModel.popNat(8,x as int)
    ensures c as int==R.count(R.bitsNat(8,x as int))
    ensures c as int<=8
  {
    var rest:=x;
    var i:bv8:=0;
    c:=0;
    while i<8
      invariant i as int<=8
      invariant c as int<=i as int
      invariant c as int+R.popNat(8-(i as int),rest as int)==R.popNat(8,x as int)
      decreases 8-(i as int)
    {
      C.Next8(i);
      Step(8-(i as int),rest);
      B.DigitSplit8(rest);
      if rest&1==1 { C.Next8(c); c:=c+1; }
      rest:=rest>>1;
      i:=i+1;
    }
    assert R.popNat(0,rest as int)==0 by { reveal R.popNat(); }
    F.Population(8,x as int);
    R.PopulationView(8,x as int);
  }
}

module Std.Arithmetic.IntegerBitwiseNativePopulation16 {
  import B = IntegerBitwiseNative16
  import C = IntegerBitwiseNative8
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  lemma {:induction false} Step(n:nat,x:bv16)
    requires n>0
    ensures R.popNat(n,x as int)==(x&1) as int+R.popNat(n-1,(x>>1) as int)
  { B.DigitSplit16(x); reveal R.popNat(); }

  method {:isolate_assertions} Count(x:bv16) returns(c:bv8)
    ensures c as int==IntegerBitwiseModel.popNat(16,x as int)
    ensures c as int==R.count(R.bitsNat(16,x as int))
    ensures c as int<=16
  {
    var rest:=x;
    var i:bv8:=0;
    c:=0;
    while i<16
      invariant i as int<=16
      invariant c as int<=i as int
      invariant c as int+R.popNat(16-(i as int),rest as int)==R.popNat(16,x as int)
      decreases 16-(i as int)
    {
      C.Next8(i);
      Step(16-(i as int),rest);
      B.DigitSplit16(rest);
      if rest&1==1 { C.Next8(c); c:=c+1; }
      rest:=rest>>1;
      i:=i+1;
    }
    assert R.popNat(0,rest as int)==0 by { reveal R.popNat(); }
    F.Population(16,x as int);
    R.PopulationView(16,x as int);
  }
}

module Std.Arithmetic.IntegerBitwiseNativePopulation32 {
  import B = IntegerBitwiseNative32
  import C = IntegerBitwiseNative8
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  lemma {:induction false} Step(n:nat,x:bv32)
    requires n>0
    ensures R.popNat(n,x as int)==(x&1) as int+R.popNat(n-1,(x>>1) as int)
  { B.DigitSplit32(x); reveal R.popNat(); }

  method {:isolate_assertions} Count(x:bv32) returns(c:bv8)
    ensures c as int==IntegerBitwiseModel.popNat(32,x as int)
    ensures c as int==R.count(R.bitsNat(32,x as int))
    ensures c as int<=32
  {
    var rest:=x;
    var i:bv8:=0;
    c:=0;
    while i<32
      invariant i as int<=32
      invariant c as int<=i as int
      invariant c as int+R.popNat(32-(i as int),rest as int)==R.popNat(32,x as int)
      decreases 32-(i as int)
    {
      C.Next8(i);
      Step(32-(i as int),rest);
      B.DigitSplit32(rest);
      if rest&1==1 { C.Next8(c); c:=c+1; }
      rest:=rest>>1;
      i:=i+1;
    }
    assert R.popNat(0,rest as int)==0 by { reveal R.popNat(); }
    F.Population(32,x as int);
    R.PopulationView(32,x as int);
  }
}

module Std.Arithmetic.IntegerBitwiseNativePopulation64 {
  import B = IntegerBitwiseNative64
  import C = IntegerBitwiseNative8
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  lemma {:induction false} Step(n:nat,x:bv64)
    requires n>0
    ensures R.popNat(n,x as int)==(x&1) as int+R.popNat(n-1,(x>>1) as int)
  { B.DigitSplit64(x); reveal R.popNat(); }

  method {:isolate_assertions} Count(x:bv64) returns(c:bv8)
    ensures c as int==IntegerBitwiseModel.popNat(64,x as int)
    ensures c as int==R.count(R.bitsNat(64,x as int))
    ensures c as int<=64
  {
    var rest:=x;
    var i:bv8:=0;
    c:=0;
    while i<64
      invariant i as int<=64
      invariant c as int<=i as int
      invariant c as int+R.popNat(64-(i as int),rest as int)==R.popNat(64,x as int)
      decreases 64-(i as int)
    {
      C.Next8(i);
      Step(64-(i as int),rest);
      B.DigitSplit64(rest);
      if rest&1==1 { C.Next8(c); c:=c+1; }
      rest:=rest>>1;
      i:=i+1;
    }
    assert R.popNat(0,rest as int)==0 by { reveal R.popNat(); }
    F.Population(64,x as int);
    R.PopulationView(64,x as int);
  }
}
