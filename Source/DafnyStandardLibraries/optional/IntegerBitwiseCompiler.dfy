// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked compiler-reference consumers for issue 193 (M2).
// Requires --integer-bitwise; --additional-axioms remains independent.
include "IntegerBitwiseCompilerFoundation.dfy"

module Std.Arithmetic.IntegerBitwiseCompiler {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation

  lemma {:induction false} Power(n: nat)
    ensures IntegerBitwiseModel.P(n) == R.P(n)
    decreases n
  { F.Power(n); }

  lemma {:induction false} Normalize(n: nat, a: int)
    ensures IntegerBitwiseModel.norm(n,a) == R.norm(n,a)
  { F.Normalize(n,a); }

  lemma {:induction false} And(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.andNat(n,a,b) == R.andNat(n,a,b)
    decreases n
  { F.And(n,a,b); }

  lemma {:induction false} Not(n: nat, a: int)
    ensures IntegerBitwiseModel.notNat(n,a) == R.notNat(n,a)
  { F.Not(n,a); }

  lemma {:induction false} Or(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.orNat(n,a,b) == R.orNat(n,a,b)
  { F.Or(n,a,b); }

  lemma {:induction false} Xor(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.xorNat(n,a,b) == R.xorNat(n,a,b)
  { F.Xor(n,a,b); }

  lemma {:induction false} AndNot(n: nat, a: int, b: int)
    ensures IntegerBitwiseModel.andNotNat(n,a,b) == R.andNotNat(n,a,b)
  { F.AndNot(n,a,b); }

  lemma {:induction false} LeftShift(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.shlNat(n,a,k) == R.shlNat(n,a,k)
  { F.LeftShift(n,a,k); }

  lemma {:induction false} RightShift(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.shrNat(n,a,k) == R.shrNat(n,a,k)
  { F.RightShift(n,a,k); }

  lemma {:induction false} LeftRotate(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.rolNat(n,a,k) == R.rolNat(n,a,k)
  { F.LeftRotate(n,a,k); }

  lemma {:induction false} RightRotate(n: nat, a: int, k: int)
    ensures IntegerBitwiseModel.rorNat(n,a,k) == R.rorNat(n,a,k)
  { F.RightRotate(n,a,k); }

  lemma {:induction false} Population(n: nat, a: int)
    ensures IntegerBitwiseModel.popNat(n,a) == R.popNat(n,a)
    decreases n
  { F.Population(n,a); }

  // Checked consumer interface and transport of the existing M1 laws.
  opaque ghost function P(n: nat): nat
    ensures 1 <= P(n)
    decreases n
    ensures P(n) == R.P(n)
  { Power(n); IntegerBitwiseModel.P(n) }

  ghost function norm(n: nat, a: int): nat
    ensures norm(n,a) == R.norm(n,a)
  { Normalize(n,a); IntegerBitwiseModel.norm(n,a) }

  opaque ghost function bitsNat(n: nat, a: int): seq<bool>
    ensures |bitsNat(n,a)| == n
    decreases n
    ensures bitsNat(n,a) == R.bitsNat(n,a)
  { R.bitsNat(n,a) }

  opaque ghost function value(s: seq<bool>): nat
    decreases |s|
    ensures value(s) == R.value(s)
  { R.value(s) }

  opaque ghost function andNat(n: nat, a: int, b: int): nat
    decreases n
    ensures andNat(n,a,b) == R.andNat(n,a,b)
  { And(n,a,b); IntegerBitwiseModel.andNat(n,a,b) }

  opaque ghost function notNat(n: nat, a: int): nat
    ensures notNat(n,a) == R.notNat(n,a)
  { Not(n,a); IntegerBitwiseModel.notNat(n,a) }

  opaque ghost function orNat(n: nat, a: int, b: int): nat
    ensures orNat(n,a,b) == R.orNat(n,a,b)
  { Or(n,a,b); IntegerBitwiseModel.orNat(n,a,b) }

  opaque ghost function xorNat(n: nat, a: int, b: int): nat
    ensures xorNat(n,a,b) == R.xorNat(n,a,b)
  { Xor(n,a,b); IntegerBitwiseModel.xorNat(n,a,b) }

  opaque ghost function andNotNat(n: nat, a: int, b: int): nat
    ensures andNotNat(n,a,b) == R.andNotNat(n,a,b)
  { AndNot(n,a,b); IntegerBitwiseModel.andNotNat(n,a,b) }

  opaque ghost function shlNat(n: nat, a: int, k: int): nat
    ensures shlNat(n,a,k) == R.shlNat(n,a,k)
  { LeftShift(n,a,k); IntegerBitwiseModel.shlNat(n,a,k) }

  opaque ghost function shrNat(n: nat, a: int, k: int): nat
    ensures shrNat(n,a,k) == R.shrNat(n,a,k)
  { RightShift(n,a,k); IntegerBitwiseModel.shrNat(n,a,k) }

  opaque ghost function rolNat(n: nat, a: int, k: int): nat
    ensures rolNat(n,a,k) == R.rolNat(n,a,k)
  { LeftRotate(n,a,k); IntegerBitwiseModel.rolNat(n,a,k) }

  opaque ghost function rorNat(n: nat, a: int, k: int): nat
    ensures rorNat(n,a,k) == R.rorNat(n,a,k)
  { RightRotate(n,a,k); IntegerBitwiseModel.rorNat(n,a,k) }

  opaque ghost function popNat(n: nat, a: int): nat
    decreases n
    ensures popNat(n,a) == R.popNat(n,a)
  { Population(n,a); IntegerBitwiseModel.popNat(n,a) }

  ghost function bit(n: nat, a: int, i: nat): bool
    requires i < n
    ensures bit(n,a,i) == R.bit(n,a,i)
  { R.bit(n,a,i) }

  lemma {:induction false} PowerZero()
    ensures P(0) == 1
  { R.PowerZero(); }

  lemma {:induction false} NormId(n: nat, a: int)
    requires 0 <= a < P(n)
    ensures norm(n,a) == a
  { R.NormId(n,a); }

  lemma {:induction false} PowerSum(a: nat, b: nat)
    ensures P(a+b) == P(a)*P(b)
    decreases a
  { R.PowerSum(a,b); }

  lemma {:induction false} Quotient(a: int, d: int, q: int, r: int)
    requires 0 < d && a == q*d+r && 0 <= r < d
    ensures a/d == q && a%d == r
  { R.Quotient(a,d,q,r); }

  lemma {:induction false} ProductBounds(r: int, d: int, u: int, e: int)
    requires 0 < d && 0 < e && 0 <= r < d && 0 <= u < e
    ensures 0 <= r+d*u < d*e
  { R.ProductBounds(r,d,u,e); }

  lemma {:induction false} {:isolate_assertions} DivProduct(a: int, d: int, e: int)
    requires 0 < d && 0 < e
    ensures a/d/e == a/(d*e)
    ensures a%(d*e) == a%d + d*((a/d)%e)
  { R.DivProduct(a,d,e); }

  lemma {:induction false} NormStep(n: nat, a: int)
    requires 0 < n
    ensures norm(n,a) == a%2+2*norm(n-1,a/2)
  { R.NormStep(n,a); }

  lemma {:induction false} ValueBound(s: seq<bool>)
    ensures value(s) < P(|s|)
    decreases |s|
  { R.ValueBound(s); }

  lemma {:induction false} BitsValue(n: nat, a: int)
    ensures value(bitsNat(n,a)) == norm(n,a)
    decreases n
  { R.BitsValue(n,a); }

  lemma {:induction false} ValueBits(s: seq<bool>)
    ensures bitsNat(|s|,value(s)) == s
    decreases |s|
  { R.ValueBits(s); }

  lemma {:induction false} ValueInjective(s: seq<bool>, t: seq<bool>)
    requires |s| == |t|
    ensures (value(s) == value(t)) == (s == t)
  { R.ValueInjective(s,t); }

  lemma {:induction false} NormalizeBits(n: nat, a: int)
    ensures bitsNat(n,norm(n,a)) == bitsNat(n,a)
  { R.NormalizeBits(n,a); }

  lemma {:induction false} BitAt(n: nat, a: int, i: nat)
    requires i < n
    ensures bit(n,a,i) == ((a/P(i))%2 == 1)
    decreases i
  { R.BitAt(n,a,i); }

  lemma {:induction false} AndRange(n: nat, a: int, b: int)
    ensures andNat(n,a,b) < P(n)
    decreases n
  { R.AndRange(n,a,b); }

  ghost function zipAnd(s: seq<bool>, t: seq<bool>): seq<bool>
    requires |s| == |t|
    ensures |zipAnd(s,t)| == |s|
    decreases |s|
    ensures zipAnd(s,t) == R.zipAnd(s,t)
  { R.zipAnd(s,t) }

  ghost function flip(s: seq<bool>): seq<bool>
    ensures |flip(s)| == |s|
    decreases |s|
    ensures flip(s) == R.flip(s)
  { R.flip(s) }

  lemma {:induction false} AndValue(n: nat, a: int, b: int)
    ensures value(zipAnd(bitsNat(n,a),bitsNat(n,b))) == andNat(n,a,b)
    decreases n
  { R.AndValue(n,a,b); }

  lemma {:induction false} FlipValue(s: seq<bool>)
    ensures value(flip(s)) == P(|s|)-1-value(s)
    decreases |s|
  { R.FlipValue(s); }

  lemma {:induction false} AndView(n: nat, a: int, b: int)
    ensures bitsNat(n,andNat(n,a,b)) == zipAnd(bitsNat(n,a),bitsNat(n,b))
  { R.AndView(n,a,b); }

  lemma {:induction false} NotView(n: nat, a: int)
    ensures bitsNat(n,notNat(n,a)) == flip(bitsNat(n,a))
  { R.NotView(n,a); }

  lemma {:induction false} ZipAt(s: seq<bool>, t: seq<bool>, i: nat)
    requires |s| == |t| && i < |s|
    ensures zipAnd(s,t)[i] == (s[i] && t[i])
    ensures flip(s)[i] == !s[i]
    decreases i
  { R.ZipAt(s,t,i); }

  lemma {:induction false} BooleanBits(n: nat, a: int, b: int, i: nat)
    requires i < n
    ensures bit(n,andNat(n,a,b),i) == (bit(n,a,i) && bit(n,b,i))
    ensures bit(n,notNat(n,a),i) == !bit(n,a,i)
    ensures bit(n,orNat(n,a,b),i) == (bit(n,a,i) || bit(n,b,i))
    ensures bit(n,xorNat(n,a,b),i) == (bit(n,a,i) != bit(n,b,i))
    ensures bit(n,andNotNat(n,a,b),i) == (bit(n,a,i) && !bit(n,b,i))
  { R.BooleanBits(n,a,b,i); }

  lemma {:induction false} {:isolate_assertions} ConcatValue(s: seq<bool>, t: seq<bool>)
    ensures value(s+t) == value(s)+P(|s|)*value(t)
    decreases |s|
  { R.ConcatValue(s,t); }

  lemma {:induction false} SplitValue(s: seq<bool>, k: nat)
    requires k <= |s|
    ensures value(s)%P(k) == value(s[..k])
    ensures value(s)/P(k) == value(s[k..])
  { R.SplitValue(s,k); }

  lemma {:induction false} ZeroBits(n: nat)
    ensures value(bitsNat(n,0)) == 0
    ensures forall i | 0 <= i < n :: !bitsNat(n,0)[i]
    decreases n
  { R.ZeroBits(n); }

  lemma {:induction false} MulMod(a: int, d: int, e: int)
    requires 0 < d && 0 < e
    ensures (a*d)%(d*e) == (a%e)*d
  { R.MulMod(a,d,e); }

  lemma {:induction false} {:isolate_assertions} ShiftViews(n: nat, a: int, k: nat)
    requires k <= n
    ensures bitsNat(n,shlNat(n,a,k)) == bitsNat(k,0)+bitsNat(n,a)[..n-k]
    ensures bitsNat(n,shrNat(n,a,k)) == bitsNat(n,a)[k..]+bitsNat(k,0)
    ensures shlNat(n,a,k) < P(n) && shrNat(n,a,k) < P(n)
  { R.ShiftViews(n,a,k); }

  lemma {:induction false} ShiftBits(n: nat, a: int, k: nat, i: nat)
    requires k <= n && i < n
    ensures bit(n,shlNat(n,a,k),i) == (i >= k && bit(n,a,i-k))
    ensures bit(n,shrNat(n,a,k),i) == (i+k < n && bit(n,a,i+k))
  { R.ShiftBits(n,a,k,i); }

  lemma {:induction false} {:isolate_assertions} RotationViews(n: nat, a: int, k: int)
    requires n > 0
    ensures var r := k%n; bitsNat(n,rolNat(n,a,k)) == bitsNat(n,a)[n-r..]+bitsNat(n,a)[..n-r]
    ensures var r := k%n; bitsNat(n,rorNat(n,a,k)) == bitsNat(n,a)[r..]+bitsNat(n,a)[..r]
    ensures rolNat(n,a,k) < P(n) && rorNat(n,a,k) < P(n)
  { R.RotationViews(n,a,k); }

  lemma {:induction false} RotationIndices(n: nat, k: int, i: nat)
    requires n > 0 && i < n
    ensures (i-k)%n == (if i < k%n then i+n-k%n else i-k%n)
    ensures (i+k)%n == (if i+k%n < n then i+k%n else i+k%n-n)
  { R.RotationIndices(n,k,i); }

  lemma {:induction false} RotationBits(n: nat, a: int, k: int, i: nat)
    requires n > 0 && i < n
    ensures bit(n,rolNat(n,a,k),i) == bit(n,a,(i-k)%n)
    ensures bit(n,rorNat(n,a,k),i) == bit(n,a,(i+k)%n)
  { R.RotationBits(n,a,k,i); }

  ghost function count(s: seq<bool>): nat
    decreases |s|
    ensures count(s) == R.count(s)
  { R.count(s) }

  lemma {:induction false} PopulationView(n: nat, a: int)
    ensures popNat(n,a) == count(bitsNat(n,a))
    ensures popNat(n,a) <= n
    decreases n
  { R.PopulationView(n,a); }

  lemma {:induction false} BooleanRange(n: nat, a: int, b: int)
    ensures notNat(n,a) < P(n) && orNat(n,a,b) < P(n)
    ensures xorNat(n,a,b) < P(n) && andNotNat(n,a,b) < P(n)
  { R.BooleanRange(n,a,b); }

  lemma {:induction false} EqualByBits(n: nat, a: int, b: int)
    requires 0 <= a < P(n) && 0 <= b < P(n)
    requires bitsNat(n,a) == bitsNat(n,b)
    ensures a == b
  { R.EqualByBits(n,a,b); }

  lemma {:induction false} AndZero(n: nat, a: int)
    ensures andNat(n,a,0) == 0
    decreases n
  { R.AndZero(n,a); }

  lemma {:induction false} AndMaskLow(n: nat, a: int, k: nat)
    requires k <= n
    ensures andNat(n,a,P(k)-1) == a%P(k)
    decreases k
  { R.AndMaskLow(n,a,k); }

  lemma {:induction false} AndBound(n: nat, a: int, b: int)
    requires 0 <= a
    ensures andNat(n,a,b) <= a
    decreases n
  { R.AndBound(n,a,b); }

  lemma {:induction false} OrBound(n: nat, a: int, b: int)
    requires 0 <= a < P(n)
    ensures a <= orNat(n,a,b)
  { R.OrBound(n,a,b); }

  lemma {:induction false} XorSelf(n: nat, a: int)
    ensures xorNat(n,a,a) == 0
  { R.XorSelf(n,a); }

  lemma {:induction false} AndLowBit(n: nat, a: int)
    requires 0 < n
    ensures andNat(n,a,1) == a%2
  { R.AndLowBit(n,a); }

  lemma {:induction false} BitTest(n: nat, a: int, i: nat)
    requires i < n
    ensures (andNat(n,shrNat(n,a,i),1) == 1) == bit(n,a,i)
  { R.BitTest(n,a,i); }

  lemma {:induction false} MaskMergeBit(n: nat, a: int, b: int, m: int, i: nat)
    requires i < n
    ensures bit(n,orNat(n,andNat(n,a,m),andNat(n,b,notNat(n,m))),i)
         == (if bit(n,m,i) then bit(n,a,i) else bit(n,b,i))
  { R.MaskMergeBit(n,a,b,m,i); }

  lemma {:induction false} {:isolate_assertions} ShiftRoundDown(n: nat, a: int, k: nat)
    requires 0 <= a < P(n) && k <= n
    ensures shlNat(n,shrNat(n,a,k),k) == a-a%P(k)
  { R.ShiftRoundDown(n,a,k); }

  lemma {:induction false} {:isolate_assertions} ShiftFits(n: nat, a: int, k: nat)
    requires k <= n && 0 <= a < P(n-k)
    ensures shlNat(n,a,k) == a*P(k)
    ensures shlNat(n,a,k) < P(n)
  { R.ShiftFits(n,a,k); }

  lemma {:induction false} PopZero(n: nat)
    ensures popNat(n,0) == 0
    decreases n
  { R.PopZero(n); }

  lemma {:induction false} PopPower(n: nat, i: nat)
    requires i < n
    ensures popNat(n,P(i)) == 1
    decreases n
  { R.PopPower(n,i); }

  lemma {:induction false} RotateEndpoints(n: nat, a: int)
    ensures rolNat(n,a,0) == norm(n,a) && rolNat(n,a,n) == norm(n,a)
    ensures rorNat(n,a,0) == norm(n,a) && rorNat(n,a,n) == norm(n,a)
  { R.RotateEndpoints(n,a); }

  lemma {:induction false} ReferenceEndpoints(n: nat, a: int, k: int)
    ensures shlNat(n,a,n) == 0 && shrNat(n,a,n) == 0
    ensures shlNat(n,a,-1) == 0 && shrNat(n,a,-1) == 0
    ensures rolNat(0,a,k) == 0 && rorNat(0,a,k) == 0
    ensures bitsNat(0,a) == [] && popNat(0,a) == 0
  { R.ReferenceEndpoints(n,a,k); }

  lemma {:induction false} ModShift(a: int, b: int, m: int)
    requires 0 < m
    ensures (a%m+b)%m == (a+b)%m
  { R.ModShift(a,b,m); }

  lemma {:induction false} {:isolate_assertions} RotationComposeBit(n: nat, a: int, k: int, j: int, i: nat)
    requires 0 < n && i < n
    ensures bit(n,rolNat(n,rolNat(n,a,k),j),i) == bit(n,rolNat(n,a,k+j),i)
    ensures bit(n,rorNat(n,rorNat(n,a,k),j),i) == bit(n,rorNat(n,a,k+j),i)
  { R.RotationComposeBit(n,a,k,j,i); }

  lemma {:induction false} IndexRoundTrip(n: nat, k: int, i: nat)
    requires 0 < n && i < n
    ensures ((i-k)%n+k)%n == i
  { R.IndexRoundTrip(n,k,i); }

  lemma {:induction false} {:isolate_assertions} RotationInverseBit(n: nat, a: int, k: int, i: nat)
    requires 0 < n && i < n
    ensures bit(n,rolNat(n,rorNat(n,a,k),k),i) == bit(n,a,i)
  { R.RotationInverseBit(n,a,k,i); }

  lemma {:induction false} RotationRange(n: nat, a: int, k: int)
    requires n > 0
    ensures rolNat(n,a,k) < P(n) && rorNat(n,a,k) < P(n)
  { R.RotationRange(n,a,k); }

  lemma {:induction false} RotationAlgebra(n: nat, a: int, k: int, j: int)
    requires n > 0
    ensures rolNat(n,rolNat(n,a,k),j) == rolNat(n,a,k+j)
    ensures rorNat(n,rorNat(n,a,k),j) == rorNat(n,a,k+j)
    ensures rolNat(n,rorNat(n,a,k),k) == norm(n,a)
  { R.RotationAlgebra(n,a,k,j); }

  lemma {:induction false} CarrierRotationCounterexample()
    ensures rolNat(12,16,8) == 1
    ensures rolNat(8,16,8) == 16
  { R.CarrierRotationCounterexample(); }
}
