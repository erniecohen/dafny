ghost function F(i: int): (r: int)
  ensures var unused := 1; F(i) == 0
{ 1 }

lemma Contradiction() {
  var x := F(0);
  assert false;
}
