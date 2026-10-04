// Unexecuted clean anti-vacuity control with independently introduced nat heads.
codatatype NatStream = Cons(head: nat, tail: NatStream)
function NatValues(n: nat): NatStream { Cons(n, NatValues(n + 1)) }
lemma FirstTwo(n: nat)
  ensures NatValues(n).head == n
  ensures NatValues(n).tail.head == n + 1
{}
lemma InhabitedControl() {
  var value := NatValues(7);
  assert value.head == 7;
  assert value.tail.head == 8;
  assert false; // Intended sole verification failure, not yet measured.
}
