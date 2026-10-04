method NativeFalse(x: bv3) {
  var y := (x + 1) ^ x;
  assert y & 0 == 0;
  assert false;
}
