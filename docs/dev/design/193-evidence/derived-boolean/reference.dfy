// Pure reference definitions and proofs. No bridge assumptions.
opaque ghost function p(n: nat): nat
  ensures 1 <= p(n)
  decreases n
{ if n == 0 then 1 else 2*p(n-1) }

opaque ghost function andNat(n: nat,a: int,b: int): nat
  decreases n
{ if n == 0 then 0 else
    (if a%2 == 1 && b%2 == 1 then 1 else 0) + 2*andNat(n-1,a/2,b/2) }

opaque ghost function notNat(n: nat,a: int): nat
{ p(n)-1-a%p(n) }

opaque ghost function orNat(n: nat,a: int,b: int): nat
{ notNat(n,andNat(n,notNat(n,a),notNat(n,b))) }

opaque ghost function xorNat(n: nat,a: int,b: int): nat
{ andNat(n,orNat(n,a,b),notNat(n,andNat(n,a,b))) }

opaque ghost function andNotNat(n: nat,a: int,b: int): nat
{ andNat(n,a,notNat(n,b)) }

lemma {:induction false} andRange(n: nat,a: int,b: int)
  ensures andNat(n,a,b) < p(n)
  decreases n
{
  reveal p(),andNat();
  if n>0 { andRange(n-1,a/2,b/2); }
}

lemma {:induction false} notNorm(n: nat,a: int)
  requires 0 <= a < p(n)
  ensures notNat(n,a) == p(n)-1-a
{
  reveal notNat();
}

lemma {:induction false} {:resource_limit 2000000} orExpand(n: nat,a: int,b: int)
  requires 0 <= a < p(n) && 0 <= b < p(n)
  ensures orNat(n,a,b) == p(n)-1-andNat(n,p(n)-1-a,p(n)-1-b)
{
  notNorm(n,a);
  notNorm(n,b);
  andRange(n,p(n)-1-a,p(n)-1-b);
  notNorm(n,andNat(n,p(n)-1-a,p(n)-1-b));
  reveal orNat();
}

lemma {:induction false} xorExpand(n: nat,a: int,b: int)
  ensures xorNat(n,a,b) == andNat(n,orNat(n,a,b),p(n)-1-andNat(n,a,b))
{
  andRange(n,a,b);
  notNorm(n,andNat(n,a,b));
  reveal xorNat();
}

lemma {:induction false} andNotExpand(n: nat,a: int,b: int)
  requires 0 <= b < p(n)
  ensures andNotNat(n,a,b) == andNat(n,a,p(n)-1-b)
{
  notNorm(n,b);
  reveal andNotNat();
}

lemma {:induction false} power8()
  ensures p(8) == 256
{
  reveal p();
  assert p(0) == 1;
  assert p(1) == 2;
  assert p(2) == 4;
  assert p(3) == 8;
  assert p(4) == 16;
  assert p(5) == 32;
  assert p(6) == 64;
  assert p(7) == 128;
  assert p(8) == 256;
}

lemma {:induction false} power16()
  ensures p(16) == 65536
{
  power8();
  reveal p();
  assert p(9) == 512;
  assert p(10) == 1024;
  assert p(11) == 2048;
  assert p(12) == 4096;
  assert p(13) == 8192;
  assert p(14) == 16384;
  assert p(15) == 32768;
  assert p(16) == 65536;
}

lemma {:induction false} power32()
  ensures p(32) == 4294967296
{
  power16();
  reveal p();
  assert p(17) == 131072;
  assert p(18) == 262144;
  assert p(19) == 524288;
  assert p(20) == 1048576;
  assert p(21) == 2097152;
  assert p(22) == 4194304;
  assert p(23) == 8388608;
  assert p(24) == 16777216;
  assert p(25) == 33554432;
  assert p(26) == 67108864;
  assert p(27) == 134217728;
  assert p(28) == 268435456;
  assert p(29) == 536870912;
  assert p(30) == 1073741824;
  assert p(31) == 2147483648;
  assert p(32) == 4294967296;
}

