// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype NonEmpty = s: seq<int> | |s| > 0 witness [0]
method Test() { var x := [1] as NonEmpty; assert |x| == 1; }
