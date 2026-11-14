// RUN: echo "legacy resolver, C#" > "%t"
// RUN: %exits-with 0 %baredafny run --target:cs --type-system-refresh:false --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "refreshed resolver, C#" >> "%t"
// RUN: %exits-with 0 %baredafny run --target:cs --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/352
// Two values of a datatype with the same constructor and equal arguments are equal, whatever type
// arguments they were built with.  The verifier proves each "ensures r" below; the compiled C# code
// used to compute false for the comparisons of values built at different type arguments.

trait Number extends object { }
class Integer extends Number { constructor () { } }

// A record type (one constructor)
datatype Pair<+A, +B> = Pair(a: A, b: B)
// Several constructors, one of them without arguments
datatype List<+T> = Nil | Cons(head: T, tail: List<T>)
// A type argument that C# represents by a value type, beside reference ones
datatype Triple<+X, +A, +B> = Triple(x: X, a: A, b: B)

// The issue's two programs: crossed type arguments, compared directly or widened first
method CrossedPair(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a := Pair<Number, Integer>.Pair(i, j);
  var b := Pair<Integer, Number>.Pair(i, j);
  r := a == b;
}

method WidenedPair(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a: Pair<Number, Number> := Pair<Number, Integer>.Pair(i, j);
  var b: Pair<Number, Number> := Pair<Integer, Number>.Pair(i, j);
  r := a == b;
}

// One type argument a subtype of the other's, in both orders
method SubPair(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a := Pair<Number, Number>.Pair(i, j);
  var b := Pair<Integer, Integer>.Pair(i, j);
  r := a == b && b == a && !(a != b);
}

method CrossedList(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a: List<Number> := List<Integer>.Cons(i, List<Integer>.Nil);
  var b: List<Number> := List<Number>.Cons(i, List<Number>.Nil);
  var n0: List<Number> := List<Integer>.Nil;
  var n1: List<Number> := List<Number>.Nil;
  r := a == b && b == a && n0 == n1 && n1 == n0;
}

method CrossedTriple(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a := Triple<int, Number, Integer>.Triple(3, i, j);
  var b := Triple<int, Integer, Number>.Triple(3, i, j);
  r := a == b;
}

// Tuples, whose C# classes come from the runtime
method CrossedTuple(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a := (i as Number, j);
  var b := (i, j as Number);
  var c: (Number, Number, int) := (i, j, 5);
  var d: (Number, Number, int) := (i as Number, j as Number, 5);
  r := a == b && c == d;
}

// Hashing agrees with equality, so a value is found in a set built from a value at other type arguments
method InSet(i: Integer, j: Integer) returns (r: bool) ensures r {
  var a: Pair<Number, Number> := Pair<Number, Integer>.Pair(i, j);
  var b: Pair<Number, Number> := Pair<Integer, Number>.Pair(i, j);
  var s := {b};
  r := a in s && {a} == s;
}

// Controls: values that differ are still unequal
method Differ(i: Integer, j: Integer) returns (r: bool) requires i != j ensures !r {
  var a: Pair<Number, Number> := Pair<Number, Integer>.Pair(i, j);
  var b: Pair<Number, Number> := Pair<Integer, Number>.Pair(j, j);
  var c: List<Number> := List<Integer>.Cons(i, List<Integer>.Nil);
  var d: List<Number> := List<Number>.Cons(j, List<Number>.Nil);
  var e: List<Number> := List<Number>.Nil;
  var f := Triple<int, Number, Integer>.Triple(3, i, j);
  var g := Triple<int, Integer, Number>.Triple(4, i, j);
  r := a == b || c == d || c == e || e == c || f == g;
}

method Main() {
  var i := new Integer();
  var j := new Integer();
  var r0 := CrossedPair(i, j);
  var r1 := WidenedPair(i, j);
  var r2 := SubPair(i, j);
  var r3 := CrossedList(i, j);
  var r4 := CrossedTriple(i, j);
  var r5 := CrossedTuple(i, j);
  var r6 := InSet(i, j);
  var r7 := Differ(i, j);
  print r0, " ", r1, " ", r2, " ", r3, " ", r4, " ", r5, " ", r6, " ", r7, "\n";
}
