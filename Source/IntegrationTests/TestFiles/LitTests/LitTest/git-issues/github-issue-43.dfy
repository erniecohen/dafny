// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --show-snippets:false --use-basename-for-filename "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --show-snippets:false --use-basename-for-filename "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype Option<X> = None | Some(value: X) {
  predicate IsFailure() { None? }
  function PropagateFailure<Y>(): Option<Y> requires IsFailure() { None }
  function Extract(): X requires !IsFailure() { value }
}
type Stack
method Pop(stack: Stack) returns (value: Option<int>, stack': Stack) {
  return None, stack;
}
method Popper(stack: Stack) returns (o: Option<int>) {
  var top;
  top, undeclaredVariable :- Pop(stack);
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

method ExtractFirst(box: Box, arr: array<int>) returns (r: Option<int>) {
  var a, b, c: int;
  missing, b, c :- WithExtract();
}

method ExtractMiddle(box: Box, arr: array<int>) returns (r: Option<int>) {
  var a, b, c: int;
  a, missing, c :- WithExtract();
}

method ExtractLast(box: Box, arr: array<int>) returns (r: Option<int>) {
  var a, b, c: int;
  a, b, missing :- WithExtract();
}

method ExtractMember(box: Box, arr: array<int>) returns (r: Option<int>) {
  var a, b, c: int;
  a, box.missing, c :- WithExtract();
}

method ExtractIndex(box: Box, arr: array<int>) returns (r: Option<int>) {
  var a, b, c: int;
  a, arr[missing], c :- WithExtract();
}

method NoExtractFirst(box: Box, arr: array<int>) returns (r: Status) {
  var a, b, c: int;
  missing, b, c :- WithoutExtract();
}

method NoExtractMiddle(box: Box, arr: array<int>) returns (r: Status) {
  var a, b, c: int;
  a, missing, c :- WithoutExtract();
}

method NoExtractLast(box: Box, arr: array<int>) returns (r: Status) {
  var a, b, c: int;
  a, b, missing :- WithoutExtract();
}

method NoExtractMember(box: Box, arr: array<int>) returns (r: Status) {
  var a, b, c: int;
  a, box.missing, c :- WithoutExtract();
}

method NoExtractIndex(box: Box, arr: array<int>) returns (r: Status) {
  var a, b, c: int;
  a, arr[missing], c :- WithoutExtract();
}

// Valid destinations still resolve after earlier errors, including member/index targets.
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

// RUN: %baredafny resolve --type-system-refresh:false --show-snippets:false --use-basename-for-filename "%S/github-issue-43-valid.dfy" > "%t"
// RUN: %baredafny resolve --type-system-refresh:true --show-snippets:false --use-basename-for-filename "%S/github-issue-43-valid.dfy" > "%t"
