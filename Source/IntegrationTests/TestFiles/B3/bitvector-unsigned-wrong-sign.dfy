lemma WrongSignedValue() {
  var x := 4 as bv3;
  assert (x as int) == -4;
}
