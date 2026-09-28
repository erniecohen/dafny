// Success controls invoked by github-issue-43.dfy.

datatype Option<X> = None | Some(value: X) {
  predicate IsFailure() { None? }
  function PropagateFailure<Y>(): Option<Y> requires IsFailure() { None }
  function Extract(): X requires !IsFailure() { value }
}
type Stack
method Pop(stack: Stack) returns (value: Option<int>, stack': Stack) {
  return None, stack;
}
// Three destinations, with and without Extract.
datatype Status = Failure | Success {
  predicate IsFailure() { Failure? }
  function PropagateFailure(): Status requires IsFailure() { Failure }
}
method WithExtract() returns (r: Option<int>, a: int, b: int) {
  return Some(1), 2, 3;
}
method WithoutExtract() returns (r: Status, a: int, b: int, c: int) {
  return Success, 1, 2, 3;
}
class Box { var value: int }

// Valid local, member, and index destinations resolve independently.
method Good(box: Box, arr: array<int>) returns (r: Option<int>) {
  var a, b, c: int;
  a, b, c :- WithExtract();
  box.value, arr[0], c :- WithExtract();
  a, box.value, arr[0] :- WithExtract();
}
method GoodNoExtract(box: Box, arr: array<int>) returns (r: Status) {
  var a, b, c: int;
  a, b, c :- WithoutExtract();
  box.value, arr[0], c :- WithoutExtract();
  a, box.value, arr[0] :- WithoutExtract();
}
