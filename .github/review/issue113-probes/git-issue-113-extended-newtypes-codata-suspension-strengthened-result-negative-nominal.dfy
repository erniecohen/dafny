// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
newtype PositiveHead = s:Stream<int> | s.head>0 witness *
function BadRefined():PositiveHead { Cons(0,BadRefined() as Stream<int>) as PositiveHead }
