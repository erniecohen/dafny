// RUN: %verify --integer-bitwise --standard-libraries:false --resource-limit 2000000 --verification-time-limit 60 "%repositoryRoot/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-193-native-views.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-views.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.off.dir" > "%t.controls"
// RUN: python3 "%repositoryRoot/.github/review/issue193-native-views.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.on.dir" --additional-axioms >> "%t.controls"
// RUN: %diff "%s.controls.expect" "%t.controls"
// https://github.com/erniecohen/dafny/issues/193
include "../../../../../DafnyStandardLibraries/optional/IntegerBitwiseNativeViews.dfy"

import V8=Std.Arithmetic.IntegerBitwiseNativeViews8
lemma Client8(x:bv8) {
  V8.WordRoundTrip(x);
  assert V8.Word(V8.Bits(x))==x;
}

import V16=Std.Arithmetic.IntegerBitwiseNativeViews16
lemma Client16(x:bv16) {
  V16.WordRoundTrip(x);
  assert V16.Word(V16.Bits(x))==x;
}

import V32=Std.Arithmetic.IntegerBitwiseNativeViews32
lemma Client32(x:bv32) {
  V32.WordRoundTrip(x);
  assert V32.Word(V32.Bits(x))==x;
}

import V64=Std.Arithmetic.IntegerBitwiseNativeViews64
lemma Client64(x:bv64) {
  V64.WordRoundTrip(x);
  assert V64.Word(V64.Bits(x))==x;
}
