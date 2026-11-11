// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Ordinary reference mathematics for
// https://github.com/erniecohen/dafny/issues/193. No compiler intrinsics or BV facts.
module Std.Arithmetic.IntegerBitwise {
  opaque ghost function P(n: nat): nat
    ensures 1 <= P(n)
    decreases n
  { if n == 0 then 1 else 2 * P(n-1) }

  ghost function norm(n: nat, a: int): nat { a % P(n) }

  opaque ghost function bitsNat(n: nat, a: int): seq<bool>
    ensures |bitsNat(n,a)| == n
    decreases n
  { if n == 0 then [] else [a % 2 == 1] + bitsNat(n-1,a/2) }

  opaque ghost function value(s: seq<bool>): nat
    decreases |s|
  { if |s| == 0 then 0 else (if s[0] then 1 else 0) + 2*value(s[1..]) }

  opaque ghost function andNat(n: nat, a: int, b: int): nat
    decreases n
  { if n == 0 then 0 else (if a%2 == 1 && b%2 == 1 then 1 else 0) + 2*andNat(n-1,a/2,b/2) }

  opaque ghost function notNat(n: nat, a: int): nat { P(n)-1-norm(n,a) }
  opaque ghost function orNat(n: nat, a: int, b: int): nat
  { notNat(n,andNat(n,notNat(n,a),notNat(n,b))) }
  opaque ghost function xorNat(n: nat, a: int, b: int): nat
  { andNat(n,orNat(n,a,b),notNat(n,andNat(n,a,b))) }
  opaque ghost function andNotNat(n: nat, a: int, b: int): nat { andNat(n,a,notNat(n,b)) }

  opaque ghost function shlNat(n: nat, a: int, k: int): nat
  { if k < 0 || n <= k then 0 else (norm(n,a)*P(k)) % P(n) }
  opaque ghost function shrNat(n: nat, a: int, k: int): nat
  { if k < 0 || n <= k then 0 else norm(n,a)/P(k) }

  opaque ghost function rolNat(n: nat, a: int, k: int): nat
  { if n == 0 then 0 else
    var r := k % n; var u := norm(n,a);
                    (u*P(r))%P(n) + u/P(n-r) }
  opaque ghost function rorNat(n: nat, a: int, k: int): nat
  { if n == 0 then 0 else
    var r := k % n; var u := norm(n,a);
                    u/P(r) + (u%P(r))*P(n-r) }

  opaque ghost function popNat(n: nat, a: int): nat
    decreases n
  { if n == 0 then 0 else a%2 + popNat(n-1,a/2) }
  ghost function bit(n: nat, a: int, i: nat): bool
    requires i < n
  { bitsNat(n,a)[i] }

  lemma {:induction false} PowerZero()
    ensures P(0) == 1
  { reveal P(); }

  lemma {:induction false} NormId(n: nat, a: int)
    requires 0 <= a < P(n)
    ensures norm(n,a) == a
  { Quotient(a,P(n),0,a); }

  lemma {:induction false} PowerSum(a: nat, b: nat)
    ensures P(a+b) == P(a)*P(b)
    decreases a
  { reveal P(); if a > 0 { PowerSum(a-1,b); } }

  lemma {:induction false} Quotient(a: int, d: int, q: int, r: int)
    requires 0 < d && a == q*d+r && 0 <= r < d
    ensures a/d == q && a%d == r
  {
    assert a == (a/d)*d+a%d;
    if a/d < q { assert (a/d)*d <= (q-1)*d; }
    else if q < a/d { assert (q+1)*d <= (a/d)*d; }
  }

  lemma {:induction false} ProductBounds(r: int, d: int, u: int, e: int)
    requires 0 < d && 0 < e && 0 <= r < d && 0 <= u < e
    ensures 0 <= r+d*u < d*e
  {
    assert u <= e-1;
    assert d*u <= d*(e-1);
  }

