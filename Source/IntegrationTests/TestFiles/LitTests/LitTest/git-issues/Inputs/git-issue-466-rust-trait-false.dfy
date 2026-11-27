include "git-issue-466-rust-imported-library.dfy"
include "git-issue-466-rust-trait-library.dfy"
module Control {
  import Traitful
  method WrongGenerated() {
    var x := new Traitful.Generated<int>();
    var p: Traitful.Protocol<int> := x;
    assert p.Get(3) == 4;
  }
  method WrongForeign() {
    var x := new Traitful.Foreign<int>();
    var p: Traitful.Protocol<int> := x;
    assert p.Get(5) == 6;
  }
  method ReachableFalse() {
    var x := new Traitful.Generated<int>();
    var p: Traitful.Protocol<int> := x;
    assert p.Get(3) == 3;
    assert false;
  }
}
