// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
include "IntegerBitwise.dfy"

module Std.Arithmetic.IntegerBitwiseBase2 refines LittleEndianNat {
  function BASE(): nat { 2 }
}

module Std.Arithmetic.IntegerBitwiseDigits {
  import B = IntegerBitwise
  import D = IntegerBitwiseBase2
  import Power

  ghost function toDigits(s: seq<bool>): seq<D.digit>
    ensures |toDigits(s)| == |s|
    decreases |s|
  { if |s| == 0 then [] else [if s[0] then 1 else 0]+toDigits(s[1..]) }

  ghost function fromDigits(s: seq<D.digit>): seq<bool>
    ensures |fromDigits(s)| == |s|
    decreases |s|
  { if |s| == 0 then [] else [s[0] == 1]+fromDigits(s[1..]) }

  lemma {:induction false} PowerAgreement(n: nat)
    ensures B.P(n) == Power.Pow(2,n)
    decreases n
  { reveal B.P(); if n > 0 { PowerAgreement(n-1); } }

  lemma {:induction false} DigitInverses(s: seq<bool>, t: seq<D.digit>)
    ensures fromDigits(toDigits(s)) == s
    ensures toDigits(fromDigits(t)) == t
    decreases |s|+|t|
  {
    if |s| > 0 { DigitInverses(s[1..],[]); assert s == [s[0]]+s[1..]; }
    if |t| > 0 { DigitInverses([],t[1..]); assert t == [t[0]]+t[1..]; }
  }

  lemma {:induction false} DigitsValue(s: seq<bool>)
    ensures D.ToNatRight(toDigits(s)) == B.value(s)
    decreases |s|
  {
    reveal B.value();
    if |s| > 0 { DigitsValue(s[1..]); }
  }

  lemma {:induction false} BaseTwoAdapter(n: nat, a: int)
    ensures (PowerAgreement(n); toDigits(B.bitsNat(n,a)) == D.FromNatWithLen(B.norm(n,a),n))
  {
    PowerAgreement(n); B.BitsValue(n,a);
    DigitsValue(B.bitsNat(n,a)); B.ValueBound(B.bitsNat(n,a));
    D.LemmaSeqNatSeq(toDigits(B.bitsNat(n,a)));
  }
}
