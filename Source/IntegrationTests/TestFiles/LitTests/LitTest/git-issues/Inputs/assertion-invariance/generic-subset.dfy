datatype D<T> = D(value: T, i: int)
ghost predicate P<T>(d: D<T>) { d.i >= 0 }
type S<T> = d: D<T> | P(d) witness *
ghost function F<T>(x: T): S<T> { var d := D(x,0); d }
