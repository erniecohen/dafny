// RUN: %exits-with 4 %verify --solver-path %review-z3 --type-system-refresh:false --general-newtypes:false --general-traits:legacy "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --solver-path %review-z3 --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// No call permission may escape a lambda whose formal type is empty.
type Empty = x: int | false witness *
function Never(): bool requires false ensures false { false }
lemma ViaLambda() ensures false {
  var f := (x: Empty) => Never();
  assert Never();
}
