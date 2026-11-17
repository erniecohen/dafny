// Rule 3 of revision 3 of issue 211: each line marked 'error' is rejected.  A foreign implementation of a member
// without a bound must be a leaf, or declare a bound of its own.
module Tr {
  trait T {
    function Size(): nat
    function Weight(): nat decreases {:global} 0
    method Run()
  }
  function G(t: T): nat { t.Size() }   // dispatches
  function {:termination_closed} C(): nat { 0 }
  function H(): nat { 0 }              // no promise: a General contract
}
module Y { import Tr
  class Y extends Tr.T {
    constructor () { }
    function Size(): nat { var me: Tr.T := this; me.Size() }   // error: dispatch
    function Weight(): nat { 0 }
    method Run() decreases {:global} 3 { }                     // a declared bound instead of a leaf
  }
  class Z extends Tr.T {
    constructor () { }
    function Size(): nat { Tr.G(this) }                        // error: through a home helper
    function Weight(): nat { 0 }
    method Run() { var t: Tr.T := this; t.Run(); }             // error: dispatch
  }
  class W extends Tr.T {
    const f: int -> nat
    constructor () { f := x => 0; }
    function Size(): nat { f(0) }                              // error: application
    function Weight(): nat { 0 }
    method Run() { var c := Tr.C(); }                          // a closed helper is a leaf
  }
  class V extends Tr.T {
    constructor () { }
    function Size(): nat { Tr.H() }                            // error: a General contract
    function Weight(): nat { 0 }
    method Run() { }
  }
}