  lemma {:induction false} {:isolate_assertions} DivProduct(a: int, d: int, e: int)
    requires 0 < d && 0 < e
    ensures a/d/e == a/(d*e)
    ensures a%(d*e) == a%d + d*((a/d)%e)
  {
    var q, r := a/d, a%d;
    var t, u := q/e, q%e;
    assert a == t*(d*e)+(r+d*u);
    ProductBounds(r,d,u,e);
    Quotient(a,d*e,t,r+d*u);
  }

  lemma {:induction false} NormStep(n: nat, a: int)
    requires 0 < n
    ensures norm(n,a) == a%2+2*norm(n-1,a/2)
  { reveal P(); DivProduct(a,2,P(n-1)); }

  lemma {:induction false} ValueBound(s: seq<bool>)
    ensures value(s) < P(|s|)
    decreases |s|
  { reveal value(), P(); if |s| > 0 { ValueBound(s[1..]); } }

  lemma {:induction false} BitsValue(n: nat, a: int)
    ensures value(bitsNat(n,a)) == norm(n,a)
    decreases n
  {
    reveal bitsNat(), value(), P();
    if n > 0 { BitsValue(n-1,a/2); NormStep(n,a); }
  }

  lemma {:induction false} ValueBits(s: seq<bool>)
    ensures bitsNat(|s|,value(s)) == s
    decreases |s|
  {
    reveal bitsNat(), value();
    if |s| > 0 {
      ValueBits(s[1..]);
      assert value(s)/2 == value(s[1..]);
      assert s == [s[0]]+s[1..];
    }
  }

  lemma {:induction false} ValueInjective(s: seq<bool>, t: seq<bool>)
    requires |s| == |t|
    ensures (value(s) == value(t)) == (s == t)
  { ValueBits(s); ValueBits(t); }

  lemma {:induction false} NormalizeBits(n: nat, a: int)
    ensures bitsNat(n,norm(n,a)) == bitsNat(n,a)
  { BitsValue(n,a); ValueBits(bitsNat(n,a)); }

  lemma {:induction false} BitAt(n: nat, a: int, i: nat)
    requires i < n
    ensures bit(n,a,i) == ((a/P(i))%2 == 1)
    decreases i
  {
    reveal bitsNat(), P();
    if i > 0 { BitAt(n-1,a/2,i-1); DivProduct(a,2,P(i-1)); }
  }

  lemma {:induction false} AndRange(n: nat, a: int, b: int)
    ensures andNat(n,a,b) < P(n)
    decreases n
  { reveal andNat(), P(); if n > 0 { AndRange(n-1,a/2,b/2); } }

  // Boolean sequence operations are mathematical views, never runtime lowering.
  ghost function zipAnd(s: seq<bool>, t: seq<bool>): seq<bool>
    requires |s| == |t|
    ensures |zipAnd(s,t)| == |s|
    decreases |s|
  { if |s| == 0 then [] else [s[0] && t[0]]+zipAnd(s[1..],t[1..]) }

  ghost function flip(s: seq<bool>): seq<bool>
    ensures |flip(s)| == |s|
    decreases |s|
  { if |s| == 0 then [] else [!s[0]]+flip(s[1..]) }

  lemma {:induction false} AndValue(n: nat, a: int, b: int)
    ensures value(zipAnd(bitsNat(n,a),bitsNat(n,b))) == andNat(n,a,b)
    decreases n
  {
    reveal bitsNat(), value(), andNat();
    if n > 0 { AndValue(n-1,a/2,b/2); }
  }

  lemma {:induction false} FlipValue(s: seq<bool>)
    ensures value(flip(s)) == P(|s|)-1-value(s)
    decreases |s|
  { reveal value(), P(); if |s| > 0 { FlipValue(s[1..]); } }

  lemma {:induction false} AndView(n: nat, a: int, b: int)
    ensures bitsNat(n,andNat(n,a,b)) == zipAnd(bitsNat(n,a),bitsNat(n,b))
  { AndValue(n,a,b); ValueBits(zipAnd(bitsNat(n,a),bitsNat(n,b))); }

