// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-subtraction.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-subtraction.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeSubtraction.dfy"
import S8 = Std.Arithmetic.IntegerBitwiseNativeSubtraction8
lemma {:induction false} Subtraction8(x:bv8,y:bv8)
  ensures (x-y) as int==(x as int-y as int)%256
{ S8.Minus(x,y); }
import S16 = Std.Arithmetic.IntegerBitwiseNativeSubtraction16
lemma {:induction false} Subtraction16(x:bv16,y:bv16)
  ensures (x-y) as int==(x as int-y as int)%65536
{ S16.Minus(x,y); }
import S32 = Std.Arithmetic.IntegerBitwiseNativeSubtraction32
lemma {:induction false} Subtraction32(x:bv32,y:bv32)
  ensures (x-y) as int==(x as int-y as int)%4294967296
{ S32.Minus(x,y); }
import S64 = Std.Arithmetic.IntegerBitwiseNativeSubtraction64
lemma {:induction false} Subtraction64(x:bv64,y:bv64)
  ensures (x-y) as int==(x as int-y as int)%18446744073709551616
{ S64.Minus(x,y); }
