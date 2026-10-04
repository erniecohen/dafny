// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
newtype Wrapped<T> = Stream<T> witness *
newtype Trivial<T> = s:Stream<T> | true witness *
function {:abstemious} Carry(s:Wrapped<int>):Wrapped<int> { (Cons(1, (s as Stream<int>)) as Wrapped<int>) }
function HelperBoundary():Wrapped<int> { (Cons(1, (Carry(HelperBoundary()) as Stream<int>)) as Wrapped<int>) }
