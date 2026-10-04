// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
method BadCarrier(s:Stream<int>) { var n := s as Stream<nat>; }
