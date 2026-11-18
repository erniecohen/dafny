// Design-only projection of finite local bridge instances, not product code.
// Every assume below is one explicitly identified proposed emitter fact.
// Certificates are ordinary checked lemmas; control assertions must FAIL.

opaque ghost function p(n: nat): nat
  ensures 1 <= p(n)
  decreases n
{ if n == 0 then 1 else 2*p(n-1) }

opaque ghost function andNat(n: nat, a: int, b: int): nat
  decreases n
{ if n == 0 then 0 else
    (if a%2 == 1 && b%2 == 1 then 1 else 0) + 2*andNat(n-1,a/2,b/2) }

lemma {:induction false} andZero(n: nat,a: int)
  ensures andNat(n,a,0) == 0
  decreases n
{
  reveal andNat();
  if n>0 { andZero(n-1,a/2); }
}

lemma {:induction false} and53(n: nat)
  requires 3 <= n
  ensures andNat(n,5,3) == 1
{
  andZero(n-2,1);
  reveal andNat();
  assert andNat(n-1,2,1) == 0;
}

lemma {:induction false} powers16()
  ensures p(0) == 1 && p(1) == 2
  ensures p(16) == 65536
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
  assert p(9) == 512;
  assert p(10) == 1024;
  assert p(11) == 2048;
  assert p(12) == 4096;
  assert p(13) == 8192;
  assert p(14) == 16384;
  assert p(15) == 32768;
  assert p(16) == 65536;
}

lemma {:induction false} powers32()
  ensures p(0) == 1 && p(1) == 2
  ensures p(32) == 4294967296
{
  powers16();
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

lemma {:induction false} powers()
  ensures p(0) == 1 && p(1) == 2
  ensures p(64) == 18446744073709551616
{
  powers32();
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

lemma {:induction false} control_A64()
{
  and53(64);
  // A, composed at checked integer literals (its range guard is true).
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616
    ==> (((5 as bv64) & (3 as bv64)) as int) == andNat(64,5,3);
  // The raw A instance is retained as well.
  assume {:axiom} (((5 as bv64) & (3 as bv64)) as int) ==
    andNat(64,(5 as bv64) as int,(3 as bv64) as int);
  assert false;
}

lemma {:induction false} control_C64()
{
  assume {:axiom} 0 <= 5 < 18446744073709551616
    ==> ((!(5 as bv64)) as int) == 18446744073709551615-5;
  assume {:axiom} ((!(5 as bv64)) as int) ==
    18446744073709551615-((5 as bv64) as int);
  assert false;
}

lemma {:induction false} control_L64()
{
  powers();
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 1 <= 64
    ==> (((5 as bv64)<<1) as int) == (5*p(1))%18446744073709551616;
  assume {:axiom} 0 <= 1 <= 64
    ==> (((5 as bv64)<<1) as int) ==
    (((5 as bv64) as int)*p(1))%18446744073709551616;
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 64 <= 64
    ==> (((5 as bv64)<<64) as int) == (5*p(64))%18446744073709551616;
  assume {:axiom} 0 <= 64 <= 64
    ==> (((5 as bv64)<<64) as int) ==
    (((5 as bv64) as int)*p(64))%18446744073709551616;
  assert false;
}

lemma {:induction false} control_R64()
{
  powers();
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 1 <= 64
    ==> (((5 as bv64)>>1) as int) == 5/p(1);
  assume {:axiom} 0 <= 1 <= 64
    ==> (((5 as bv64)>>1) as int) == ((5 as bv64) as int)/p(1);
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 64 <= 64
    ==> (((5 as bv64)>>64) as int) == 5/p(64);
  assume {:axiom} 0 <= 64 <= 64
    ==> (((5 as bv64)>>64) as int) == ((5 as bv64) as int)/p(64);
  assert false;
}

lemma {:induction false} control_add64()
{
  // +: the composed instance keeps all operand-range and divisor guards.
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616
    ==> (((5 as bv64) + (3 as bv64)) as int) == (5+3)%18446744073709551616;
  // +: the raw instance uses the unsigned native operand observations.
  assume {:axiom} (((5 as bv64) + (3 as bv64)) as int) == (((5 as bv64) as int)+((3 as bv64) as int))%18446744073709551616;
  assert false;
}

lemma {:induction false} control_sub64()
{
  // -: the composed instance keeps all operand-range and divisor guards.
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 9 < 18446744073709551616
    ==> (((5 as bv64) - (9 as bv64)) as int) == (5-9)%18446744073709551616;
  // -: the raw instance uses the unsigned native operand observations.
  assume {:axiom} (((5 as bv64) - (9 as bv64)) as int) == (((5 as bv64) as int)-((9 as bv64) as int))%18446744073709551616;
  assert false;
}

lemma {:induction false} control_mul64()
{
  // *: the composed instance keeps all operand-range and divisor guards.
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616
    ==> (((5 as bv64) * (3 as bv64)) as int) == (5*3)%18446744073709551616;
  // *: the raw instance uses the unsigned native operand observations.
  assume {:axiom} (((5 as bv64) * (3 as bv64)) as int) == (((5 as bv64) as int)*((3 as bv64) as int))%18446744073709551616;
  assert false;
}

lemma {:induction false} control_div64()
{
  // /: the composed instance keeps all operand-range and divisor guards.
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616 && 3 != 0
    ==> (((5 as bv64) / (3 as bv64)) as int) == 5/3;
  // /: the raw instance uses the unsigned native operand observations.
  assume {:axiom} ((3 as bv64) as int) != 0 ==> (((5 as bv64) / (3 as bv64)) as int) == ((5 as bv64) as int)/((3 as bv64) as int);
  assert false;
}

lemma {:induction false} control_mod64()
{
  // %: the composed instance keeps all operand-range and divisor guards.
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616 && 3 != 0
    ==> (((5 as bv64) % (3 as bv64)) as int) == 5%3;
  // %: the raw instance uses the unsigned native operand observations.
  assume {:axiom} ((3 as bv64) as int) != 0 ==> (((5 as bv64) % (3 as bv64)) as int) == ((5 as bv64) as int)%((3 as bv64) as int);
  assert false;
}

lemma {:induction false} control_le64()
{
  // <=: composed and raw comparison instances, at the same inhabited witness.
  assume {:axiom} 0 <= 5 < 18446744073709551616 && 0 <= 3 < 18446744073709551616
    ==> (((5 as bv64) <= (3 as bv64)) <==> (5 <= 3));
  assume {:axiom} ((5 as bv64) <= (3 as bv64)) <==>
    (((5 as bv64) as int) <= ((3 as bv64) as int));
  assert false;
}
