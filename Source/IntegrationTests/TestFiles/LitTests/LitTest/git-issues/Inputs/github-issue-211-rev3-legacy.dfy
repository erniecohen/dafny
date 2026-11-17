// Legacy forms keep their 4.11 meaning: an unchecked premise for undeclared members; declared ones are checked.
module Tr {
  trait {:termination false} T { method Go(other: T)  method Step(n: nat) decreases {:global} n }
  trait U { method Go(other: U)  method Step(n: nat) decreases {:global} n }
}
module A { import Tr
  class A extends Tr.T {
    constructor () { }
    method Go(other: Tr.T) { other.Go(this); }              // premise: accepted
    method Step(n: nat) decreases {:global} n { var me: Tr.T := this; me.Step(n); } // checked: rejected
  }
  @AssumeCrossModuleTermination class B extends Tr.U {
    constructor () { }
    method Go(other: Tr.U) { other.Go(this); }              // premise: accepted
    method Step(n: nat) decreases {:global} n { var me: Tr.U := this; me.Step(n); } // checked: rejected
  }
}
