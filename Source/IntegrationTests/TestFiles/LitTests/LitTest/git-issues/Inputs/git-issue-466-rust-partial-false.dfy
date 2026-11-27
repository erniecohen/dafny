include "git-issue-466-rust-imported-library.dfy"
include "git-issue-466-rust-extended-library.dfy"
include "git-issue-466-rust-partial-library.dfy"
module Control {
  import Partial
  method WrongResult() {
    var x := new Partial.Native();
    var r := x.Value(3);
    assert r == 105;
  }
  method ReachableFalse() {
    var x := new Partial.Native();
    var r := x.Value(3);
    assert r == 104;
    assert false;
  }
}
