ghost function Valid(i: int): int
  ensures var unused := 1; Valid(i) == 1
{ 1 }

lemma FalseControl() {
  var x := Valid(0);
  assert false;
}