  lemma {:induction false} NotView(n: nat, a: int)
    ensures bitsNat(n,notNat(n,a)) == flip(bitsNat(n,a))
  {
    reveal notNat(); BitsValue(n,a); FlipValue(bitsNat(n,a));
    ValueBits(flip(bitsNat(n,a)));
  }

  lemma {:induction false} ZipAt(s: seq<bool>, t: seq<bool>, i: nat)
    requires |s| == |t| && i < |s|
    ensures zipAnd(s,t)[i] == (s[i] && t[i])
    ensures flip(s)[i] == !s[i]
    decreases i
  { if i > 0 { ZipAt(s[1..],t[1..],i-1); } }

  lemma {:induction false} BooleanBits(n: nat, a: int, b: int, i: nat)
    requires i < n
    ensures bit(n,andNat(n,a,b),i) == (bit(n,a,i) && bit(n,b,i))
    ensures bit(n,notNat(n,a),i) == !bit(n,a,i)
    ensures bit(n,orNat(n,a,b),i) == (bit(n,a,i) || bit(n,b,i))
    ensures bit(n,xorNat(n,a,b),i) == (bit(n,a,i) != bit(n,b,i))
    ensures bit(n,andNotNat(n,a,b),i) == (bit(n,a,i) && !bit(n,b,i))
  {
    reveal orNat(), xorNat(), andNotNat();
    AndView(n,a,b); NotView(n,a); NotView(n,b);
    ZipAt(bitsNat(n,a),bitsNat(n,b),i);
    ZipAt(bitsNat(n,b),bitsNat(n,b),i);
    AndView(n,notNat(n,a),notNat(n,b));
    ZipAt(bitsNat(n,notNat(n,a)),bitsNat(n,notNat(n,b)),i);
    NotView(n,andNat(n,notNat(n,a),notNat(n,b)));
    ZipAt(bitsNat(n,andNat(n,notNat(n,a),notNat(n,b))),bitsNat(n,a),i);
    NotView(n,andNat(n,a,b));
    ZipAt(bitsNat(n,andNat(n,a,b)),bitsNat(n,a),i);
    AndView(n,orNat(n,a,b),notNat(n,andNat(n,a,b)));
    ZipAt(bitsNat(n,orNat(n,a,b)),bitsNat(n,notNat(n,andNat(n,a,b))),i);
    AndView(n,a,notNat(n,b));
    ZipAt(bitsNat(n,a),bitsNat(n,notNat(n,b)),i);
  }
  lemma {:induction false} {:isolate_assertions} ConcatValue(s: seq<bool>, t: seq<bool>)
    ensures value(s+t) == value(s)+P(|s|)*value(t)
    decreases |s|
  {
    reveal value(), P();
    if |s| > 0 {
      ConcatValue(s[1..],t);
      assert (s+t)[1..] == s[1..]+t;
      calc {
        value(s+t);
        (if s[0] then 1 else 0)+2*value(s[1..]+t);
        (if s[0] then 1 else 0)+2*(value(s[1..])+P(|s|-1)*value(t));
        value(s)+P(|s|)*value(t);
      }
    } else { assert s == []; assert s+t == t; }
  }

  lemma {:induction false} SplitValue(s: seq<bool>, k: nat)
    requires k <= |s|
    ensures value(s)%P(k) == value(s[..k])
    ensures value(s)/P(k) == value(s[k..])
  {
    assert s == s[..k]+s[k..];
    ConcatValue(s[..k],s[k..]); ValueBound(s[..k]);
    Quotient(value(s),P(k),value(s[k..]),value(s[..k]));
  }

  lemma {:induction false} ZeroBits(n: nat)
    ensures value(bitsNat(n,0)) == 0
    ensures forall i | 0 <= i < n :: !bitsNat(n,0)[i]
    decreases n
  {
    reveal bitsNat(), value();
    if n > 0 { ZeroBits(n-1); }
  }

