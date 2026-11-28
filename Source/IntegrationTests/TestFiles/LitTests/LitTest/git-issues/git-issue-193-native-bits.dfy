// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-bits.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-bits.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeBits.dfy"
import C = Std.Arithmetic.IntegerBitwiseCompiler
import N8 = Std.Arithmetic.IntegerBitwiseNativeBits8
lemma {:induction false} SingleBit8(x:bv8,i:nat)
 requires i<8
 ensures (((x>>i)&1)==1)==C.bit(8,x as int,i)
{ N8.BitTest(x,i); }
import N16 = Std.Arithmetic.IntegerBitwiseNativeBits16
lemma {:induction false} SingleBit16(x:bv16,i:nat)
 requires i<16
 ensures (((x>>i)&1)==1)==C.bit(16,x as int,i)
{ N16.BitTest(x,i); }
import N32 = Std.Arithmetic.IntegerBitwiseNativeBits32
lemma {:induction false} SingleBit32(x:bv32,i:nat)
 requires i<32
 ensures (((x>>i)&1)==1)==C.bit(32,x as int,i)
{ N32.BitTest(x,i); }
import N64 = Std.Arithmetic.IntegerBitwiseNativeBits64
lemma {:induction false} SingleBit64(x:bv64,i:nat)
 requires i<64
 ensures (((x>>i)&1)==1)==C.bit(64,x as int,i)
{ N64.BitTest(x,i); }
