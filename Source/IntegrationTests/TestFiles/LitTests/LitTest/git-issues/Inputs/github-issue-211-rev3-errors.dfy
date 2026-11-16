// Rules 3, 5 and 7 of revision 3 of issue 211: each line marked 'error' is rejected.
module Tr {
  trait T {
    function Size(): nat
    function {:termination_leaf} Weight(): nat
    method Run()
    function {:termination_leaf} Bad(): nat decreases {:global} 1  // error: a leaf has no bound
  }
  function G(t: T): nat { t.Size() }   // dispatches
  function {:termination_closed} C(): nat { 0 }
  function {:termination_leaf} L(): nat { 0 }
}
module Y { import Tr
  class Y extends Tr.T {
    const f: int -> nat
    constructor () { f := x => 0; }
    function Size(): nat { var me: Tr.T := this; me.Size() }   // error: dispatch (rule 3)
    function Weight(): nat { var me: Tr.T := this; me.Weight() } // error: a leaf may not dispatch to a leaf
    method Run() decreases {:global} 3 { }                     // error: rule 7
    function Bad(): nat { 0 }
  }
  class Z extends Tr.T {
    const f: int -> nat
    constructor () { f := x => 0; }
    function Size(): nat { Tr.G(this) }                        // error: through a home helper
    function Weight(): nat { f(0) }                            // error: application
    method Run() { var t: Tr.T := this; t.Run(); }             // error: dispatch
    function Bad(): nat { 0 }
  }
  class W extends Tr.T {
    constructor () { }
    function Size(): nat { Tr.C() }                            // error: Closed but not a leaf contract
    function Weight(): nat { Tr.L() }                          // a leaf contract
    method Run() { }
    function Bad(): nat { 0 }
  }
  method {:termination_leaf} NotLeaf(t: Tr.T) { t.Run(); }     // error: leaf promise
}
