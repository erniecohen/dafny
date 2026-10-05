// RUN: %testDafnyForEachCompiler --refresh-exit-code=0 "%s" -- --type-system-refresh=true --general-newtypes=true
newtype CharView = char
newtype WideView = bv22
method Main() {
  var supplementary := ((0x10000 as bv22) as WideView) as CharView;
  var largest := (0x10FFFF as ORDINAL) as CharView;
  var ascii := ('A' as CharView) as bv8;
  expect (supplementary as int) == 0x10000;
  expect (largest as int) == 0x10FFFF;
  expect ascii == 65;
  print supplementary as int, " ", largest as int, " ", ascii as int, "\n";
}
