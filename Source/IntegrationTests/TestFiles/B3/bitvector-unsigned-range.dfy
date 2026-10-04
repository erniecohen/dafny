lemma UnsignedRange(x: bv3) {
  assert 0 <= (x as int) < 8;
}

lemma WideRange(x: bv67) {
  assert 0 <= (x as int) < 147573952589676412928;
}
