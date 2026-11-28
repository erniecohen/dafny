// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked unsigned equality and order; requires --integer-bitwise.
// Every body is checked with the default bitvector engine (bodies select Z3's own
// case split, O:smt.case_split=1). Order inducts on
// the unsigned observations through the checked digit split and inverse
// bridges of the included Boolean/conversion closure.
// See docs/dev/integer-bitwise-references.md.
include "IntegerBitwiseNative.dfy"

module Std.Arithmetic.IntegerBitwiseNativeOrderArithmetic {
  // Integer order compares the high digits, then the low digits.
  lemma {:induction false} Digits(a:int,b:int,ha:int,hb:int,la:int,lb:int)
    requires ha==a/2
    requires hb==b/2
    requires la==a%2
    requires lb==b%2
    ensures (a<b)==(ha<hb || (ha==hb && la==0 && lb==1))
  {}
}

module Std.Arithmetic.IntegerBitwiseNativeOrder8 {
  import B = IntegerBitwiseNative8
  import A = IntegerBitwiseNativeOrderArithmetic
  lemma {:induction false} {:isolate_assertions} Equal(x:bv8,y:bv8)
    ensures (x==y)==((x as int)==(y as int))
  {
    B.Inverse8(x); B.Inverse8(y);
  }

  // Native order compares the high digits, then the low bits.
  lemma {:induction false} Digits(x:bv8,y:bv8)
    ensures (x<y)==((x>>1)<(y>>1) || ((x>>1)==(y>>1) && x&1==0 && y&1==1))
    ensures x&1==0 || x&1==1
  {}

  // Induct on the unsigned observations, one low digit at a time.
  lemma {:induction false} {:isolate_assertions} Less(x:bv8,y:bv8)
    ensures (x<y)==((x as int)<(y as int))
    decreases x as int+y as int
  {
    var a, b := x as int, y as int;
    Equal(x,0); Equal(y,0);
    if x==0 || y==0 {
      assert (x==0)==(a==0) && (y==0)==(b==0);
    } else {
      var hx, hy, lx, ly := x>>1, y>>1, x&1, y&1;
      B.DigitSplit8(x); B.DigitSplit8(y);
      assert hx as int==a/2 && hy as int==b/2;
      assert lx as int==a%2 && ly as int==b%2;
      Digits(x,y); Digits(y,x);
      Equal(lx,0); Equal(ly,1);
      assert (lx==0)==(lx as int==0) && (ly==1)==(ly as int==1);
      assert (x<y)==(hx<hy || (hx==hy && lx==0 && ly==1));
      Less(hx,hy); Equal(hx,hy);
      A.Digits(a,b,hx as int,hy as int,lx as int,ly as int);
      assert (a<b)==(hx<hy || (hx==hy && lx==0 && ly==1));
    }
  }

  lemma {:induction false} {:isolate_assertions} LessEqual(x:bv8,y:bv8)
    ensures (x<=y)==((x as int)<=(y as int))
  {
    Less(y,x);
  }

}

module Std.Arithmetic.IntegerBitwiseNativeOrder16 {
  import B = IntegerBitwiseNative16
  import A = IntegerBitwiseNativeOrderArithmetic
  lemma {:induction false} {:isolate_assertions} Equal(x:bv16,y:bv16)
    ensures (x==y)==((x as int)==(y as int))
  {
    B.Inverse16(x); B.Inverse16(y);
  }

  // Native order compares the high digits, then the low bits.
  lemma {:induction false} Digits(x:bv16,y:bv16)
    ensures (x<y)==((x>>1)<(y>>1) || ((x>>1)==(y>>1) && x&1==0 && y&1==1))
    ensures x&1==0 || x&1==1
  {}

  // Induct on the unsigned observations, one low digit at a time.
  lemma {:induction false} {:isolate_assertions} Less(x:bv16,y:bv16)
    ensures (x<y)==((x as int)<(y as int))
    decreases x as int+y as int
  {
    var a, b := x as int, y as int;
    Equal(x,0); Equal(y,0);
    if x==0 || y==0 {
      assert (x==0)==(a==0) && (y==0)==(b==0);
    } else {
      var hx, hy, lx, ly := x>>1, y>>1, x&1, y&1;
      B.DigitSplit16(x); B.DigitSplit16(y);
      assert hx as int==a/2 && hy as int==b/2;
      assert lx as int==a%2 && ly as int==b%2;
      Digits(x,y); Digits(y,x);
      Equal(lx,0); Equal(ly,1);
      assert (lx==0)==(lx as int==0) && (ly==1)==(ly as int==1);
      assert (x<y)==(hx<hy || (hx==hy && lx==0 && ly==1));
      Less(hx,hy); Equal(hx,hy);
      A.Digits(a,b,hx as int,hy as int,lx as int,ly as int);
      assert (a<b)==(hx<hy || (hx==hy && lx==0 && ly==1));
    }
  }

