// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-division.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-division.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeDivision.dfy"
import D8 = Std.Arithmetic.IntegerBitwiseNativeDivision8
lemma {:induction false} UnsignedDivision8(x:bv8,y:bv8)
  requires y!=0
  ensures (x/y) as int==(x as int)/(y as int)
  ensures (x%y) as int==(x as int)%(y as int)
{ D8.Divide(x,y); D8.Remainder(x,y); }
import D16 = Std.Arithmetic.IntegerBitwiseNativeDivision16
lemma {:induction false} UnsignedDivision16(x:bv16,y:bv16)
  requires y!=0
  ensures (x/y) as int==(x as int)/(y as int)
  ensures (x%y) as int==(x as int)%(y as int)
{ D16.Divide(x,y); D16.Remainder(x,y); }
import D32 = Std.Arithmetic.IntegerBitwiseNativeDivision32
lemma {:induction false} UnsignedDivision32(x:bv32,y:bv32)
  requires y!=0
  ensures (x/y) as int==(x as int)/(y as int)
  ensures (x%y) as int==(x as int)%(y as int)
{ D32.Divide(x,y); D32.Remainder(x,y); }
import D64 = Std.Arithmetic.IntegerBitwiseNativeDivision64
lemma {:induction false} UnsignedDivision64(x:bv64,y:bv64)
  requires y!=0
  ensures (x/y) as int==(x as int)/(y as int)
  ensures (x%y) as int==(x as int)%(y as int)
{ D64.Divide(x,y); D64.Remainder(x,y); }
