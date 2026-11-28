// RUN: %verify --integer-bitwise --standard-libraries:false --resource-limit 200000 --cores 2 --type-system-refresh:false --general-newtypes:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-rotation-bit-source.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --integer-bitwise --standard-libraries:false --resource-limit 200000 --cores 2 --type-system-refresh "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-rotation-bit-source.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-rotation-bit-source.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" --resolver refreshed --quick > "%t"
// RUN: %diff "%s.runtime.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
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
newtype {:bitWidth 8} {:nativeType "byte"} U8=x:nat | x<256
function RotateLeft8(a:U8,k:int):U8 requires 0<=k<=8
 ensures RotateLeft8(a,k) as int == IntegerBitwiseModel.rolNat(8,a as int,k)
 ensures RotateLeft8(a,k) as int == R.rolNat(8,a as int,k)
{ P8(); F.LeftRotate(8,a as int,k); R.RotationRange(8,a as int,k); a.RotateLeft(k) }
function RotateRight8(a:U8,k:int):U8 requires 0<=k<=8
 ensures RotateRight8(a,k) as int == IntegerBitwiseModel.rorNat(8,a as int,k)
 ensures RotateRight8(a,k) as int == R.rorNat(8,a as int,k)
{ P8(); F.RightRotate(8,a as int,k); R.RotationRange(8,a as int,k); a.RotateRight(k) }
function Bit8(a:U8,i:int):bool requires 0<=i<8
 ensures Bit8(a,i) == (IntegerBitwiseModel.shrNat(8,a as int,i)%2==1)
 ensures Bit8(a,i) == R.bit(8,a as int,i)
{ F.RightShift(8,a as int,i); R.NormalizeBits(8,a as int);
 R.BitAt(8,R.norm(8,a as int),i); reveal R.shrNat(); a.bit(i) }
newtype {:bitWidth 8} Even=x:nat | 0<x<256 && x%2==0 witness 2
function EvenBit(a:Even,i:int):bool requires 0<=i<8
 ensures EvenBit(a,i)==(IntegerBitwiseModel.shrNat(8,a as int,i)%2==1) { a.bit(i) }
function Nested(a:U8,k:int):U8 requires 0<=k<=8
 ensures Nested(a,k) == a
{ P8(); F.RightRotate(8,a as int,k); R.RotationRange(8,a as int,k);
 F.LeftRotate(8,R.rorNat(8,a as int,k),k); R.RotationRange(8,R.rorNat(8,a as int,k),k); reveal R.norm();
 R.RotationAlgebra(8,a as int,k,0); a.RotateRight(k).RotateLeft(k) }
function Named(a:U8,i:int):bool requires 0<=i<8 { a.bit(i:=i) }
lemma ExistingBV(a:bv8,k:nat) requires k<=8 {
 assert a.RotateLeft(0)==a; assert a.RotateRight(8)==a;
 assert a.RotateRight(k).RotateLeft(k)==a; }
