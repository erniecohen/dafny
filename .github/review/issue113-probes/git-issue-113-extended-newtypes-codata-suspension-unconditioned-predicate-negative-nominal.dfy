// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
function Repeat<T>(x:T):Wrapped<T> { (Cons(x, (Repeat(x) as Stream<T>)) as Wrapped<T>) }
newtype PositiveHead = s:Stream<int> | s.head>0 witness *
method BadCast(s:Stream<int>) { var p := s as PositiveHead; }
