// Private, unexecuted control: legal constructor-returning helper could
// restore typed-result facts before the literal -1 introduction is checked.
codatatype Stream = Cons(tail: Stream, head: nat)
function {:abstemious} Carry(s: Stream): Stream { Cons(s, s.head) }
function Bad(): Stream { Cons(Carry(Bad()), -1) }
lemma Exploit() { var s := Bad(); assert false; }
