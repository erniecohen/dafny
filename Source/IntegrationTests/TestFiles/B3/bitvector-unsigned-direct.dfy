lemma WidthOne(x: bv1) {
  assert (x as int) == 0 || (x as int) == 1;
}

lemma HighBit() {
  var x := 73786976294838206464 as bv67;
  var i := x as int;
  assert i == 73786976294838206464;
}
