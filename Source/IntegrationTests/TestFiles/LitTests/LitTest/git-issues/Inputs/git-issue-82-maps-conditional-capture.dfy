ghost function ConditionalCapturedMap(b: bool): map<int, int> {
  var n := if b then 0 else 1;
  map x: int | x == n :: 0 := x
}

lemma ConditionalCaptureIsNotContradictory(b: bool) {
  assert ConditionalCapturedMap(b)[0] == (if b then 0 else 1);
  assert false;
}
