// Milestone M4a of --modular-termination (https://github.com/erniecohen/dafny/issues/211): a witness, not run
// by the tests.  This is github-issue-211-rank-gap.dfy with Main printing the value instead of asserting false.
// Before M4a, "dafny run --modular-termination" verified it (4 verified, 0 errors) and then the program
// overflowed the stack: C.F calls itself through dispatch forever.  Now it is rejected before it runs.
module Tr {
  trait {:termination false} T {
    function {:fuel 3,4} F(n: nat): nat
      decreases {:global} 7
  }
}
module Z {
  import Tr
  // A rank defined through dispatch, in a module of its own.
  function {:fuel 3,4} R(x: Tr.T): nat
    decreases {:global} 8
  {
    x.F(0)
  }
}
module X {
  import Tr
  import Z
  class C extends Tr.T {
    constructor () {}
    function {:fuel 3,4} F(n: nat): nat
      decreases {:global} Z.R(this)
    {
      var me: Tr.T := this;
      me.F(n) + 1
    }
  }
  method Main() {
    var c := new C();
    var k := c.F(0);
    print k, "\n";
  }
}
