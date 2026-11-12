// RUN: %verify --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-reference-laws.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwise.dfy"

import opened Std.Arithmetic.IntegerBitwise

lemma {:induction false} SequenceWitnesses()
  ensures bitsNat(0,-7) == []
  ensures value([true,false,false,false]) == 1
  ensures bitsNat(4,1) == [true,false,false,false]
  ensures value(bitsNat(8,-1)) == 255
{
  reveal bitsNat(), value(), P();
  ValueBits([true,false,false,false]); BitsValue(8,-1);
  assert P(2) == 4;
  assert P(4) == 16;
  PowerSum(4,4);
}

// The semantic width is independent of a hypothetical native carrier.
// At N=12, W=8, x=16, k=8 the correct result is 1. Carrier rotation
// returns 16 even though both possible results fit the same 8-bit carrier.
lemma {:induction false} WidthMismatch()
  ensures rolNat(12,16,8) == 1 && rolNat(8,16,8) == 16
{ CarrierRotationCounterexample(); }

lemma {:induction false} TenClients(n: nat, a: int, b: int, m: int, k: nat, i: nat)
  requires 0 < n && 0 <= a < P(n) && k <= n && i < n
  ensures andNat(n,a,P(k)-1) == a%P(k)
  ensures andNat(n,a,b) <= a && orNat(n,a,b) >= a
  ensures shlNat(n,shrNat(n,a,k),k) == a-a%P(k)
  ensures xorNat(n,a,a) == 0
  ensures (andNat(n,shrNat(n,a,i),1) == 1) == bit(n,a,i)
  ensures bit(n,orNat(n,andNat(n,a,m),andNat(n,b,notNat(n,m))),i)
       == (if bit(n,m,i) then bit(n,a,i) else bit(n,b,i))
  ensures popNat(n,P(i)) == 1
  ensures shlNat(n,a,k) < P(n)
  ensures rolNat(n,a,0) == a && rolNat(n,a,n) == a
{
  AndMaskLow(n,a,k); AndBound(n,a,b); OrBound(n,a,b);
  ShiftRoundDown(n,a,k); XorSelf(n,a); BitTest(n,a,i);
  MaskMergeBit(n,a,b,m,i); PopPower(n,i); ShiftViews(n,a,k);
  NormId(n,a); RotateEndpoints(n,a);
}
