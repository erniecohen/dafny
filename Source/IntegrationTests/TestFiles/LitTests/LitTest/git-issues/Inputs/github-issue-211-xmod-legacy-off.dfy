// Under the hidden --legacy-no-modular-termination, the rules of 4.11: a class may extend a trait of another module
// only with a legacy form, and "decreases {:global} n" is an ordinary decreases clause, which here does not decrease.
module Tr { trait T { method Go(n: nat) decreases {:global} n } }
module A { import Tr
  class A extends Tr.T {                                    // error: a trait of another module, unmarked
    constructor () { }
    method Go(n: nat) decreases {:global} n { }
  }
}
module B {
  method Loop(n: nat) decreases {:global} n { Loop(n); }    // error: the ordinary decreases clause n does not decrease
}
