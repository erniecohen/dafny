// Unexecuted clean anti-vacuity control with independently introduced nat heads.
codatatype NatStream = Cons(head: nat, tail: NatStream)
function NatValues(n: nat): NatStream { Cons(n, NatValues(n + 1)) }
lemma Head(n: nat)
  ensures NatValues(n).head == n
{}
lemma InhabitedControl() {
  var value := NatValues(7);
  assert value.head == 7;
  assert false; // Intended sole verification failure, not yet measured.
}