  lemma {:induction false} MulMod(a: int, d: int, e: int)
    requires 0 < d && 0 < e
    ensures (a*d)%(d*e) == (a%e)*d
  {
    assert a*d == (a/e)*(d*e)+(a%e)*d;
    assert 0 <= (a%e)*d < d*e;
    Quotient(a*d,d*e,a/e,(a%e)*d);
  }

  lemma {:induction false} {:isolate_assertions} ShiftViews(n: nat, a: int, k: nat)
    requires k <= n
    ensures bitsNat(n,shlNat(n,a,k)) == bitsNat(k,0)+bitsNat(n,a)[..n-k]
    ensures bitsNat(n,shrNat(n,a,k)) == bitsNat(n,a)[k..]+bitsNat(k,0)
    ensures shlNat(n,a,k) < P(n) && shrNat(n,a,k) < P(n)
  {
    reveal shlNat(), shrNat(); PowerZero();
    var s := bitsNat(n,a);
    BitsValue(n,a); ZeroBits(k);
    SplitValue(s,n-k); SplitValue(s,k);
    ConcatValue(bitsNat(k,0),s[..n-k]);
    ConcatValue(s[k..],bitsNat(k,0));
    PowerSum(k,n-k); MulMod(norm(n,a),P(k),P(n-k));
    var l, r := bitsNat(k,0)+s[..n-k], s[k..]+bitsNat(k,0);
    ValueBound(l); ValueBound(r); ValueBits(l); ValueBits(r);
    if k == n { assert s[..0] == []; assert s[n..] == []; }
    assert value(l) == shlNat(n,a,k);
    assert value(r) == shrNat(n,a,k);
  }

  lemma {:induction false} ShiftBits(n: nat, a: int, k: nat, i: nat)
    requires k <= n && i < n
    ensures bit(n,shlNat(n,a,k),i) == (i >= k && bit(n,a,i-k))
    ensures bit(n,shrNat(n,a,k),i) == (i+k < n && bit(n,a,i+k))
  {
    ShiftViews(n,a,k); ZeroBits(k);
  }

  lemma {:induction false} {:isolate_assertions} RotationViews(n: nat, a: int, k: int)
    requires n > 0
    ensures var r := k%n; bitsNat(n,rolNat(n,a,k)) == bitsNat(n,a)[n-r..]+bitsNat(n,a)[..n-r]
    ensures var r := k%n; bitsNat(n,rorNat(n,a,k)) == bitsNat(n,a)[r..]+bitsNat(n,a)[..r]
    ensures rolNat(n,a,k) < P(n) && rorNat(n,a,k) < P(n)
  {
    reveal rolNat(), rorNat();
    var s, r := bitsNat(n,a), k%n;
    BitsValue(n,a); SplitValue(s,n-r); SplitValue(s,r);
    ConcatValue(s[n-r..],s[..n-r]);
    ConcatValue(s[r..],s[..r]);
    PowerSum(r,n-r); MulMod(norm(n,a),P(r),P(n-r));
    var l, t := s[n-r..]+s[..n-r], s[r..]+s[..r];
    ValueBound(l); ValueBound(t); ValueBits(l); ValueBits(t);
    assert value(l) == rolNat(n,a,k);
    assert value(t) == rorNat(n,a,k);
  }

  lemma {:induction false} RotationIndices(n: nat, k: int, i: nat)
    requires n > 0 && i < n
    ensures (i-k)%n == (if i < k%n then i+n-k%n else i-k%n)
    ensures (i+k)%n == (if i+k%n < n then i+k%n else i+k%n-n)
  {
    var q, r := k/n, k%n;
    Quotient(i-k,n,-q-(if i<r then 1 else 0),if i<r then i+n-r else i-r);
    Quotient(i+k,n,q+(if i+r<n then 0 else 1),if i+r<n then i+r else i+r-n);
  }

