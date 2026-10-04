// RUN: %verify --type-system-refresh=true --general-newtypes --extended-newtype-bases "%s" > "%t"
// Unexecuted draft: phantom parameters do not introduce observable floating fields.

datatype Phantom<T> = Unit(value: int)
newtype WrappedPhantom = Phantom<fp32> witness Unit(0)
datatype NestedPhantom<T> = End | Next(next: NestedPhantom<seq<T>>)
newtype WrappedNested = NestedPhantom<fp64> witness End
datatype Box<T> = Box(value: T)
newtype NestedVisible = Box<Box<int>> witness Box(Box(0))

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

newtype Id<T> = T witness *
newtype RepeatedVisible = Box<Id<Id<int>>> witness *
method CompareRepeatedVisible(x: RepeatedVisible) {
  var b := x == x;
  assert b;
}
