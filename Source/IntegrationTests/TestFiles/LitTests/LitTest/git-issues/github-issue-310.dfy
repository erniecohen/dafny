// https://github.com/erniecohen/dafny/issues/310
// A :- statement whose first destination is an array element, or an element of a
// multidimensional array, crashed the refreshed resolver. Both resolvers verify and run it alike.
// RUN: echo "Legacy resolver" > "%t"
// RUN: %baredafny run --standard-libraries:false --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" >> "%t"
// RUN: echo "Refreshed resolver" >> "%t"
// RUN: %baredafny run --standard-libraries:false --solver-path %review-z3 --cores 1 --use-basename-for-filename --show-snippets:false --type-system-refresh:true "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

datatype Result<T> = Failure(msg: string) | Success(value: T) {
  predicate IsFailure() { Failure? }
  function PropagateFailure<U>(): Result<U> requires IsFailure() { Failure(msg) }
  function Extract(): (t: T) requires !IsFailure() ensures t == this.value { this.value }
}

method Get(x: int) returns (r: Result<int>)
  ensures r == Success(x)
{
  r := Success(x);
}

class Cell {
  var v: int
  constructor (v: int) ensures this.v == v { this.v := v; }
}

// The array and the index are evaluated before the right-hand side is assigned.
method ArrayElement() returns (rr: Result<int>) {
  var a := new int[3](_ => 0);
  var b := a;
  var i := 1;
  a[i], i, a :- Success(7), 2, new int[3](_ => 5);
  assert b[1] == 7 && i == 2 && a[1] == 5;
  print b[..], " ", a[..], "\n";

  b[0] :- Get(11);
  assert b[0] == 11;
  b[2] :- expect Get(13);
  assert b[2] == 13;
  print b[..], "\n";
  rr := Success(0);
}

method MultiElement() returns (rr: Result<int>) {
  var m := new int[2, 2]((_, _) => 0);
  var n := m;
  var i, j := 1, 0;
  m[i, j], i, m :- Success(3), 0, new int[2, 2]((_, _) => 9);
  assert n[1, 0] == 3 && i == 0 && m[1, 0] == 9;
  n[0, 1] :- Get(4);
  assert n[0, 1] == 4;
  print n[0, 0], n[0, 1], n[1, 0], n[1, 1], " ", m[1, 0], "\n";
  rr := Success(0);
}

method FieldDestination() returns (rr: Result<int>) {
  var c := new Cell(0);
  var c1 := new Cell(1);
  var d := c;
  c.v, c :- Success(8), c1;
  assert d.v == 8 && c.v == 1;
  print d.v, " ", c.v, "\n";
  rr := Success(0);
}

method Fails() returns (rr: Result<int>) {
  var a := new int[1](_ => 0);
  a[0] :- Failure("stop");
  print "unreachable\n";
  rr := Success(a[0]);
}

method Main() {
  var r := ArrayElement();
  r := MultiElement();
  r := FieldDestination();
  r := Fails();
  print r, "\n";
}