  lemma {:induction false} RotationBits(n: nat, a: int, k: int, i: nat)
    requires n > 0 && i < n
    ensures bit(n,rolNat(n,a,k),i) == bit(n,a,(i-k)%n)
    ensures bit(n,rorNat(n,a,k),i) == bit(n,a,(i+k)%n)
  { RotationViews(n,a,k); RotationIndices(n,k,i); }

  ghost function count(s: seq<bool>): nat
    decreases |s|
  { if |s| == 0 then 0 else (if s[0] then 1 else 0)+count(s[1..]) }

  lemma {:induction false} PopulationView(n: nat, a: int)
    ensures popNat(n,a) == count(bitsNat(n,a))
    ensures popNat(n,a) <= n
    decreases n
  {
    reveal popNat(), bitsNat();
    if n > 0 { PopulationView(n-1,a/2); }
  }

  lemma {:induction false} BooleanRange(n: nat, a: int, b: int)
    ensures notNat(n,a) < P(n) && orNat(n,a,b) < P(n)
    ensures xorNat(n,a,b) < P(n) && andNotNat(n,a,b) < P(n)
  {
    reveal notNat(), orNat(), xorNat(), andNotNat();
    AndRange(n,a,b); AndRange(n,notNat(n,a),notNat(n,b));
    AndRange(n,orNat(n,a,b),notNat(n,andNat(n,a,b)));
    AndRange(n,a,notNat(n,b));
  }

  lemma {:induction false} EqualByBits(n: nat, a: int, b: int)
    requires 0 <= a < P(n) && 0 <= b < P(n)
    requires bitsNat(n,a) == bitsNat(n,b)
    ensures a == b
  { NormId(n,a); NormId(n,b); BitsValue(n,a); BitsValue(n,b); }

  lemma {:induction false} AndZero(n: nat, a: int)
    ensures andNat(n,a,0) == 0
    decreases n
  { reveal andNat(); if n > 0 { AndZero(n-1,a/2); } }

  lemma {:induction false} AndMaskLow(n: nat, a: int, k: nat)
    requires k <= n
    ensures andNat(n,a,P(k)-1) == a%P(k)
    decreases k
  {
    reveal P(), andNat();
    if k == 0 { AndZero(n,a); }
    else {
      AndMaskLow(n-1,a/2,k-1); NormStep(k,a);
      assert (P(k)-1)/2 == P(k-1)-1;
      assert (P(k)-1)%2 == 1;
    }
  }

  lemma {:induction false} AndBound(n: nat, a: int, b: int)
    requires 0 <= a
    ensures andNat(n,a,b) <= a
    decreases n
  { reveal andNat(); if n > 0 { AndBound(n-1,a/2,b/2); } }

  lemma {:induction false} OrBound(n: nat, a: int, b: int)
    requires 0 <= a < P(n)
    ensures a <= orNat(n,a,b)
  {
    reveal orNat(), notNat();
    AndBound(n,notNat(n,a),notNat(n,b));
    AndRange(n,notNat(n,a),notNat(n,b));
    NormId(n,a); NormId(n,andNat(n,notNat(n,a),notNat(n,b)));
  }

  lemma {:induction false} XorSelf(n: nat, a: int)
    ensures xorNat(n,a,a) == 0
  {
    BooleanRange(n,a,a); ZeroBits(n);
    forall i | 0 <= i < n
      ensures bitsNat(n,xorNat(n,a,a))[i] == bitsNat(n,0)[i]
    { BooleanBits(n,a,a,i); }
    assert bitsNat(n,xorNat(n,a,a)) == bitsNat(n,0);
    EqualByBits(n,xorNat(n,a,a),0);
  }

  lemma {:induction false} AndLowBit(n: nat, a: int)
    requires 0 < n
    ensures andNat(n,a,1) == a%2
  { reveal andNat(); AndZero(n-1,a/2); }

  lemma {:induction false} BitTest(n: nat, a: int, i: nat)
    requires i < n
    ensures (andNat(n,shrNat(n,a,i),1) == 1) == bit(n,a,i)
  {
    reveal shrNat(); AndLowBit(n,shrNat(n,a,i));
    NormalizeBits(n,a); BitAt(n,norm(n,a),i);
  }

