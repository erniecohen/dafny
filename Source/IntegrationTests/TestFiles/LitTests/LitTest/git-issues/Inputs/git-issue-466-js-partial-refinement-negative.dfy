include "git-issue-466-js-partial-refinement-library.dfy"
module Control {
  import A
  import B
  import RefinedB
  import Final
  method WrongConstructor() {
    var x := new RefinedB.Cell<int>.Init(0, 3, 5);
    assert x.Read() == 4;
  }
  method WrongGeneric() {
    var x := new Final.Cell<A.Tree<int>>.Init(0, A.Leaf(7), 11);
    assert x.Read().value == 8;
  }
  method WrongField() {
    var x := new RefinedB.Partial<int>(3);
    assert x.Stored == 4;
  }
  method WrongUpdate() {
    var x := new Final.Partial<int>(3);
    x.Update(5);
    assert x.Stored == 6;
  }
  method ReachableFalse() {
    var x := new Final.Cell<int>.Init(0, 3, 5);
    assert x.Read() == 3 && x.Tag() == 5;
    var partial := new Final.Partial<int>(7);
    assert partial.Stored == 7;
    assert false;
  }
}
