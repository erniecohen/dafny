// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

// Checked two's-complement interpretation; requires --integer-bitwise.
// New body qualification uses O:smt.bv.solver=2; dependencies use the default engine.
include "IntegerBitwiseNativeShifts.dfy"
module Std.Arithmetic.IntegerBitwiseNativeSigned8 {
 import B=IntegerBitwiseNative8
 import S=IntegerBitwiseNativeShifts8
 import R=IntegerBitwise
 ghost function Encode(a:int):bv8
 requires -128<=a<128
 { (if a<0 then a+256 else a) as bv8 }
 ghost function Decode(x:bv8):int
 { var u:=x as int; if u<128 then u else u-256 }
 lemma {:induction false} Range(x:bv8)
 ensures -128<=Decode(x)<128
 {}
 lemma {:induction false} RoundTrip(a:int)
 requires -128<=a<128
 ensures Decode(Encode(a))==a
 { B.RoundTrip(if a<0 then a+256 else a); }
 lemma {:induction false} Inverse(x:bv8)
 ensures Encode(Decode(x))==x
 { B.Inverse8(x); }
 lemma {:induction false} SignBit(x:bv8)
 ensures (((x>>7)&1)==1)==(Decode(x)<0)
 { B.Power(); reveal R.P(); S.NativeRight(x,7); B.DigitSplit8(x>>7); var low:=(x>>7)&1; assert low==0 || low==1; }
}
module Std.Arithmetic.IntegerBitwiseNativeSigned16 {
 import B=IntegerBitwiseNative16
 import S=IntegerBitwiseNativeShifts16
 import R=IntegerBitwise
 ghost function Encode(a:int):bv16
 requires -32768<=a<32768
 { (if a<0 then a+65536 else a) as bv16 }
 ghost function Decode(x:bv16):int
 { var u:=x as int; if u<32768 then u else u-65536 }
 lemma {:induction false} Range(x:bv16)
 ensures -32768<=Decode(x)<32768
 {}
 lemma {:induction false} RoundTrip(a:int)
 requires -32768<=a<32768
 ensures Decode(Encode(a))==a
 { B.RoundTrip(if a<0 then a+65536 else a); }
 lemma {:induction false} Inverse(x:bv16)
 ensures Encode(Decode(x))==x
 { B.Inverse16(x); }
 lemma {:induction false} SignBit(x:bv16)
 ensures (((x>>15)&1)==1)==(Decode(x)<0)
 { B.Power(); reveal R.P(); S.NativeRight(x,15); B.DigitSplit16(x>>15); var low:=(x>>15)&1; assert low==0 || low==1; }
}
module Std.Arithmetic.IntegerBitwiseNativeSigned32 {
 import B=IntegerBitwiseNative32
 import S=IntegerBitwiseNativeShifts32
 import R=IntegerBitwise
 ghost function Encode(a:int):bv32
 requires -2147483648<=a<2147483648
 { (if a<0 then a+4294967296 else a) as bv32 }
 ghost function Decode(x:bv32):int
 { var u:=x as int; if u<2147483648 then u else u-4294967296 }
 lemma {:induction false} Range(x:bv32)
 ensures -2147483648<=Decode(x)<2147483648
 {}
 lemma {:induction false} RoundTrip(a:int)
 requires -2147483648<=a<2147483648
 ensures Decode(Encode(a))==a
 { B.RoundTrip(if a<0 then a+4294967296 else a); }
 lemma {:induction false} Inverse(x:bv32)
 ensures Encode(Decode(x))==x
 { B.Inverse32(x); }
 lemma {:induction false} SignBit(x:bv32)
 ensures (((x>>31)&1)==1)==(Decode(x)<0)
 { B.Power(); reveal R.P(); S.NativeRight(x,31); B.DigitSplit32(x>>31); var low:=(x>>31)&1; assert low==0 || low==1; }
}
module Std.Arithmetic.IntegerBitwiseNativeSigned64 {
 import B=IntegerBitwiseNative64
 import S=IntegerBitwiseNativeShifts64
 import R=IntegerBitwise
 ghost function Encode(a:int):bv64
 requires -9223372036854775808<=a<9223372036854775808
 { (if a<0 then a+18446744073709551616 else a) as bv64 }
 ghost function Decode(x:bv64):int
 { var u:=x as int; if u<9223372036854775808 then u else u-18446744073709551616 }
 lemma {:induction false} Range(x:bv64)
 ensures -9223372036854775808<=Decode(x)<9223372036854775808
 {}
 lemma {:induction false} RoundTrip(a:int)
 requires -9223372036854775808<=a<9223372036854775808
 ensures Decode(Encode(a))==a
 { B.RoundTrip(if a<0 then a+18446744073709551616 else a); }
 lemma {:induction false} Inverse(x:bv64)
 ensures Encode(Decode(x))==x
 { B.Inverse64(x); }
 lemma {:induction false} SignBit(x:bv64)
 ensures (((x>>63)&1)==1)==(Decode(x)<0)
 { B.Power(); reveal R.P(); S.NativeRight(x,63); B.DigitSplit64(x>>63); var low:=(x>>63)&1; assert low==0 || low==1; }
}
