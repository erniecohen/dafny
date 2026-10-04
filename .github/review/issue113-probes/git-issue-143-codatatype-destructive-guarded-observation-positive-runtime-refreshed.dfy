// Private, unexecuted compatibility control. Finite observations are defined:
/// Values(n) begins n,n+2,n+2,n+4,n+4,...; every head is a nat.
codatatype Stream = Cons(head: nat, tail: Stream)
function Values(n: nat): Stream {
  Cons(n, Cons(Values(n + 2).head, Values(n + 2)))
}
lemma FirstTwo(n: nat)
  ensures Values(n).head == n
  ensures Values(n).tail.head == n + 2
{}
method Main() { print Values(3).head, " ", Values(3).tail.head, "\n"; }
