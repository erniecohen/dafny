include "git-issue-466-js-trait-refinement-library.dfy"
module Control {
  import B
  import RefinedB
  import Final
  method WrongRead() {
    var viewer := RefinedB.Viewer<int>.Create(3);
    assert viewer.Read() == 4;
  }
  method WrongField() {
    var mirror := new Final.Mirror<int>(5);
    assert mirror.Stored == 6;
  }
  method ReachableFalse() {
    var viewer := Final.Viewer<int>.Create(7);
    assert viewer.Read() == 7;
    assert viewer.GhostValue() == 1;
    assert false;
  }
}
