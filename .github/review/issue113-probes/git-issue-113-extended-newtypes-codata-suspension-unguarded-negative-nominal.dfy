// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
function Unguarded():Wrapped<int> { ((Unguarded() as Stream<int>) as Wrapped<int>) }
