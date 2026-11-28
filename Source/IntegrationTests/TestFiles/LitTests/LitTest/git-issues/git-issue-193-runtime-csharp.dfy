// RUN: %verify --integer-bitwise --standard-libraries:false --resource-limit 200000 "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-runtime-csharp.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-runtime-csharp.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" --resolver refreshed --quick > "%t"
// RUN: %diff "%s.runtime.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
// The same checked source operations execute with semantic-width masks and count endpoints.
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseCompilerFoundation.dfy"
import F = Std.Arithmetic.IntegerBitwiseCompilerFoundation
import R = Std.Arithmetic.IntegerBitwise
lemma P0() ensures R.P(0) == 1
 ensures IntegerBitwiseModel.P(0) == 1 {
 F.Power(0); IntegerBitwiseModel.unfoldP(0);
}
lemma P1() ensures R.P(1) == 2
 ensures IntegerBitwiseModel.P(1) == 2 {
 P0();
 F.Power(1); IntegerBitwiseModel.unfoldP(1);
}
lemma P2() ensures R.P(2) == 4
 ensures IntegerBitwiseModel.P(2) == 4 {
 P1();
 F.Power(2); IntegerBitwiseModel.unfoldP(2);
}
lemma P3() ensures R.P(3) == 8
 ensures IntegerBitwiseModel.P(3) == 8 {
 P2();
 F.Power(3); IntegerBitwiseModel.unfoldP(3);
}
lemma P4() ensures R.P(4) == 16
 ensures IntegerBitwiseModel.P(4) == 16 {
 P3();
 F.Power(4); IntegerBitwiseModel.unfoldP(4);
}
lemma P5() ensures R.P(5) == 32
 ensures IntegerBitwiseModel.P(5) == 32 {
 P4();
 F.Power(5); IntegerBitwiseModel.unfoldP(5);
}
lemma P6() ensures R.P(6) == 64
 ensures IntegerBitwiseModel.P(6) == 64 {
 P5();
 F.Power(6); IntegerBitwiseModel.unfoldP(6);
}
lemma P7() ensures R.P(7) == 128
 ensures IntegerBitwiseModel.P(7) == 128 {
 P6();
 F.Power(7); IntegerBitwiseModel.unfoldP(7);
}
lemma P8() ensures R.P(8) == 256
 ensures IntegerBitwiseModel.P(8) == 256 {
 P7();
 F.Power(8); IntegerBitwiseModel.unfoldP(8);
}
newtype {:bitWidth 8} {:nativeType "byte"} U8 = x:nat | x < 256
function And8(a:U8, b:U8):U8
 ensures And8(a,b) as int == IntegerBitwiseModel.andNat(8,a as int,b as int)
{
 P8(); F.And(8,a as int,b as int);
 R.AndRange(8,a as int,b as int);
 a & b
}
function Or8(a:U8, b:U8):U8
 ensures Or8(a,b) as int == IntegerBitwiseModel.orNat(8,a as int,b as int)
{
 P8(); F.Or(8,a as int,b as int);
 R.BooleanRange(8,a as int,b as int);
 a | b
}
function Xor8(a:U8, b:U8):U8
 ensures Xor8(a,b) as int == IntegerBitwiseModel.xorNat(8,a as int,b as int)
{
 P8(); F.Xor(8,a as int,b as int);
 R.BooleanRange(8,a as int,b as int);
 a ^ b
}
function Not8(a:U8):U8
 ensures Not8(a) as int == IntegerBitwiseModel.notNat(8,a as int)
{
 P8(); F.Not(8,a as int);
 R.BooleanRange(8,a as int,a as int);
 !a
}
function LeftShift8(a:U8, k:nat):U8
 requires k <= 8
 ensures LeftShift8(a,k) as int == IntegerBitwiseModel.shlNat(8,a as int,k)
{
 P8(); F.LeftShift(8,a as int,k);
 R.ShiftViews(8,a as int,k);
 a << k
}
function RightShift8(a:U8, k:nat):U8
 requires k <= 8
 ensures RightShift8(a,k) as int == IntegerBitwiseModel.shrNat(8,a as int,k)
{
 P8(); F.RightShift(8,a as int,k);
 R.ShiftViews(8,a as int,k);
 a >> k
}
