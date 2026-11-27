include "git-issue-466-rust-imported-library.dfy"
module Control {
  import RefinedImported
  method ReachableFalse() {
    var native := new RefinedImported.Native();
    var value := native.Read();
    assert false;
  }
}
