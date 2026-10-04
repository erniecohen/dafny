// Unexecuted clean anti-vacuity control. The definition has no destructive
// observation of a suspended co-call; all client observations are ordinary.
codatatype RawStream = Raw(head: int, tail: RawStream)
function RawValues(n: int): RawStream { Raw(n, RawValues(n + 1)) }
lemma Head(n: int)
  ensures RawValues(n).head == n
{}
lemma InhabitedControl() {
  var value := RawValues(0);
  assert value.head == 0;
  assert false; // Intended sole verification failure, not yet measured.
}
