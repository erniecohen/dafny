include "../git-issue-504-positive.dfy"

lemma TwoIsConsistent() {
  var g := TwoIndependent.Construct();
  assert false;
}
lemma ThreeIsConsistent() {
  var g := ThreeIndependent.Construct();
  assert false;
}
