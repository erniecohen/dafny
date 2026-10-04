// Private unexecuted control; capture actual diagnostics before registration.
codatatype Stream<T> = Cons(head:T,tail:Stream<T>)
function Repeat<T>(x:T):Stream<T> { (Cons(x, (Repeat(x)))) }
lemma Inhabited() {
 var s := Repeat(7);
 assert s.head == 7;
 assert s.tail.head == 7;
 assert false;
}
