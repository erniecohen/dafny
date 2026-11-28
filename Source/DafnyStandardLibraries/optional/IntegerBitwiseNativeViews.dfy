// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked little-endian sequence views of native words. Requires --integer-bitwise.
include "IntegerBitwiseNativeShifts.dfy"

module Std.Arithmetic.IntegerBitwiseNativeViews8 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative8
  import S = IntegerBitwiseNativeShifts8

  opaque ghost function Bits(x:bv8):seq<bool>
    ensures |Bits(x)|==8
  { R.bitsNat(8,x as int) }

  opaque ghost function Word(s:seq<bool>):bv8
    requires |s|==8
  { B.Power(); R.ValueBound(s); R.value(s) as bv8 }

  lemma {:induction false} Observe(x:bv8)
    ensures R.value(Bits(x))==x as int
  { reveal Bits(); B.Power(); R.BitsValue(8,x as int); R.NormId(8,x as int); }

  lemma {:induction false} Build(s:seq<bool>)
    requires |s|==8
    ensures Word(s) as int==R.value(s)
  { reveal Word(); B.Power(); R.ValueBound(s); B.RoundTrip(R.value(s)); }

  lemma {:induction false} WordRoundTrip(x:bv8)
    ensures Word(Bits(x))==x
  { Observe(x); Build(Bits(x)); B.Inverse8(Word(Bits(x))); B.Inverse8(x); }

  lemma {:induction false} SequenceRoundTrip(s:seq<bool>)
    requires |s|==8
    ensures Bits(Word(s))==s
  { Build(s); reveal Bits(); R.ValueBits(s); }

  lemma {:induction false} Equality(x:bv8,y:bv8)
    ensures (x==y)==(Bits(x)==Bits(y))
  { WordRoundTrip(x); WordRoundTrip(y); }

  lemma {:induction false} Digit(x:bv8,i:nat)
    requires i<8
    ensures Bits(x)[i]==((x as int / R.P(i))%2==1)
  { reveal Bits(); R.BitAt(8,x as int,i); }

  lemma {:induction false} {:isolate_assertions} Boolean(x:bv8,y:bv8,i:nat)
    requires i<8
    ensures Bits(x&y)[i]==(Bits(x)[i] && Bits(y)[i])
    ensures Bits(!x)[i]==!Bits(x)[i]
    ensures Bits(x|y)[i]==(Bits(x)[i] || Bits(y)[i])
    ensures Bits(x^y)[i]==(Bits(x)[i] != Bits(y)[i])
    ensures Bits(x&!y)[i]==(Bits(x)[i] && !Bits(y)[i])
  {
    B.And(x,y); B.Not(x); B.Or(x,y); B.Xor(x,y); B.AndNot(x,y);
    F.And(8,x as int,y as int); F.Not(8,x as int);
    F.Or(8,x as int,y as int); F.Xor(8,x as int,y as int); F.AndNot(8,x as int,y as int);
    R.BooleanBits(8,x as int,y as int,i);
    reveal Bits();
  }

  lemma {:induction false} {:isolate_assertions} Shifts(x:bv8,k:nat,i:nat)
    requires k<=8
    requires i<8
    ensures Bits(x<<k)[i]==(i>=k && Bits(x)[i-k])
    ensures Bits(x>>k)[i]==(i+k<8 && Bits(x)[i+k])
  {
    S.LeftShift(x,k); S.RightShift(x,k);
    F.LeftShift(8,x as int,k); F.RightShift(8,x as int,k);
    R.ShiftBits(8,x as int,k,i);
    reveal Bits();
  }
}

