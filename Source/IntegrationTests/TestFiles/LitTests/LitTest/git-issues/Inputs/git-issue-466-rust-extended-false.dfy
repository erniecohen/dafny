include "git-issue-466-rust-imported-library.dfy"
include "git-issue-466-rust-extended-library.dfy"
module Control {
  import Extended
  method WrongResult() {
    var x := new Extended.Native();
    assert x.Added(3) == 104;
  }
  method ReachableFalse() {
    var x := new Extended.Native();
    var result := x.Added(3);
    assert result == 103;
    assert false;
  }
}
