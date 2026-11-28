// RUN: %verify --standard-libraries:false --integer-bitwise --additional-axioms:false "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-population.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-population.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" --runtime > "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativePopulation.dfy"
import N8=Std.Arithmetic.IntegerBitwiseNativePopulation8
method Count8(x:bv8) returns(c:bv8)
 ensures c as int<=8
{ c:=N8.Count(x); }
import N16=Std.Arithmetic.IntegerBitwiseNativePopulation16
method Count16(x:bv16) returns(c:bv8)
 ensures c as int<=16
{ c:=N16.Count(x); }
import N32=Std.Arithmetic.IntegerBitwiseNativePopulation32
method Count32(x:bv32) returns(c:bv8)
 ensures c as int<=32
{ c:=N32.Count(x); }
import N64=Std.Arithmetic.IntegerBitwiseNativePopulation64
method Count64(x:bv64) returns(c:bv8)
 ensures c as int<=64
{ c:=N64.Count(x); }
