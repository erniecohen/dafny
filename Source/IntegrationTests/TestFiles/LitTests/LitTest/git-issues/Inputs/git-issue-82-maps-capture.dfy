ghost function CapturedMap(n: int): map<int, int> {
  map x: int | x == n :: 0 := x
}

lemma FiniteCaptureIsNotContradictory() {
  assert CapturedMap(0)[0] == 0;
  assert CapturedMap(1)[0] == 1;
  assert false;
}