  lemma {:induction false} MaskMergeBit(n: nat, a: int, b: int, m: int, i: nat)
    requires i < n
    ensures bit(n,orNat(n,andNat(n,a,m),andNat(n,b,notNat(n,m))),i)
         == (if bit(n,m,i) then bit(n,a,i) else bit(n,b,i))
  {
    BooleanBits(n,a,m,i); BooleanBits(n,b,m,i); BooleanBits(n,m,m,i);
    BooleanBits(n,b,notNat(n,m),i);
    BooleanBits(n,andNat(n,a,m),andNat(n,b,notNat(n,m)),i);
  }

  lemma {:induction false} {:isolate_assertions} ShiftRoundDown(n: nat, a: int, k: nat)
    requires 0 <= a < P(n) && k <= n
    ensures shlNat(n,shrNat(n,a,k),k) == a-a%P(k)
  {
    NormId(n,a); PowerZero();
    if k == n {
      reveal shlNat(), shrNat();
    } else {
      var q := a/P(k);
      assert 0 <= q < P(n-k) by {
        PowerSum(k,n-k); DivProduct(a,P(k),P(n-k));
        assert a/P(n) == 0;
        assert q/P(n-k) == 0;
      }
      ShiftFits(n,q,k);
      reveal shrNat();
      assert shrNat(n,a,k) == q;
      assert q*P(k) == a-a%P(k);
    }
  }

  lemma {:induction false} {:isolate_assertions} ShiftFits(n: nat, a: int, k: nat)
    requires k <= n && 0 <= a < P(n-k)
    ensures shlNat(n,a,k) == a*P(k)
    ensures shlNat(n,a,k) < P(n)
  {
    PowerZero(); PowerSum(k,n-k);
    ProductBounds(0,P(k),a,P(n-k));
    assert 0 <= a*P(k) < P(n);
    assert a < P(n);
    NormId(n,a);
    Quotient(a*P(k),P(n),0,a*P(k));
    if k == n { assert a == 0; }
    reveal shlNat();
  }

  lemma {:induction false} PopZero(n: nat)
    ensures popNat(n,0) == 0
    decreases n
  { reveal popNat(); if n > 0 { PopZero(n-1); } }

  lemma {:induction false} PopPower(n: nat, i: nat)
    requires i < n
    ensures popNat(n,P(i)) == 1
    decreases n
  {
    reveal popNat(), P();
    if i == 0 { PopZero(n-1); }
    else { PopPower(n-1,i-1); }
  }

  lemma {:induction false} RotateEndpoints(n: nat, a: int)
    ensures rolNat(n,a,0) == norm(n,a) && rolNat(n,a,n) == norm(n,a)
    ensures rorNat(n,a,0) == norm(n,a) && rorNat(n,a,n) == norm(n,a)
  { reveal rolNat(), rorNat(); PowerZero(); }

  lemma {:induction false} ReferenceEndpoints(n: nat, a: int, k: int)
    ensures shlNat(n,a,n) == 0 && shrNat(n,a,n) == 0
    ensures shlNat(n,a,-1) == 0 && shrNat(n,a,-1) == 0
    ensures rolNat(0,a,k) == 0 && rorNat(0,a,k) == 0
    ensures bitsNat(0,a) == [] && popNat(0,a) == 0
  { reveal shlNat(), shrNat(), rolNat(), rorNat(), bitsNat(), popNat(); }

  lemma {:induction false} ModShift(a: int, b: int, m: int)
    requires 0 < m
    ensures (a%m+b)%m == (a+b)%m
  {
    var q, r := (a%m+b)/m, (a%m+b)%m;
    assert a+b == (a/m+q)*m+r;
    Quotient(a+b,m,a/m+q,r);
  }

