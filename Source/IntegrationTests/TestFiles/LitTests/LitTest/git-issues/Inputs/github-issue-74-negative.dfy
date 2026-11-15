function Identity(x: int): int { x }

lemma LiveIdentityThenFalse() {
  assert Identity(7) == 7;
  assert false;
}

lemma WrongValue() ensures Identity(7) == 8 { }

function Bounded(x: int): int requires x >= 0 { x }
lemma ArgumentCheck() {
  var x := Bounded(-7);
}
