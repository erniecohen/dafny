// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
function Unguarded():Stream<int> { ((Unguarded())) }
