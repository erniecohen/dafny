// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-signed.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-signed.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeSigned.dfy"
import N8=Std.Arithmetic.IntegerBitwiseNativeSigned8
lemma Sign8(x:bv8)
 ensures (((x>>7)&1)==1)==(N8.Decode(x)<0)
{ N8.SignBit(x); }
import N16=Std.Arithmetic.IntegerBitwiseNativeSigned16
lemma Sign16(x:bv16)
 ensures (((x>>15)&1)==1)==(N16.Decode(x)<0)
{ N16.SignBit(x); }
import N32=Std.Arithmetic.IntegerBitwiseNativeSigned32
lemma Sign32(x:bv32)
 ensures (((x>>31)&1)==1)==(N32.Decode(x)<0)
{ N32.SignBit(x); }
import N64=Std.Arithmetic.IntegerBitwiseNativeSigned64
lemma Sign64(x:bv64)
 ensures (((x>>63)&1)==1)==(N64.Decode(x)<0)
{ N64.SignBit(x); }
