// Unexecuted clean anti-vacuity control. The definition has no destructive
// observation of a suspended co-call; all client observations are ordinary.
codatatype RawStream = Raw(head: int, tail: RawStream)
function RawValues(n: int): RawStream { Raw(n, RawValues(n + 1)) }
lemma FirstTwo(n: int)
  ensures RawValues(n).head == n
  ensures RawValues(n).tail.head == n + 1
{}
lemma InhabitedControl() {
  var value := RawValues(0);
  assert value.head == 0;
  assert value.tail.head == 1;
  assert false; // Intended sole verification failure, not yet measured.
}
