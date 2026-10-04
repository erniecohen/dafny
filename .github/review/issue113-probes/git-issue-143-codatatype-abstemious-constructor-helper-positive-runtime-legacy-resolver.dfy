// Private, unexecuted compatibility control for a legal abstemious helper.
codatatype Stream = Cons(head: nat, tail: Stream)
function {:abstemious} Carry(s: Stream): Stream { Cons(s.head, s) }
function Values(n: nat): Stream { Cons(n, Carry(Values(n + 1))) }
lemma FirstTwo(n: nat)
  ensures Values(n).head == n
  ensures Values(n).tail.head == n + 1
{}
method Main() { print Values(3).head, " ", Values(3).tail.head, "\n"; }
