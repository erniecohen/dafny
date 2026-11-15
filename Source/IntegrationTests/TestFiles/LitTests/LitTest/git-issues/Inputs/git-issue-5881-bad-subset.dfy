type Positive = t:int | t > 0 witness 1
datatype Box<T> = Box(x:T)
method Main() { var b: Box<Positive> := Box(-1); }
