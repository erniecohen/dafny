// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
function Repeat<T>(x:T):Stream<T> { (Cons(x, (Repeat(x)))) }
type PositiveHead = s:Stream<int> | s.head>0 witness *
method BadCast(s:Stream<int>) { var p := s as PositiveHead; }
