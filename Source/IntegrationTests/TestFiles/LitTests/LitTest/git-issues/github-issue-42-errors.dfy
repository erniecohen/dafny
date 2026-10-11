// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --show-snippets:false --use-basename-for-filename "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --show-snippets:false --use-basename-for-filename "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

method InferredGhost(x: int) returns (c: bool) {
  var b := x - 1 decreases to x;
  c := b;
}
method NonGhost(x: int) {
  expect x - 1 decreases to x;
}
