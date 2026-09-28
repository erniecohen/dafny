// RUN: %baredafny resolve --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny resolve --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

method test(x: int) {
  var b := x - 1 decreases to x;
  assert b;
}
method Explicit(x: int) {
  ghost var b := x - 1 decreases to x;
  assert b;
}
lemma Local(x: int) {
  var b := x - 1 decreases to x;
  assert b;
}
method Control(x: int) {
  assert x - 1 decreases to x;
}
method Nonincreases(x: int) {
  var b := x nonincreases to x;
  assert b;
}