lemma {:induction false} power64()
  ensures p(64) == 18446744073709551616
{
  power32();
  reveal p();
  assert p(33) == 8589934592;
  assert p(34) == 17179869184;
  assert p(35) == 34359738368;
  assert p(36) == 68719476736;
  assert p(37) == 137438953472;
  assert p(38) == 274877906944;
  assert p(39) == 549755813888;
  assert p(40) == 1099511627776;
  assert p(41) == 2199023255552;
  assert p(42) == 4398046511104;
  assert p(43) == 8796093022208;
  assert p(44) == 17592186044416;
  assert p(45) == 35184372088832;
  assert p(46) == 70368744177664;
  assert p(47) == 140737488355328;
  assert p(48) == 281474976710656;
  assert p(49) == 562949953421312;
  assert p(50) == 1125899906842624;
  assert p(51) == 2251799813685248;
  assert p(52) == 4503599627370496;
  assert p(53) == 9007199254740992;
  assert p(54) == 18014398509481984;
  assert p(55) == 36028797018963968;
  assert p(56) == 72057594037927936;
  assert p(57) == 144115188075855872;
  assert p(58) == 288230376151711744;
  assert p(59) == 576460752303423488;
  assert p(60) == 1152921504606846976;
  assert p(61) == 2305843009213693952;
  assert p(62) == 4611686018427387904;
  assert p(63) == 9223372036854775808;
  assert p(64) == 18446744073709551616;
}

lemma {:induction false} andZeroRight(n: nat,a: int)
  ensures andNat(n,a,0) == 0
  decreases n
{
  reveal andNat();
  if n>0 { andZeroRight(n-1,a/2); }
}

lemma {:induction false} andZeroLeft(n: nat,b: int)
  ensures andNat(n,0,b) == 0
  decreases n
{
  reveal andNat();
  if n>0 { andZeroLeft(n-1,b/2); }
}

lemma {:induction false} pThree(n: nat)
  requires 3 <= n
  ensures p(n) == 8*p(n-3)
{
  reveal p();
  assert p(n-2) == 2*p(n-3);
  assert p(n-1) == 2*p(n-2);
}

lemma {:induction false} andOnes(n: nat)
  ensures andNat(n,p(n)-1,p(n)-1) == p(n)-1
  decreases n
{
  reveal p(),andNat();
  if n>0 {
    andOnes(n-1);
    assert (p(n)-1)/2 == p(n-1)-1;
    assert (p(n)-1)%2 == 1;
  }
}

lemma {:induction false} {:resource_limit 2000000} smallAnds(n: nat)
  requires 3 <= n
  ensures andNat(n,5,3) == 1
  ensures andNat(n,5,p(n)-4) == 4
  ensures andNat(n,7,p(n)-2) == 6
  ensures andNat(n,p(n)-6,p(n)-4) == p(n)-8
{
  pThree(n);
  var q := p(n-3);
  andZeroRight(n-2,1);
  andZeroLeft(n-3,q-1);
  andOnes(n-3);
  reveal andNat();
  assert andNat(n-1,2,1) == 0;
  calc {
    andNat(n,5,p(n)-4);
    == 2*andNat(n-1,2,4*q-2);
    == 4*andNat(n-2,1,2*q-1);
    == 4+8*andNat(n-3,0,q-1);
    == 4;
  }
  calc {
    andNat(n,7,p(n)-2);
    == 2*andNat(n-1,3,4*q-1);
    == 2+4*andNat(n-2,1,2*q-1);
    == 6+8*andNat(n-3,0,q-1);
    == 6;
  }
  calc {
    andNat(n,p(n)-6,p(n)-4);
    == 2*andNat(n-1,4*q-3,4*q-2);
    == 4*andNat(n-2,2*q-2,2*q-1);
    == 8*andNat(n-3,q-1,q-1);
    == p(n)-8;
  }
}

lemma {:induction false} smallReference(n: nat)
  requires 3 <= n
  ensures andNat(n,5,3) == 1
  ensures orNat(n,5,3) == 7
  ensures xorNat(n,5,3) == 6
  ensures andNotNat(n,5,3) == 4
{
  pThree(n);
  smallAnds(n);
  orExpand(n,5,3);
  xorExpand(n,5,3);
  andNotExpand(n,5,3);
}