module Std.Arithmetic.IntegerBitwiseNativeViews16 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative16
  import S = IntegerBitwiseNativeShifts16

  opaque ghost function Bits(x:bv16):seq<bool>
    ensures |Bits(x)|==16
  { R.bitsNat(16,x as int) }

  opaque ghost function Word(s:seq<bool>):bv16
    requires |s|==16
  { B.Power(); R.ValueBound(s); R.value(s) as bv16 }

  lemma {:induction false} Observe(x:bv16)
    ensures R.value(Bits(x))==x as int
  { reveal Bits(); B.Power(); R.BitsValue(16,x as int); R.NormId(16,x as int); }

  lemma {:induction false} Build(s:seq<bool>)
    requires |s|==16
    ensures Word(s) as int==R.value(s)
  { reveal Word(); B.Power(); R.ValueBound(s); B.RoundTrip(R.value(s)); }

  lemma {:induction false} WordRoundTrip(x:bv16)
    ensures Word(Bits(x))==x
  { Observe(x); Build(Bits(x)); B.Inverse16(Word(Bits(x))); B.Inverse16(x); }

  lemma {:induction false} SequenceRoundTrip(s:seq<bool>)
    requires |s|==16
    ensures Bits(Word(s))==s
  { Build(s); reveal Bits(); R.ValueBits(s); }

  lemma {:induction false} Equality(x:bv16,y:bv16)
    ensures (x==y)==(Bits(x)==Bits(y))
  { WordRoundTrip(x); WordRoundTrip(y); }

  lemma {:induction false} Digit(x:bv16,i:nat)
    requires i<16
    ensures Bits(x)[i]==((x as int / R.P(i))%2==1)
  { reveal Bits(); R.BitAt(16,x as int,i); }

  lemma {:induction false} {:isolate_assertions} Boolean(x:bv16,y:bv16,i:nat)
    requires i<16
    ensures Bits(x&y)[i]==(Bits(x)[i] && Bits(y)[i])
    ensures Bits(!x)[i]==!Bits(x)[i]
    ensures Bits(x|y)[i]==(Bits(x)[i] || Bits(y)[i])
    ensures Bits(x^y)[i]==(Bits(x)[i] != Bits(y)[i])
    ensures Bits(x&!y)[i]==(Bits(x)[i] && !Bits(y)[i])
  {
    B.And(x,y); B.Not(x); B.Or(x,y); B.Xor(x,y); B.AndNot(x,y);
    F.And(16,x as int,y as int); F.Not(16,x as int);
    F.Or(16,x as int,y as int); F.Xor(16,x as int,y as int); F.AndNot(16,x as int,y as int);
    R.BooleanBits(16,x as int,y as int,i);
    reveal Bits();
  }

  lemma {:induction false} {:isolate_assertions} Shifts(x:bv16,k:nat,i:nat)
    requires k<=16
    requires i<16
    ensures Bits(x<<k)[i]==(i>=k && Bits(x)[i-k])
    ensures Bits(x>>k)[i]==(i+k<16 && Bits(x)[i+k])
  {
    S.LeftShift(x,k); S.RightShift(x,k);
    F.LeftShift(16,x as int,k); F.RightShift(16,x as int,k);
    R.ShiftBits(16,x as int,k,i);
    reveal Bits();
  }
}

module Std.Arithmetic.IntegerBitwiseNativeViews32 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative32
  import S = IntegerBitwiseNativeShifts32

  opaque ghost function Bits(x:bv32):seq<bool>
    ensures |Bits(x)|==32
  { R.bitsNat(32,x as int) }

  opaque ghost function Word(s:seq<bool>):bv32
    requires |s|==32
  { B.Power(); R.ValueBound(s); R.value(s) as bv32 }

  lemma {:induction false} Observe(x:bv32)
    ensures R.value(Bits(x))==x as int
  { reveal Bits(); B.Power(); R.BitsValue(32,x as int); R.NormId(32,x as int); }

  lemma {:induction false} Build(s:seq<bool>)
    requires |s|==32
    ensures Word(s) as int==R.value(s)
  { reveal Word(); B.Power(); R.ValueBound(s); B.RoundTrip(R.value(s)); }

  lemma {:induction false} WordRoundTrip(x:bv32)
    ensures Word(Bits(x))==x
  { Observe(x); Build(Bits(x)); B.Inverse32(Word(Bits(x))); B.Inverse32(x); }

  lemma {:induction false} SequenceRoundTrip(s:seq<bool>)
    requires |s|==32
    ensures Bits(Word(s))==s
  { Build(s); reveal Bits(); R.ValueBits(s); }

  lemma {:induction false} Equality(x:bv32,y:bv32)
    ensures (x==y)==(Bits(x)==Bits(y))
  { WordRoundTrip(x); WordRoundTrip(y); }

  lemma {:induction false} Digit(x:bv32,i:nat)
    requires i<32
    ensures Bits(x)[i]==((x as int / R.P(i))%2==1)
  { reveal Bits(); R.BitAt(32,x as int,i); }

  lemma {:induction false} {:isolate_assertions} Boolean(x:bv32,y:bv32,i:nat)
    requires i<32
    ensures Bits(x&y)[i]==(Bits(x)[i] && Bits(y)[i])
    ensures Bits(!x)[i]==!Bits(x)[i]
    ensures Bits(x|y)[i]==(Bits(x)[i] || Bits(y)[i])
    ensures Bits(x^y)[i]==(Bits(x)[i] != Bits(y)[i])
    ensures Bits(x&!y)[i]==(Bits(x)[i] && !Bits(y)[i])
  {
    B.And(x,y); B.Not(x); B.Or(x,y); B.Xor(x,y); B.AndNot(x,y);
    F.And(32,x as int,y as int); F.Not(32,x as int);
    F.Or(32,x as int,y as int); F.Xor(32,x as int,y as int); F.AndNot(32,x as int,y as int);
    R.BooleanBits(32,x as int,y as int,i);
    reveal Bits();
  }

  lemma {:induction false} {:isolate_assertions} Shifts(x:bv32,k:nat,i:nat)
    requires k<=32
    requires i<32
    ensures Bits(x<<k)[i]==(i>=k && Bits(x)[i-k])
    ensures Bits(x>>k)[i]==(i+k<32 && Bits(x)[i+k])
  {
    S.LeftShift(x,k); S.RightShift(x,k);
    F.LeftShift(32,x as int,k); F.RightShift(32,x as int,k);
    R.ShiftBits(32,x as int,k,i);
    reveal Bits();
  }
}

