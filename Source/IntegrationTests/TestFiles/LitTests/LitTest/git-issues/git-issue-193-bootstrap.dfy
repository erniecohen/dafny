// RUN: %verify --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-bootstrap.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseCompiler.dfy"
import opened Std.Arithmetic.IntegerBitwiseCompiler

// These are consumers of the checked, range-bearing interface. The twelve
// foundations in the included companion check raw signatures separately.
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

lemma {:induction false} WidthAndEndpoints()
  ensures rolNat(12,16,8) == 1 && rolNat(8,16,8) == 16
  ensures bitsNat(0,-7) == []
{
  CarrierRotationCounterexample();
  ReferenceEndpoints(0,-7,-1);
}
