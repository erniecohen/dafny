// Milestone M4a of --modular-termination (https://github.com/erniecohen/dafny/issues/211): the program of
// github-issue-211-rank-gap.dfy with the rank function R in the implementation's own module.  Without the
// option, the resolver rejects the decreases clause, which calls the mutually recursive R; with it, before
// M4a, the program verified and proved false.  Now the global bound is rejected.
module Tr {
  trait {:termination false} T {
    function {:fuel 3,4} F(n: nat): nat
      decreases {:global} 7
  }
}
module X {
  import Tr
  // A rank defined through dispatch.
  function {:fuel 3,4} R(x: Tr.T): nat
    decreases {:global} 8
  {
    x.F(0)
  }
  class C extends Tr.T {
    constructor () {}
    // The override's own bound, at most the parent's, is the rank R.
    function {:fuel 3,4} F(n: nat): nat
      decreases {:global} R(this)
    {
      var me: Tr.T := this;
      me.F(n) + 1
    }
  }
  method Main() {
    var c := new C();
    var k := c.F(0);
    assert false;
  }
}
