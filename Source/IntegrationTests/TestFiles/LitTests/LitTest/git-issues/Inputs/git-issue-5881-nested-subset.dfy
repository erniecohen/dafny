type Unique<T> = t:T | true witness *
type Twice<T> = t:Unique<T> | true witness *
datatype Box<T> = Box(x:Twice<T>)
method Main() { var b: Box<int> := Box(4); print b.x, "\n"; }
