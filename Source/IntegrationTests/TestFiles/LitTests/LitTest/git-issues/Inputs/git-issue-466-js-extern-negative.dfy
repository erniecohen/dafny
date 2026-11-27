include "git-issue-466-js-extern-library.dfy"
module Negative {
  import A
  method Check() {
    assert A.Identity(1) == 1;
    assert false;
  }
}
