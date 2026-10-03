type Id<T> = T
newtype A<T> = b | P<T>(b)
newtype B<T> = Id<A<seq<T>>>
predicate P<T>(b: B<T>)
