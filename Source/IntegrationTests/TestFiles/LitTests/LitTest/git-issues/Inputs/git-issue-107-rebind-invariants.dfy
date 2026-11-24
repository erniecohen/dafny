// A rebound copy may copy a refinement that adds member invariants (issue 105).  The inherited code was
// verified against the copy of the previous target, so at each inherited site the conjuncts that the
// refinement added are checked, as for an abstract import bound to a refinement.
module Lib {
  datatype D = D(x: int) {
    invariant x >= 0
  }
  trait Named extends object {
    const name: string
    invariant |name| > 0
  }
}
module Lib1 refines Lib {
  datatype D ... {
    invariant x < 10
  }
  trait Named ... {
    invariant |name| < 5
  }
}
abstract module User {
  import L := Lib
  function Make(): L.D { L.D(20) }                     // error in User1
  function Small(): L.D { L.D(2) }
  class Thing extends L.Named {
    constructor () { name := "a long name"; }          // error in User1
  }
  class Short extends L.Named {
    constructor () { name := "abc"; }
  }
}
module User1 refines User {
  import L := Lib1
  // positive control: both invariants are known of a value of the rebound copy's type
  lemma Range(d: L.D) ensures 0 <= d.x < 10 { }
  // vacuity control: the facts above do not make the module inconsistent
  lemma Vacuity() ensures false { var d := Small(); assert d.x == 2; }
}
// A rebinding that adds no invariant checks nothing again.
module User2 refines User {
  import L := Lib
}

// Through a nested copy that is itself rebound.
module Mid {
  import L := Lib
  function Make(): L.D { L.D(20) }                     // error in Mid1
}
module Mid1 refines Mid {
  import L := Lib1
}
abstract module Outer {
  import Y := Mid
  function Make2(): Y.L.D { Y.L.D(30) }               // error in Outer1
}
module Outer1 refines Outer {
  import Y := Mid1
}

// An override of a member whose specification the new target strengthens is checked again.
module T0 {
  trait Tr extends object {
    method M() returns (r: int)
    function F(): int
  }
}
module T1 refines T0 {
  trait Tr ... {
    method M ... ensures r > 0
    function F ... ensures F() > 0
  }
}
module U {
  import X := T0
  class C extends X.Tr {
    constructor () { }
    method M() returns (r: int) { r := 0; }            // error in U1
    function F(): int { 0 }                            // error in U1
  }
}
module U1 refines U {
  import X := T1
}
