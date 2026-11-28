// RUN: %verify --cores 2 --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-rotations.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-rotations.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" --runtime > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeRotations.dfy"
import H8 = Std.Arithmetic.IntegerBitwiseNativeRotations8
lemma {:induction false} Left8(x:bv8,k:nat)
  requires k<=8
  ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(8,x as int,k)
{ H8.LeftRotate(x,k); }
lemma {:induction false} Right8(x:bv8,k:nat)
  requires k<=8
  ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(8,x as int,k)
{ H8.RightRotate(x,k); }
import H16 = Std.Arithmetic.IntegerBitwiseNativeRotations16
lemma {:induction false} Left16(x:bv16,k:nat)
  requires k<=16
  ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(16,x as int,k)
{ H16.LeftRotate(x,k); }
lemma {:induction false} Right16(x:bv16,k:nat)
  requires k<=16
  ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(16,x as int,k)
{ H16.RightRotate(x,k); }
import H32 = Std.Arithmetic.IntegerBitwiseNativeRotations32
lemma {:induction false} Left32(x:bv32,k:nat)
  requires k<=32
  ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(32,x as int,k)
{ H32.LeftRotate(x,k); }
lemma {:induction false} Right32(x:bv32,k:nat)
  requires k<=32
  ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(32,x as int,k)
{ H32.RightRotate(x,k); }
import H64 = Std.Arithmetic.IntegerBitwiseNativeRotations64
lemma {:induction false} Left64(x:bv64,k:nat)
  requires k<=64
  ensures x.RotateLeft(k) as int==IntegerBitwiseModel.rolNat(64,x as int,k)
{ H64.LeftRotate(x,k); }
lemma {:induction false} Right64(x:bv64,k:nat)
  requires k<=64
  ensures x.RotateRight(k) as int==IntegerBitwiseModel.rorNat(64,x as int,k)
{ H64.RightRotate(x,k); }
