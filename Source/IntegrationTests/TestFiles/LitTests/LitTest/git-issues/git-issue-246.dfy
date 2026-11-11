// https://github.com/erniecohen/dafny/issues/246
// RUN: %baredafny run --no-verify --target:js --standard-libraries:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target:js --standard-libraries:false --type-system-refresh --general-newtypes --general-traits:datatype "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: node "%repositoryRoot/.github/review/246-js-bitwise.cjs" "%repositoryRoot/Source/DafnyRuntime/DafnyRuntimeJs/DafnyRuntime.js" > "%t.helpers"

method Main() {
  Check32((2147483649) as bv32, (4294967295) as bv32);
  Check33((4294967297) as bv33, (8589934591) as bv33);
  Check53((4503599627370497) as bv53, (9007199254740991) as bv53);
  Check64((9223372036854775809) as bv64, (18446744073709551615) as bv64);
  Check65((18446744073709551617) as bv65, (36893488147419103231) as bv65);
}

method Check32(x: bv32, y: bv32) {
  expect (x & x) == x;
  expect (x | 0) == x;
  expect (x ^ 0) == x;
  expect (x ^ x) == 0;
  expect (x & y) == x;
  expect (x | y) == y;
  expect (x ^ y) == 2147483646;
  expect (0 & x) == 0;
  expect (0 | x) == x;
  expect (0 ^ x) == x;
  print "bv32 passed\n";
}

method Check33(x: bv33, y: bv33) {
  expect (x & x) == x;
  expect (x | 0) == x;
  expect (x ^ 0) == x;
  expect (x ^ x) == 0;
  expect (x & y) == x;
  expect (x | y) == y;
  expect (x ^ y) == 4294967294;
  expect (0 & x) == 0;
  expect (0 | x) == x;
  expect (0 ^ x) == x;
  print "bv33 passed\n";
}

method Check53(x: bv53, y: bv53) {
  expect (x & x) == x;
  expect (x | 0) == x;
  expect (x ^ 0) == x;
  expect (x ^ x) == 0;
  expect (x & y) == x;
  expect (x | y) == y;
  expect (x ^ y) == 4503599627370494;
  expect (0 & x) == 0;
  expect (0 | x) == x;
  expect (0 ^ x) == x;
  print "bv53 passed\n";
}

method Check64(x: bv64, y: bv64) {
  expect (x & x) == x;
  expect (x | 0) == x;
  expect (x ^ 0) == x;
  expect (x ^ x) == 0;
  expect (x & y) == x;
  expect (x | y) == y;
  expect (x ^ y) == 9223372036854775806;
  expect (0 & x) == 0;
  expect (0 | x) == x;
  expect (0 ^ x) == x;
  print "bv64 passed\n";
}

method Check65(x: bv65, y: bv65) {
  expect (x & x) == x;
  expect (x | 0) == x;
  expect (x ^ 0) == x;
  expect (x ^ x) == 0;
  expect (x & y) == x;
  expect (x | y) == y;
  expect (x ^ y) == 18446744073709551614;
  expect (0 & x) == 0;
  expect (0 | x) == x;
  expect (0 ^ x) == x;
  print "bv65 passed\n";
}