  lemma {:induction false} {:isolate_assertions} RotationComposeBit(n: nat, a: int, k: int, j: int, i: nat)
    requires 0 < n && i < n
    ensures bit(n,rolNat(n,rolNat(n,a,k),j),i) == bit(n,rolNat(n,a,k+j),i)
    ensures bit(n,rorNat(n,rorNat(n,a,k),j),i) == bit(n,rorNat(n,a,k+j),i)
  {
    RotationBits(n,rolNat(n,a,k),j,i); RotationBits(n,a,k,(i-j)%n);
    RotationBits(n,rorNat(n,a,k),j,i); RotationBits(n,a,k,(i+j)%n);
    RotationBits(n,a,k+j,i);
    ModShift(i-j,-k,n); ModShift(i+j,k,n);
  }

  lemma {:induction false} IndexRoundTrip(n: nat, k: int, i: nat)
    requires 0 < n && i < n
    ensures ((i-k)%n+k)%n == i
  { ModShift(i-k,k,n); Quotient(i,n,0,i); }

  lemma {:induction false} {:isolate_assertions} RotationInverseBit(n: nat, a: int, k: int, i: nat)
    requires 0 < n && i < n
    ensures bit(n,rolNat(n,rorNat(n,a,k),k),i) == bit(n,a,i)
  {
    RotationBits(n,rorNat(n,a,k),k,i); RotationBits(n,a,k,(i-k)%n);
    IndexRoundTrip(n,k,i);
  }

  lemma {:induction false} RotationRange(n: nat, a: int, k: int)
    requires n > 0
    ensures rolNat(n,a,k) < P(n) && rorNat(n,a,k) < P(n)
  { RotationViews(n,a,k); }

  lemma {:induction false} RotationAlgebra(n: nat, a: int, k: int, j: int)
    requires n > 0
    ensures rolNat(n,rolNat(n,a,k),j) == rolNat(n,a,k+j)
    ensures rorNat(n,rorNat(n,a,k),j) == rorNat(n,a,k+j)
    ensures rolNat(n,rorNat(n,a,k),k) == norm(n,a)
  {
    forall i | 0 <= i < n
      ensures bitsNat(n,rolNat(n,rolNat(n,a,k),j))[i] == bitsNat(n,rolNat(n,a,k+j))[i]
      ensures bitsNat(n,rorNat(n,rorNat(n,a,k),j))[i] == bitsNat(n,rorNat(n,a,k+j))[i]
      ensures bitsNat(n,rolNat(n,rorNat(n,a,k),k))[i] == bitsNat(n,a)[i]
    { RotationComposeBit(n,a,k,j,i); RotationInverseBit(n,a,k,i); }
    assert bitsNat(n,rolNat(n,rolNat(n,a,k),j)) == bitsNat(n,rolNat(n,a,k+j));
    assert bitsNat(n,rorNat(n,rorNat(n,a,k),j)) == bitsNat(n,rorNat(n,a,k+j));
    assert bitsNat(n,rolNat(n,rorNat(n,a,k),k)) == bitsNat(n,a);
    RotationRange(n,a,k+j); RotationRange(n,rolNat(n,a,k),j);
    RotationRange(n,rorNat(n,a,k),j); RotationRange(n,rorNat(n,a,k),k);
    NormalizeBits(n,a);
    EqualByBits(n,rolNat(n,rolNat(n,a,k),j),rolNat(n,a,k+j));
    EqualByBits(n,rorNat(n,rorNat(n,a,k),j),rorNat(n,a,k+j));
    EqualByBits(n,rolNat(n,rorNat(n,a,k),k),norm(n,a));
  }

  lemma {:induction false} CarrierRotationCounterexample()
    ensures rolNat(12,16,8) == 1
    ensures rolNat(8,16,8) == 16
  {
    reveal rolNat(), P();
    assert P(0) == 1;
    assert P(4) == 16 by { assert P(2) == 4; }
    assert P(8) == 256 by { PowerSum(4,4); }
    assert P(12) == 4096 by { PowerSum(8,4); }
  }

}
