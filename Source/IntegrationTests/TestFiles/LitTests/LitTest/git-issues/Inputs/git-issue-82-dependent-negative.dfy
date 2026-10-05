// Active finite definitions and legitimate conclusions must not prove false.
lemma FiniteDependentImagesRemainConsistent() {
  var antitone := set x: int, y: int | 0 <= x < 5 && -x-x < y && y < 0 && x < -y :: x;
  assert forall x: int :: x in antitone ==> 0 <= x < 5;
  var increasing := set x: int, y: int | 0 <= x && 0 <= y < 3 && x < y :: x;
  assert forall x: int :: x in increasing ==> 0 <= x < 3;
  assert false;
}
