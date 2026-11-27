// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-order.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-order.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeOrder.dfy"
import N8 = Std.Arithmetic.IntegerBitwiseNativeOrder8
lemma {:induction false} UnsignedComparison8(x:bv8,y:bv8)
 ensures (x==y)==(x as int==y as int)
 ensures (x<y)==(x as int<y as int)
 ensures (x<=y)==(x as int<=y as int)
{ N8.Equal(x,y); N8.Less(x,y); N8.LessEqual(x,y); }
import N16 = Std.Arithmetic.IntegerBitwiseNativeOrder16
lemma {:induction false} UnsignedComparison16(x:bv16,y:bv16)
 ensures (x==y)==(x as int==y as int)
 ensures (x<y)==(x as int<y as int)
 ensures (x<=y)==(x as int<=y as int)
{ N16.Equal(x,y); N16.Less(x,y); N16.LessEqual(x,y); }
import N32 = Std.Arithmetic.IntegerBitwiseNativeOrder32
lemma {:induction false} UnsignedComparison32(x:bv32,y:bv32)
 ensures (x==y)==(x as int==y as int)
 ensures (x<y)==(x as int<y as int)
 ensures (x<=y)==(x as int<=y as int)
{ N32.Equal(x,y); N32.Less(x,y); N32.LessEqual(x,y); }
import N64 = Std.Arithmetic.IntegerBitwiseNativeOrder64
lemma {:induction false} UnsignedComparison64(x:bv64,y:bv64)
 ensures (x==y)==(x as int==y as int)
 ensures (x<y)==(x as int<y as int)
 ensures (x<=y)==(x as int<=y as int)
{ N64.Equal(x,y); N64.Less(x,y); N64.LessEqual(x,y); }
