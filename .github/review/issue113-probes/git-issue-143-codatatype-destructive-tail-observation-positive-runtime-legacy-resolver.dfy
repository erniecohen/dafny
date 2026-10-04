// Private, unexecuted raw tail compatibility control.
codatatype Stream = Cons(head: int, tail: Stream)
function Values(n: int): Stream {
  Cons(n, Cons(n + 1, Values(n + 2).tail))
}
lemma FirstTwo(n: int)
  ensures Values(n).head == n
  ensures Values(n).tail.head == n + 1
{}
method Main() { print Values(3).head, " ", Values(3).tail.head, "\n"; }
