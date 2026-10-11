ghost function CapturedIMap(n: int): imap<int, int> {
  imap x: int | x == n :: 0 := x
}

lemma InfiniteCaptureIsNotContradictory() {
  assert CapturedIMap(0)[0] == 0;
  assert CapturedIMap(1)[0] == 1;
  assert false;
}
