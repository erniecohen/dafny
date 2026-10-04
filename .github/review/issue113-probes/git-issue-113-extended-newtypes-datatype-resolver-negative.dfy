// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Desired failures are nominal source-type mismatches, never successful implicit base conversions.

datatype L = Nil | Cons(head: int, tail: L)
newtype NonEmpty = xs: L | xs.Cons? witness Cons(1, Nil)
newtype Other = L witness *

method BadTail(n: NonEmpty) {
  var strengthened: NonEmpty := n.tail; // tail retains L
}

method BadNominality(n: NonEmpty, o: Other) {
  var b: L := n; // explicit unwrap required
  var n2: NonEmpty := o; // explicit checked reintroduction required
  assert n == o; // incompatible nominal operands
}

datatype WithFunction = WithFunction(f: int -> int)
newtype NF = WithFunction witness *
method BadEquality(a: NF, b: NF) returns (same: bool) {
  same := a == b; // inherited executable equality restriction
}

class C {}
datatype RefBox = RefBox(value: C)
newtype NR = RefBox witness *
method RequiresReferenceFree<T(!new)>() {}
method BadReferenceFree() {
  RequiresReferenceFree<NR>(); // inherits reference content
}
