ghost function DependentWitness82(x: int, y: int): int { x + y }

// Independent finite bounds remain valid for antitone dependent fibers.
lemma FiniteDependentImages() {
  var antitone := set x: int, y: int {:trigger DependentWitness82(x, y)} | 0 <= x < 5 && -x-x < y && y < 0 && x < -y :: x;
  assert DependentWitness82(2, -3) == -1;
  assert 2 in antitone;
  assert forall x: int :: x in antitone ==> 0 <= x < 5;
  // Increasing substitution may use the upper bound of the later variable.
  var increasing := set x: int, y: int {:trigger DependentWitness82(x, y)} | 0 <= x && 0 <= y < 3 && x < y :: x;
  assert DependentWitness82(0, 1) == 1;
  assert 0 in increasing;
  assert DependentWitness82(1, 2) == 3;
  assert 1 in increasing;
  assert forall x: int :: x in increasing ==> 0 <= x < 3;
}
