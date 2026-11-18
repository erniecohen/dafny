include "git-issue-466-stage5-positive.dfy"
module ConcreteControl {
  import Left
  lemma Nonvacuity() {
    var r := Left.Make();
    assert r.lo <= r.hi && r.hi >= 0;
    assert false;
  }
}
abstract module AbstractControl {
  import AbstractLeft
  import RefinedLeft
  lemma Nonvacuity() {
    var t := AbstractLeft.Make();
    assert t.Leaf? && t.n == 0;
    assert false;
  }
  lemma RefinedNonvacuity() {
    var t := RefinedLeft.Make();
    assert t.Leaf? && t.n == 0 && t.n <= 10;
    assert false;
  }
}