module Std.Arithmetic.IntegerBitwiseNativeViews64 {
  import R = IntegerBitwise
  import F = IntegerBitwiseCompilerFoundation
  import B = IntegerBitwiseNative64
  import S = IntegerBitwiseNativeShifts64

  opaque ghost function Bits(x:bv64):seq<bool>
    ensures |Bits(x)|==64
  { R.bitsNat(64,x as int) }

  opaque ghost function Word(s:seq<bool>):bv64
    requires |s|==64
  { B.Power(); R.ValueBound(s); R.value(s) as bv64 }

  lemma {:induction false} Observe(x:bv64)
    ensures R.value(Bits(x))==x as int
  { reveal Bits(); B.Power(); R.BitsValue(64,x as int); R.NormId(64,x as int); }

  lemma {:induction false} Build(s:seq<bool>)
    requires |s|==64
    ensures Word(s) as int==R.value(s)
  { reveal Word(); B.Power(); R.ValueBound(s); B.RoundTrip(R.value(s)); }

  lemma {:induction false} WordRoundTrip(x:bv64)
    ensures Word(Bits(x))==x
  { Observe(x); Build(Bits(x)); B.Inverse64(Word(Bits(x))); B.Inverse64(x); }

  lemma {:induction false} SequenceRoundTrip(s:seq<bool>)
    requires |s|==64
    ensures Bits(Word(s))==s
  { Build(s); reveal Bits(); R.ValueBits(s); }

  lemma {:induction false} Equality(x:bv64,y:bv64)
    ensures (x==y)==(Bits(x)==Bits(y))
  { WordRoundTrip(x); WordRoundTrip(y); }

  lemma {:induction false} Digit(x:bv64,i:nat)
    requires i<64
    ensures Bits(x)[i]==((x as int / R.P(i))%2==1)
  { reveal Bits(); R.BitAt(64,x as int,i); }

  lemma {:induction false} {:isolate_assertions} Boolean(x:bv64,y:bv64,i:nat)
    requires i<64
    ensures Bits(x&y)[i]==(Bits(x)[i] && Bits(y)[i])
    ensures Bits(!x)[i]==!Bits(x)[i]
    ensures Bits(x|y)[i]==(Bits(x)[i] || Bits(y)[i])
    ensures Bits(x^y)[i]==(Bits(x)[i] != Bits(y)[i])
    ensures Bits(x&!y)[i]==(Bits(x)[i] && !Bits(y)[i])
  {
    B.And(x,y); B.Not(x); B.Or(x,y); B.Xor(x,y); B.AndNot(x,y);
    F.And(64,x as int,y as int); F.Not(64,x as int);
    F.Or(64,x as int,y as int); F.Xor(64,x as int,y as int); F.AndNot(64,x as int,y as int);
    R.BooleanBits(64,x as int,y as int,i);
    reveal Bits();
  }

  lemma {:induction false} {:isolate_assertions} Shifts(x:bv64,k:nat,i:nat)
    requires k<=64
    requires i<64
    ensures Bits(x<<k)[i]==(i>=k && Bits(x)[i-k])
    ensures Bits(x>>k)[i]==(i+k<64 && Bits(x)[i+k])
  {
    S.LeftShift(x,k); S.RightShift(x,k);
    F.LeftShift(64,x as int,k); F.RightShift(64,x as int,k);
    R.ShiftBits(64,x as int,k,i);
    reveal Bits();
  }
}
