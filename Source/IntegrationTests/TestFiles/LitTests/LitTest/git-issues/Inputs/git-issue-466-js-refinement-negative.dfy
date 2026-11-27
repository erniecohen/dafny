include "git-issue-466-js-refinement-library.dfy"
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
  method ReachableFalse() {
    var x := new Final.Cell<int>.Init(0, 3, 5);
    assert x.Read() == 3 && x.Tag() == 5;
    assert false;
  }
}
