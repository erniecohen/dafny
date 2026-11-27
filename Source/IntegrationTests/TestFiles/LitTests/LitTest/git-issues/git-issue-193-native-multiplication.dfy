// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-multiplication.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-multiplication.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeMultiplication.dfy"
import S8 = Std.Arithmetic.IntegerBitwiseNativeMultiplication8
lemma {:induction false} Multiplication8(x:bv8,y:bv8)
  ensures (x*y) as int==(x as int*y as int)%256
{ S8.Times(x,y); }
import S16 = Std.Arithmetic.IntegerBitwiseNativeMultiplication16
lemma {:induction false} Multiplication16(x:bv16,y:bv16)
  ensures (x*y) as int==(x as int*y as int)%65536
{ S16.Times(x,y); }
import S32 = Std.Arithmetic.IntegerBitwiseNativeMultiplication32
lemma {:induction false} Multiplication32(x:bv32,y:bv32)
  ensures (x*y) as int==(x as int*y as int)%4294967296
{ S32.Times(x,y); }
import S64 = Std.Arithmetic.IntegerBitwiseNativeMultiplication64
lemma {:induction false} Multiplication64(x:bv64,y:bv64)
  ensures (x*y) as int==(x as int*y as int)%18446744073709551616
{ S64.Times(x,y); }
