include "git-issue-466-rust-trait-source.dfy"
include "git-issue-466-rust-body-library.dfy"
module Control {
  import Bodied
  method WrongConstructor() {
    var x := new Bodied.Store(7);
    assert x.Read() == 8 + x.Tag() + 1;
  }
  method WrongFunction() {
    var x := new Bodied.Store(7);
    assert x.Next(3) == 5;
  }
  method WrongByMethod() {
    var x := new Bodied.Cell<int>.Init(5);
    assert x.Echo(5) == 6;
  }
  method ReachableFalse() {
    var x := new Bodied.Store(7);
    var y := x.Advance(3);
    assert y == 4;
    assert false;
  }
}
