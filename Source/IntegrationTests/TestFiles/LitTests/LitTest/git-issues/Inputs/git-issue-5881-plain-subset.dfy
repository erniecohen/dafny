type Unique<T> = t:T | true witness *
datatype Box<T> = Box(x:Unique<T>)
method Main() { var b: Box<int> := Box(4); print b.x, "\n"; }
