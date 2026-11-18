// A library with a declared bound and a foreign leaf implementation.
module Lib {
  trait T { function Value(): nat  method Step(n: nat) decreases {:global} n }
}
module Impl { import Lib
  class C extends Lib.T {
    constructor () { }
    function Value(): nat { 7 }
    method Step(n: nat) decreases {:global} n { if n > 0 { var me: Lib.T := this; me.Step(n - 1); } }
  }
}
