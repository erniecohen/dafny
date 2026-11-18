include "git-issue-466-clause5-positive.dfy"
module Control {
  import TwoA
  import TwoB
  import ThreeA
  import ThreeC
  lemma TwoModules() {
    var t := TwoA.Unit();
    assert TwoB.Good(t);
    assert false;
  }
  lemma ThreeModules() {
    var t := ThreeA.Unit();
    assert ThreeC.Good(t);
    assert false;
  }
}
