// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked native Boolean bridges. Requires --integer-bitwise, independently of
// --additional-axioms. Every lemma body is a verification input.
include "IntegerBitwiseCompilerFoundation.dfy"

module Std.Arithmetic.IntegerBitwiseNative64 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  // One extra bit makes addition exact, before any narrow no-overflow check.
  lemma {:induction false} Add64(a:bv64,b:bv64)
    ensures ((a as bv65)+(b as bv65)) as int == a as int+b as int
  {}
  lemma {:induction false} Widen64(x:bv64)
    ensures (x as bv65) as int == x as int
  {}
  lemma {:induction false} {:isolate_assertions} NoOverflow64(a:bv64,b:bv64)
    requires (a as bv65)+(b as bv65) == (a+b) as bv65
    ensures (a+b) as int == a as int+b as int
  {
    Add64(a,b); Widen64(a+b);
  }
  lemma {:induction false} {:isolate_assertions} Split64(x:bv64)
    ensures x as int == 2*((x >> 1) as int) + (x & 1) as int
  {
    var h := x>>1;
    var l := x&1;
    NoOverflow64(h,h);
    assert (h+h) as int == 2*(h as int);
    NoOverflow64(h+h,l);
    assert (h+h+l) == x;
    assert x as int == ((h+h) as int)+(l as int);
  }
  lemma {:induction false} {:isolate_assertions} DigitSplit64(x:bv64)
    ensures (x>>1) as int == (x as int)/2
    ensures (x&1) as int == (x as int)%2
  {
    Split64(x);
    var l := x&1;
    assert l == 0 || l==1;
    assert 0<=l as int<2;
  }
  lemma {:induction false} {:isolate_assertions} Next64(p:bv64)
    requires p!=18446744073709551615
    ensures (p+1) as int==p as int+1
  {
    NoOverflow64(p,1);
    calc { (p+1) as int; == p as int+(1 as bv64) as int; == p as int+1; }
  }
  lemma {:induction false} Inverse64(x:bv64)
    ensures (x as int) as bv64 == x
  {}

  lemma {:induction false} {:isolate_assertions} Successor(p:bv64,a:int)
    requires p!=18446744073709551615
    requires p as int==a-1
    ensures (p+1) as int==a
  {
    Next64(p);
    calc { (p+1) as int; == p as int+1; == a; }
  }
  // Induct on the integer, using a checked native successor and inverse.
  lemma {:induction false} {:isolate_assertions} RoundTrip(a:nat)
    requires a<18446744073709551616
    ensures (a as bv64) as int==a
    decreases a
  {
    if a>0 {
      RoundTrip(a-1);
      var p:=(a-1) as bv64;
      Successor(p,a);
      var q:=p+1;
      Inverse64(q);
      assert q as int==a;
      assert q==(a as bv64);
    }
  }

  lemma {:induction false} Zero64(x:bv64)
    ensures (x==0) == (x as int==0)
  {}
  lemma {:induction false} {:isolate_assertions} AndStep64(x:bv64,y:bv64)
    ensures (x&y) as int == (if (x as int)%2==1 && (y as int)%2==1 then 1 else 0) + 2*(((x>>1)&(y>>1)) as int)
  {
    var z:=x&y;
    var h:=z>>1;
    var l:=z&1;
    DigitSplit64(x); DigitSplit64(y);
    Split64(z);
    assert h == (x>>1)&(y>>1);
    assert (x&1)==0 || (x&1)==1;
    assert (y&1)==0 || (y&1)==1;
    assert l as int == (if (x as int)%2==1 && (y as int)%2==1 then 1 else 0);
    assert z as int == 2*(h as int)+(l as int);
  }
  // A fixed native carrier, with a decreasing semantic prefix width.
  lemma {:induction false} {:isolate_assertions} AndPrefix64(n:nat,x:bv64,y:bv64)
    requires x as int < R.P(n) && y as int < R.P(n)
    ensures (x&y) as int == IntegerBitwiseModel.andNat(n,x as int,y as int)
    decreases n
  {
    if n==0 {
      reveal R.P();
      IntegerBitwiseModel.unfoldAnd(n,x as int,y as int);
      Zero64(x); Zero64(y);
      assert x==0 && y==0;
      assert (x&y) as int==0;
    } else {
      DigitSplit64(x); DigitSplit64(y);
      reveal R.P();
      var hx,hy:=x>>1,y>>1;
      AndPrefix64(n-1,hx,hy);
      assert (hx&hy) as int == IntegerBitwiseModel.andNat(n-1,(x as int)/2,(y as int)/2);
      AndStep64(x,y);
      AndRecurrence(n,x as int,y as int,(hx&hy) as int,(x&y) as int);
    }
  }

  // Keep the integer recurrence independent of native rewriting.
  lemma {:induction false} AndRecurrence(n:nat,a:int,b:int,h:int,r:int)
    requires n>0
    requires h==IntegerBitwiseModel.andNat(n-1,a/2,b/2)
    requires r==(if a%2==1 && b%2==1 then 1 else 0)+2*h
    ensures r==IntegerBitwiseModel.andNat(n,a,b)
  { IntegerBitwiseModel.unfoldAnd(n,a,b); }


  lemma {:induction false} {:isolate_assertions} NativeNot(x:bv64)
    ensures (!x) as int==18446744073709551615-x as int
  {
    Add64(x,!x);
    assert (x as bv65)+((!x) as bv65)==18446744073709551615;
  }
  lemma {:induction false} {:isolate_assertions} And(x:bv64,y:bv64)
    ensures (x&y) as int==IntegerBitwiseModel.andNat(64,x as int,y as int)
  {
    Power(); AndPrefix64(64,x,y);
  }
  lemma {:induction false} {:isolate_assertions} Not(x:bv64)
    ensures (!x) as int==IntegerBitwiseModel.notNat(64,x as int)
  {
    Power(); NativeNot(x);
    F.Normalize(64,x as int);
    R.NormId(64,x as int);
    IntegerBitwiseModel.unfoldNot(64,x as int);
  }
  lemma {:induction false} {:isolate_assertions} Or(x:bv64,y:bv64)
    ensures (x|y) as int==IntegerBitwiseModel.orNat(64,x as int,y as int)
  {
    var nx,ny:=!x,!y;
    var c:=nx&ny;
    Not(x); Not(y); And(nx,ny); Not(c);
    assert x|y == !c;
    IntegerBitwiseModel.unfoldOr(64,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} Xor(x:bv64,y:bv64)
    ensures (x^y) as int==IntegerBitwiseModel.xorNat(64,x as int,y as int)
  {
    var o,a:=x|y,x&y;
    var c:=!a;
    Or(x,y); And(x,y); Not(a); And(o,c);
    assert x^y == o&c;
    IntegerBitwiseModel.unfoldXor(64,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} AndNot(x:bv64,y:bv64)
    ensures (x&!y) as int==IntegerBitwiseModel.andNotNat(64,x as int,y as int)
  {
    var ny:=!y;
    Not(y); And(x,ny);
    IntegerBitwiseModel.unfoldAndNot(64,x as int,y as int);
  }
  lemma {:induction false} Power()
    ensures R.P(64)==18446744073709551616
    ensures IntegerBitwiseModel.P(64)==18446744073709551616
  {
    reveal R.P();
    assert R.P(0)==1;
    assert R.P(1)==2;
    assert R.P(2)==4;
    assert R.P(3)==8;
    assert R.P(4)==16;
    assert R.P(5)==32;
    assert R.P(6)==64;
    assert R.P(7)==128;
    assert R.P(8)==256;
    assert R.P(9)==512;
    assert R.P(10)==1024;
    assert R.P(11)==2048;
    assert R.P(12)==4096;
    assert R.P(13)==8192;
    assert R.P(14)==16384;
    assert R.P(15)==32768;
    assert R.P(16)==65536;
    assert R.P(17)==131072;
    assert R.P(18)==262144;
    assert R.P(19)==524288;
    assert R.P(20)==1048576;
    assert R.P(21)==2097152;
    assert R.P(22)==4194304;
    assert R.P(23)==8388608;
    assert R.P(24)==16777216;
    assert R.P(25)==33554432;
    assert R.P(26)==67108864;
    assert R.P(27)==134217728;
    assert R.P(28)==268435456;
    assert R.P(29)==536870912;
    assert R.P(30)==1073741824;
    assert R.P(31)==2147483648;
    assert R.P(32)==4294967296;
    assert R.P(33)==8589934592;
    assert R.P(34)==17179869184;
    assert R.P(35)==34359738368;
    assert R.P(36)==68719476736;
    assert R.P(37)==137438953472;
    assert R.P(38)==274877906944;
    assert R.P(39)==549755813888;
    assert R.P(40)==1099511627776;
    assert R.P(41)==2199023255552;
    assert R.P(42)==4398046511104;
    assert R.P(43)==8796093022208;
    assert R.P(44)==17592186044416;
    assert R.P(45)==35184372088832;
    assert R.P(46)==70368744177664;
    assert R.P(47)==140737488355328;
    assert R.P(48)==281474976710656;
    assert R.P(49)==562949953421312;
    assert R.P(50)==1125899906842624;
    assert R.P(51)==2251799813685248;
    assert R.P(52)==4503599627370496;
    assert R.P(53)==9007199254740992;
    assert R.P(54)==18014398509481984;
    assert R.P(55)==36028797018963968;
    assert R.P(56)==72057594037927936;
    assert R.P(57)==144115188075855872;
    assert R.P(58)==288230376151711744;
    assert R.P(59)==576460752303423488;
    assert R.P(60)==1152921504606846976;
    assert R.P(61)==2305843009213693952;
    assert R.P(62)==4611686018427387904;
    assert R.P(63)==9223372036854775808;
    assert R.P(64)==18446744073709551616;
    F.Power(64);
  }
}

module Std.Arithmetic.IntegerBitwiseNative8 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative64
  // One extra bit makes addition exact, before any narrow no-overflow check.
  lemma {:induction false} Add8(a:bv8,b:bv8)
    ensures ((a as bv9)+(b as bv9)) as int == a as int+b as int
  {}
  lemma {:induction false} Widen8(x:bv8)
    ensures (x as bv9) as int == x as int
  {}
  lemma {:induction false} {:isolate_assertions} NoOverflow8(a:bv8,b:bv8)
    requires (a as bv9)+(b as bv9) == (a+b) as bv9
    ensures (a+b) as int == a as int+b as int
  {
    Add8(a,b); Widen8(a+b);
  }
  lemma {:induction false} {:isolate_assertions} Split8(x:bv8)
    ensures x as int == 2*((x >> 1) as int) + (x & 1) as int
  {
    var h := x>>1;
    var l := x&1;
    NoOverflow8(h,h);
    assert (h+h) as int == 2*(h as int);
    NoOverflow8(h+h,l);
    assert (h+h+l) == x;
    assert x as int == ((h+h) as int)+(l as int);
  }
  lemma {:induction false} {:isolate_assertions} DigitSplit8(x:bv8)
    ensures (x>>1) as int == (x as int)/2
    ensures (x&1) as int == (x as int)%2
  {
    Split8(x);
    var l := x&1;
    assert l == 0 || l==1;
    assert 0<=l as int<2;
  }
  lemma {:induction false} {:isolate_assertions} Next8(p:bv8)
    requires p!=255
    ensures (p+1) as int==p as int+1
  {
    NoOverflow8(p,1);
    calc { (p+1) as int; == p as int+(1 as bv8) as int; == p as int+1; }
  }
  lemma {:induction false} Inverse8(x:bv8)
    ensures (x as int) as bv8 == x
  {}

  lemma {:induction false} {:isolate_assertions} Successor(p:bv8,a:int)
    requires p!=255
    requires p as int==a-1
    ensures (p+1) as int==a
  {
    Next8(p);
    calc { (p+1) as int; == p as int+1; == a; }
  }
  // Induct on the integer, using a checked native successor and inverse.
  lemma {:induction false} {:isolate_assertions} RoundTrip(a:nat)
    requires a<256
    ensures (a as bv8) as int==a
    decreases a
  {
    if a>0 {
      RoundTrip(a-1);
      var p:=(a-1) as bv8;
      Successor(p,a);
      var q:=p+1;
      Inverse8(q);
      assert q as int==a;
      assert q==(a as bv8);
    }
  }

  lemma {:induction false} {:isolate_assertions} NativeNot(x:bv8)
    ensures (!x) as int==255-x as int
  {
    Add8(x,!x);
    assert (x as bv9)+((!x) as bv9)==255;
  }
  lemma {:induction false} WidenTo64(x:bv8)
    ensures (x as bv64) as int==x as int
  {}
  lemma {:induction false} {:isolate_assertions} And(x:bv8,y:bv8)
    ensures (x&y) as int==IntegerBitwiseModel.andNat(8,x as int,y as int)
  {
    Power();
    var a,b:=x as bv64,y as bv64;
    WidenTo64(x); WidenTo64(y);
    B.AndPrefix64(8,a,b);
    var z:=x&y;
    assert a&b==z as bv64;
    WidenTo64(z);
  }
  lemma {:induction false} {:isolate_assertions} Not(x:bv8)
    ensures (!x) as int==IntegerBitwiseModel.notNat(8,x as int)
  {
    Power(); NativeNot(x);
    F.Normalize(8,x as int);
    R.NormId(8,x as int);
    IntegerBitwiseModel.unfoldNot(8,x as int);
  }
  lemma {:induction false} {:isolate_assertions} Or(x:bv8,y:bv8)
    ensures (x|y) as int==IntegerBitwiseModel.orNat(8,x as int,y as int)
  {
    var nx,ny:=!x,!y;
    var c:=nx&ny;
    Not(x); Not(y); And(nx,ny); Not(c);
    assert x|y == !c;
    IntegerBitwiseModel.unfoldOr(8,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} Xor(x:bv8,y:bv8)
    ensures (x^y) as int==IntegerBitwiseModel.xorNat(8,x as int,y as int)
  {
    var o,a:=x|y,x&y;
    var c:=!a;
    Or(x,y); And(x,y); Not(a); And(o,c);
    assert x^y == o&c;
    IntegerBitwiseModel.unfoldXor(8,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} AndNot(x:bv8,y:bv8)
    ensures (x&!y) as int==IntegerBitwiseModel.andNotNat(8,x as int,y as int)
  {
    var ny:=!y;
    Not(y); And(x,ny);
    IntegerBitwiseModel.unfoldAndNot(8,x as int,y as int);
  }
  lemma {:induction false} Power()
    ensures R.P(8)==256
    ensures IntegerBitwiseModel.P(8)==256
  {
    reveal R.P();
    assert R.P(0)==1;
    assert R.P(1)==2;
    assert R.P(2)==4;
    assert R.P(3)==8;
    assert R.P(4)==16;
    assert R.P(5)==32;
    assert R.P(6)==64;
    assert R.P(7)==128;
    assert R.P(8)==256;
    F.Power(8);
  }
}

module Std.Arithmetic.IntegerBitwiseNative16 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative64
  // One extra bit makes addition exact, before any narrow no-overflow check.
  lemma {:induction false} Add16(a:bv16,b:bv16)
    ensures ((a as bv17)+(b as bv17)) as int == a as int+b as int
  {}
  lemma {:induction false} Widen16(x:bv16)
    ensures (x as bv17) as int == x as int
  {}
  lemma {:induction false} {:isolate_assertions} NoOverflow16(a:bv16,b:bv16)
    requires (a as bv17)+(b as bv17) == (a+b) as bv17
    ensures (a+b) as int == a as int+b as int
  {
    Add16(a,b); Widen16(a+b);
  }
  lemma {:induction false} {:isolate_assertions} Split16(x:bv16)
    ensures x as int == 2*((x >> 1) as int) + (x & 1) as int
  {
    var h := x>>1;
    var l := x&1;
    NoOverflow16(h,h);
    assert (h+h) as int == 2*(h as int);
    NoOverflow16(h+h,l);
    assert (h+h+l) == x;
    assert x as int == ((h+h) as int)+(l as int);
  }
  lemma {:induction false} {:isolate_assertions} DigitSplit16(x:bv16)
    ensures (x>>1) as int == (x as int)/2
    ensures (x&1) as int == (x as int)%2
  {
    Split16(x);
    var l := x&1;
    assert l == 0 || l==1;
    assert 0<=l as int<2;
  }
  lemma {:induction false} {:isolate_assertions} Next16(p:bv16)
    requires p!=65535
    ensures (p+1) as int==p as int+1
  {
    NoOverflow16(p,1);
    calc { (p+1) as int; == p as int+(1 as bv16) as int; == p as int+1; }
  }
  lemma {:induction false} Inverse16(x:bv16)
    ensures (x as int) as bv16 == x
  {}

  lemma {:induction false} {:isolate_assertions} Successor(p:bv16,a:int)
    requires p!=65535
    requires p as int==a-1
    ensures (p+1) as int==a
  {
    Next16(p);
    calc { (p+1) as int; == p as int+1; == a; }
  }
  // Induct on the integer, using a checked native successor and inverse.
  lemma {:induction false} {:isolate_assertions} RoundTrip(a:nat)
    requires a<65536
    ensures (a as bv16) as int==a
    decreases a
  {
    if a>0 {
      RoundTrip(a-1);
      var p:=(a-1) as bv16;
      Successor(p,a);
      var q:=p+1;
      Inverse16(q);
      assert q as int==a;
      assert q==(a as bv16);
    }
  }

  lemma {:induction false} {:isolate_assertions} NativeNot(x:bv16)
    ensures (!x) as int==65535-x as int
  {
    Add16(x,!x);
    assert (x as bv17)+((!x) as bv17)==65535;
  }
  lemma {:induction false} WidenTo64(x:bv16)
    ensures (x as bv64) as int==x as int
  {}
  lemma {:induction false} {:isolate_assertions} And(x:bv16,y:bv16)
    ensures (x&y) as int==IntegerBitwiseModel.andNat(16,x as int,y as int)
  {
    Power();
    var a,b:=x as bv64,y as bv64;
    WidenTo64(x); WidenTo64(y);
    B.AndPrefix64(16,a,b);
    var z:=x&y;
    assert a&b==z as bv64;
    WidenTo64(z);
  }
  lemma {:induction false} {:isolate_assertions} Not(x:bv16)
    ensures (!x) as int==IntegerBitwiseModel.notNat(16,x as int)
  {
    Power(); NativeNot(x);
    F.Normalize(16,x as int);
    R.NormId(16,x as int);
    IntegerBitwiseModel.unfoldNot(16,x as int);
  }
  lemma {:induction false} {:isolate_assertions} Or(x:bv16,y:bv16)
    ensures (x|y) as int==IntegerBitwiseModel.orNat(16,x as int,y as int)
  {
    var nx,ny:=!x,!y;
    var c:=nx&ny;
    Not(x); Not(y); And(nx,ny); Not(c);
    assert x|y == !c;
    IntegerBitwiseModel.unfoldOr(16,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} Xor(x:bv16,y:bv16)
    ensures (x^y) as int==IntegerBitwiseModel.xorNat(16,x as int,y as int)
  {
    var o,a:=x|y,x&y;
    var c:=!a;
    Or(x,y); And(x,y); Not(a); And(o,c);
    assert x^y == o&c;
    IntegerBitwiseModel.unfoldXor(16,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} AndNot(x:bv16,y:bv16)
    ensures (x&!y) as int==IntegerBitwiseModel.andNotNat(16,x as int,y as int)
  {
    var ny:=!y;
    Not(y); And(x,ny);
    IntegerBitwiseModel.unfoldAndNot(16,x as int,y as int);
  }
  lemma {:induction false} Power()
    ensures R.P(16)==65536
    ensures IntegerBitwiseModel.P(16)==65536
  {
    reveal R.P();
    assert R.P(0)==1;
    assert R.P(1)==2;
    assert R.P(2)==4;
    assert R.P(3)==8;
    assert R.P(4)==16;
    assert R.P(5)==32;
    assert R.P(6)==64;
    assert R.P(7)==128;
    assert R.P(8)==256;
    assert R.P(9)==512;
    assert R.P(10)==1024;
    assert R.P(11)==2048;
    assert R.P(12)==4096;
    assert R.P(13)==8192;
    assert R.P(14)==16384;
    assert R.P(15)==32768;
    assert R.P(16)==65536;
    F.Power(16);
  }
}

module Std.Arithmetic.IntegerBitwiseNative32 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative64
  // One extra bit makes addition exact, before any narrow no-overflow check.
  lemma {:induction false} Add32(a:bv32,b:bv32)
    ensures ((a as bv33)+(b as bv33)) as int == a as int+b as int
  {}
  lemma {:induction false} Widen32(x:bv32)
    ensures (x as bv33) as int == x as int
  {}
  lemma {:induction false} {:isolate_assertions} NoOverflow32(a:bv32,b:bv32)
    requires (a as bv33)+(b as bv33) == (a+b) as bv33
    ensures (a+b) as int == a as int+b as int
  {
    Add32(a,b); Widen32(a+b);
  }
  lemma {:induction false} {:isolate_assertions} Split32(x:bv32)
    ensures x as int == 2*((x >> 1) as int) + (x & 1) as int
  {
    var h := x>>1;
    var l := x&1;
    NoOverflow32(h,h);
    assert (h+h) as int == 2*(h as int);
    NoOverflow32(h+h,l);
    assert (h+h+l) == x;
    assert x as int == ((h+h) as int)+(l as int);
  }
  lemma {:induction false} {:isolate_assertions} DigitSplit32(x:bv32)
    ensures (x>>1) as int == (x as int)/2
    ensures (x&1) as int == (x as int)%2
  {
    Split32(x);
    var l := x&1;
    assert l == 0 || l==1;
    assert 0<=l as int<2;
  }
  lemma {:induction false} {:isolate_assertions} Next32(p:bv32)
    requires p!=4294967295
    ensures (p+1) as int==p as int+1
  {
    NoOverflow32(p,1);
    calc { (p+1) as int; == p as int+(1 as bv32) as int; == p as int+1; }
  }
  lemma {:induction false} Inverse32(x:bv32)
    ensures (x as int) as bv32 == x
  {}

  lemma {:induction false} {:isolate_assertions} Successor(p:bv32,a:int)
    requires p!=4294967295
    requires p as int==a-1
    ensures (p+1) as int==a
  {
    Next32(p);
    calc { (p+1) as int; == p as int+1; == a; }
  }
  // Induct on the integer, using a checked native successor and inverse.
  lemma {:induction false} {:isolate_assertions} RoundTrip(a:nat)
    requires a<4294967296
    ensures (a as bv32) as int==a
    decreases a
  {
    if a>0 {
      RoundTrip(a-1);
      var p:=(a-1) as bv32;
      Successor(p,a);
      var q:=p+1;
      Inverse32(q);
      assert q as int==a;
      assert q==(a as bv32);
    }
  }

  lemma {:induction false} {:isolate_assertions} NativeNot(x:bv32)
    ensures (!x) as int==4294967295-x as int
  {
    Add32(x,!x);
    assert (x as bv33)+((!x) as bv33)==4294967295;
  }
  lemma {:induction false} WidenTo64(x:bv32)
    ensures (x as bv64) as int==x as int
  {}
  lemma {:induction false} {:isolate_assertions} And(x:bv32,y:bv32)
    ensures (x&y) as int==IntegerBitwiseModel.andNat(32,x as int,y as int)
  {
    Power();
    var a,b:=x as bv64,y as bv64;
    WidenTo64(x); WidenTo64(y);
    B.AndPrefix64(32,a,b);
    var z:=x&y;
    assert a&b==z as bv64;
    WidenTo64(z);
  }
  lemma {:induction false} {:isolate_assertions} Not(x:bv32)
    ensures (!x) as int==IntegerBitwiseModel.notNat(32,x as int)
  {
    Power(); NativeNot(x);
    F.Normalize(32,x as int);
    R.NormId(32,x as int);
    IntegerBitwiseModel.unfoldNot(32,x as int);
  }
  lemma {:induction false} {:isolate_assertions} Or(x:bv32,y:bv32)
    ensures (x|y) as int==IntegerBitwiseModel.orNat(32,x as int,y as int)
  {
    var nx,ny:=!x,!y;
    var c:=nx&ny;
    Not(x); Not(y); And(nx,ny); Not(c);
    assert x|y == !c;
    IntegerBitwiseModel.unfoldOr(32,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} Xor(x:bv32,y:bv32)
    ensures (x^y) as int==IntegerBitwiseModel.xorNat(32,x as int,y as int)
  {
    var o,a:=x|y,x&y;
    var c:=!a;
    Or(x,y); And(x,y); Not(a); And(o,c);
    assert x^y == o&c;
    IntegerBitwiseModel.unfoldXor(32,x as int,y as int);
  }
  lemma {:induction false} {:isolate_assertions} AndNot(x:bv32,y:bv32)
    ensures (x&!y) as int==IntegerBitwiseModel.andNotNat(32,x as int,y as int)
  {
    var ny:=!y;
    Not(y); And(x,ny);
    IntegerBitwiseModel.unfoldAndNot(32,x as int,y as int);
  }
  lemma {:induction false} Power()
    ensures R.P(32)==4294967296
    ensures IntegerBitwiseModel.P(32)==4294967296
  {
    reveal R.P();
    assert R.P(0)==1;
    assert R.P(1)==2;
    assert R.P(2)==4;
    assert R.P(3)==8;
    assert R.P(4)==16;
    assert R.P(5)==32;
    assert R.P(6)==64;
    assert R.P(7)==128;
    assert R.P(8)==256;
    assert R.P(9)==512;
    assert R.P(10)==1024;
    assert R.P(11)==2048;
    assert R.P(12)==4096;
    assert R.P(13)==8192;
    assert R.P(14)==16384;
    assert R.P(15)==32768;
    assert R.P(16)==65536;
    assert R.P(17)==131072;
    assert R.P(18)==262144;
    assert R.P(19)==524288;
    assert R.P(20)==1048576;
    assert R.P(21)==2097152;
    assert R.P(22)==4194304;
    assert R.P(23)==8388608;
    assert R.P(24)==16777216;
    assert R.P(25)==33554432;
    assert R.P(26)==67108864;
    assert R.P(27)==134217728;
    assert R.P(28)==268435456;
    assert R.P(29)==536870912;
    assert R.P(30)==1073741824;
    assert R.P(31)==2147483648;
    assert R.P(32)==4294967296;
    F.Power(32);
  }
}

