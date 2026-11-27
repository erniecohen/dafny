include "git-issue-466-js-constructor-library.dfy"
module Control {
  import A
  import B
  method WrongConstructor() {
    var x := new B.Cell<int>.Init(0, 3, 5);
    assert x.Read() == 4;
  }
  method WrongGeneric() {
    var x := new B.Cell<A.Tree<int>>.Init(0, A.Leaf(7), 11);
    assert x.Read().value == 8;
  }
  method ReachableFalse() {
    var x := new B.Cell<int>.Init(0, 3, 5);
    assert x.Read() == 3 && x.Tag() == 5;
    assert false;
  }
}
