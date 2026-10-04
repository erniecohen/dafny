datatype Box<T> = Box(value: T)
method Identity<T>(x: T) returns (y: T) ensures y == x { y := x; }
lemma Generic(x: Box<int>) { assert x == Box(x.value); }
