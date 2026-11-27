// RUN: %verify --integer-bitwise --standard-libraries:false --resource-limit 2000000 --verification-time-limit 60 "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-signed-shifts.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-signed-shifts.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.off.dir" --runtime > "%t.controls"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-signed-shifts.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.on.dir" --additional-axioms >> "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeSignedShifts.dfy"

import R=Std.Arithmetic.IntegerBitwise

import N8=Std.Arithmetic.IntegerBitwiseNativeSignedShifts8
import S8=Std.Arithmetic.IntegerBitwiseNativeSigned8
method Client8(x:bv8,k:nat) returns(r:bv8)
  requires k<=8
  ensures S8.Decode(r)==S8.Decode(x)/R.P(k)
{ r:=N8.Right(x,k); }

import N16=Std.Arithmetic.IntegerBitwiseNativeSignedShifts16
import S16=Std.Arithmetic.IntegerBitwiseNativeSigned16
method Client16(x:bv16,k:nat) returns(r:bv16)
  requires k<=16
  ensures S16.Decode(r)==S16.Decode(x)/R.P(k)
{ r:=N16.Right(x,k); }

import N32=Std.Arithmetic.IntegerBitwiseNativeSignedShifts32
import S32=Std.Arithmetic.IntegerBitwiseNativeSigned32
method Client32(x:bv32,k:nat) returns(r:bv32)
  requires k<=32
  ensures S32.Decode(r)==S32.Decode(x)/R.P(k)
{ r:=N32.Right(x,k); }

import N64=Std.Arithmetic.IntegerBitwiseNativeSignedShifts64
import S64=Std.Arithmetic.IntegerBitwiseNativeSigned64
method Client64(x:bv64,k:nat) returns(r:bv64)
  requires k<=64
  ensures S64.Decode(r)==S64.Decode(x)/R.P(k)
{ r:=N64.Right(x,k); }
