// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Portable field-shape counterpart of the development-only floating phantom controls.
datatype Phantom<T> = Unit(value: int)
newtype WrappedPhantom = Phantom<int -> int> witness Unit(0)
datatype NestedPhantom<T> = End | Next(next: NestedPhantom<seq<T>>)
newtype WrappedNested = NestedPhantom<int -> int> witness End
datatype Box<T> = Box(value: T)
newtype NestedVisible = Box<Box<int>> witness Box(Box(0))
newtype Id<T> = T witness *
newtype RepeatedVisible = Box<Id<Id<int>>> witness *

method ComparePhantom(x: WrappedPhantom) {
  var b := x == x;
  assert b;
}
method CompareNestedPhantom(x: WrappedNested) {
  var b := x == x;
  assert b;
}
method CompareNestedVisible(x: NestedVisible) {
  var b := x == x;
  assert b;
}
method CompareRepeatedVisible(x: RepeatedVisible) {
  var b := x == x;
  assert b;
}
