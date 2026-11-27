include "git-issue-466-rust-imported-library.dfy"
include "git-issue-466-rust-constructor-library.dfy"
module Control {
  import Constructed
  method WrongStore() {
    var x := new Constructed.Store(3);
    assert x.Read() == 3 + x.Tag() + 2;
  }
  method WrongGeneric() {
    var x := new Constructed.Cell<int>.Init(151);
    assert x.Read() == 152;
  }
  method ReachableFalse() {
    var x := new Constructed.Store(3);
    assert x.Read() == 3 + x.Tag() + 1;
    assert false;
  }
}
