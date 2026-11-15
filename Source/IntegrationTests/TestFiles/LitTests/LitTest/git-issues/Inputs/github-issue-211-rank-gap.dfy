// Milestone M4a of --modular-termination (https://github.com/erniecohen/dafny/issues/211): before M4a this
// program verified under the option, "assert false" included, and running it overflows the stack.  The
// override's own bound R(this) is a rank computed through dispatch: R(this) calls this.F(0), which is C.F
// itself.  The bound obligations of C.F assumed R#canCall(this), unfolded R and C.F, and so proved anything
// from C.F(this, 0) == C.F(this, 0) + 1.  Now the bound is rejected: a bound may not reach open dispatch.
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
    assert false;
  }
}
