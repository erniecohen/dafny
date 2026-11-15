// Proposed [] syntax; expected outcomes are UNVERIFIED.
// --user-defined-operators --type-system-refresh
// Both bad_index and bad_key must fail at the selected ordinary call's
// precondition. q1_false_control must separately fail at assert false.

datatype SeqBox = SeqBox(xs: seq<int>) {
  function {:operator "[]"} get(i: int): int
    requires 0 <= i < |xs|
  { xs[i] }
}

datatype Catalog = Catalog(answer: int) {
  function {:operator "[]"} get(key: string): int
    requires key == "answer"
  { answer }
}

method bad_index()
{
  var b := SeqBox([10]);
  var x := b[1]; // Expected verifier error: get's index precondition.
}

method bad_key()
{
  var c := Catalog(42);
  var x := c["missing"]; // Expected verifier error: get's key precondition.
}

lemma q1_false_control()
{
  var b := SeqBox([10]);
  assert b[0] == 10;
  assert false; // Must still fail independently of the negative cases.
}
