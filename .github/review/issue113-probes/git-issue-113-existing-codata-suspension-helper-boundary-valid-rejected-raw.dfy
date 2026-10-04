// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
function {:abstemious} Carry(s:Stream<int>):Stream<int> { (Cons(1, (s))) }
function HelperBoundary():Stream<int> { (Cons(1, (Carry(HelperBoundary())))) }
