// RUN: %exits-with 4 %verify --solver-path %review-z3 --type-system-refresh:false --general-newtypes:false --general-traits:legacy "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Equal empty maps may have different unused element expressions.
lemma Bad(k: int)
  ensures false
{
  var s: set<int> := {};
  var m1 := map x: int | x in s :: 1;
  var m2 := map x: int | x in s :: 2;
  assert m1 == m2;
  var e := if k in m1 then m1[k] else 1;
}
