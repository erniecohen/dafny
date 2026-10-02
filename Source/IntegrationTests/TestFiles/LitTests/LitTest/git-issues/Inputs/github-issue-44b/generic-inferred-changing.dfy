newtype A<T> = b | P<T>(b)
newtype B<T> = A<seq<T>>
predicate P<T>(b: B<T>)