  lemma {:induction false} {:isolate_assertions} LessEqual(x:bv16,y:bv16)
    ensures (x<=y)==((x as int)<=(y as int))
  {
    Less(y,x);
  }

}

module Std.Arithmetic.IntegerBitwiseNativeOrder32 {
  import B = IntegerBitwiseNative32
  import A = IntegerBitwiseNativeOrderArithmetic
  lemma {:induction false} {:isolate_assertions} Equal(x:bv32,y:bv32)
    ensures (x==y)==((x as int)==(y as int))
  {
    B.Inverse32(x); B.Inverse32(y);
  }

  // Native order compares the high digits, then the low bits.
  lemma {:induction false} Digits(x:bv32,y:bv32)
    ensures (x<y)==((x>>1)<(y>>1) || ((x>>1)==(y>>1) && x&1==0 && y&1==1))
    ensures x&1==0 || x&1==1
  {}

  // Induct on the unsigned observations, one low digit at a time.
  lemma {:induction false} {:isolate_assertions} Less(x:bv32,y:bv32)
    ensures (x<y)==((x as int)<(y as int))
    decreases x as int+y as int
  {
    var a, b := x as int, y as int;
    Equal(x,0); Equal(y,0);
    if x==0 || y==0 {
      assert (x==0)==(a==0) && (y==0)==(b==0);
    } else {
      var hx, hy, lx, ly := x>>1, y>>1, x&1, y&1;
      B.DigitSplit32(x); B.DigitSplit32(y);
      assert hx as int==a/2 && hy as int==b/2;
      assert lx as int==a%2 && ly as int==b%2;
      Digits(x,y); Digits(y,x);
      Equal(lx,0); Equal(ly,1);
      assert (lx==0)==(lx as int==0) && (ly==1)==(ly as int==1);
      assert (x<y)==(hx<hy || (hx==hy && lx==0 && ly==1));
      Less(hx,hy); Equal(hx,hy);
      A.Digits(a,b,hx as int,hy as int,lx as int,ly as int);
      assert (a<b)==(hx<hy || (hx==hy && lx==0 && ly==1));
    }
  }

  lemma {:induction false} {:isolate_assertions} LessEqual(x:bv32,y:bv32)
    ensures (x<=y)==((x as int)<=(y as int))
  {
    Less(y,x);
  }

}

module Std.Arithmetic.IntegerBitwiseNativeOrder64 {
  import B = IntegerBitwiseNative64
  import A = IntegerBitwiseNativeOrderArithmetic
  lemma {:induction false} {:isolate_assertions} Equal(x:bv64,y:bv64)
    ensures (x==y)==((x as int)==(y as int))
  {
    B.Inverse64(x); B.Inverse64(y);
  }

  // Native order compares the high digits, then the low bits.
  lemma {:induction false} Digits(x:bv64,y:bv64)
    ensures (x<y)==((x>>1)<(y>>1) || ((x>>1)==(y>>1) && x&1==0 && y&1==1))
    ensures x&1==0 || x&1==1
  {}

  // Induct on the unsigned observations, one low digit at a time.
  lemma {:induction false} {:isolate_assertions} Less(x:bv64,y:bv64)
    ensures (x<y)==((x as int)<(y as int))
    decreases x as int+y as int
  {
    var a, b := x as int, y as int;
    Equal(x,0); Equal(y,0);
    if x==0 || y==0 {
      assert (x==0)==(a==0) && (y==0)==(b==0);
    } else {
      var hx, hy, lx, ly := x>>1, y>>1, x&1, y&1;
      B.DigitSplit64(x); B.DigitSplit64(y);
      assert hx as int==a/2 && hy as int==b/2;
      assert lx as int==a%2 && ly as int==b%2;
      Digits(x,y); Digits(y,x);
      Equal(lx,0); Equal(ly,1);
      assert (lx==0)==(lx as int==0) && (ly==1)==(ly as int==1);
      assert (x<y)==(hx<hy || (hx==hy && lx==0 && ly==1));
      Less(hx,hy); Equal(hx,hy);
      A.Digits(a,b,hx as int,hy as int,lx as int,ly as int);
      assert (a<b)==(hx<hy || (hx==hy && lx==0 && ly==1));
    }
  }

  lemma {:induction false} {:isolate_assertions} LessEqual(x:bv64,y:bv64)
    ensures (x<=y)==((x as int)<=(y as int))
  {
    Less(y,x);
  }

}
