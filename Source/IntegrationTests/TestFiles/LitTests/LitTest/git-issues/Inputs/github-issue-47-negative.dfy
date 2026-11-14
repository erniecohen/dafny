// Comparisons that the refreshed resolver must still reject, as the legacy resolver does.
trait Number extends object { }
class Integer extends Number { }
trait Left extends object { }
trait Right extends object { }
class Both extends Left, Right { }
datatype Box<T> = Box(t: T)
datatype Pair<+A, +B> = Pair(a: A, b: B)
class Cell<T> { }

method NoCommonComponent(i: Integer, x: int) returns (r: bool) {
  var a: (int, Integer) := (x, i);
  var b: (bool, Number) := (true, i);
  r := a == b;
}
method UnrelatedTraits(l: Left, rt: Right, c: Both) returns (r: bool) {
  var a: (Left, Both) := (l, c);
  var b: (Right, Both) := (rt, c);
  r := a == b;
}
method InvariantDatatype(i: Integer) returns (r: bool) {
  var a: Box<(Number, Integer)> := Box((i, i));
  var b: Box<(Integer, Number)> := Box((i, i));
  r := a == b;
}
method InvariantClass(c: Cell<Number>, d: Cell<Integer>) returns (r: bool) {
  r := c == d;
}
method CrossedInsideInvariant(i: Integer) returns (r: bool) {
  var a: Pair<Box<Number>, Integer> := Pair(Box(i), i);
  var b: Pair<Box<Integer>, Number> := Pair(Box(i), i);
  r := a == b;
}
method DifferentArity(i: Integer) returns (r: bool) {
  var a: (Number, Integer) := (i, i);
  var b: (Integer, Number, Number) := (i, i, i);
  r := a == b;
}
