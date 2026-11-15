// https://github.com/erniecohen/dafny/issues/310
// A range of array elements is not a destination of a :- statement. The refreshed resolver crashed
// on the element destination before it, and now reports the ranges as the legacy resolver does.
// RUN: echo "Legacy resolver" > "%t"
// RUN: %exits-with 2 %baredafny resolve --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" >> "%t"
// RUN: echo "Refreshed resolver" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:true "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

datatype Result<T> = Failure(msg: string) | Success(value: T) {
  predicate IsFailure() { Failure? }
  function PropagateFailure(): Result<T> requires IsFailure() { this }
  function Extract(): T requires !IsFailure() { this.value }
}

method M() returns (rr: Result<int>) {
  var a := new int[10];
  var m := new int[2, 2];
  var r := Success(100);
  var k: int;

  a[1], k :- r, 0; // OK
  m[1, 0], k :- r, 0; // OK
  a[1..2], k :- r, 0; // ERROR
  a[..2], k :- r, 0; // ERROR
  a[..], k :- r, 0; // ERROR
  a[1..], k :- r, 0; // ERROR
  a[2..] :- r; // ERROR
}
