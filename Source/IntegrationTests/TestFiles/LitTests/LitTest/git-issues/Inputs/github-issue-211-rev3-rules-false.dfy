// Revision 3 of issue 211, rules 1 to 7, with no legacy form anywhere.
module Tr {
  trait T {
    function Size(): nat                                    // undeclared: foreign implementations must be leaves
    function {:termination_leaf} Weight(): nat              // explicit leaf: BOTTOM
    method Step(n: nat) returns (r: nat) decreases {:global} n  // declared
  }
  class X extends T {
    constructor () { }
    function Size(): nat { 1 }
    function Weight(): nat { 2 }
    method Step(n: nat) returns (r: nat) decreases {:global} n {
      var t: T := this;
      r := t.Weight();              // a leaf, from a bounded caller
      if n > 0 { r := t.Step(n - 1); }
    }
  }
  // A home implementation of an undeclared member keeps today's check.
  class H extends T {
    const other: T
    constructor (o: T) { other := o; }
    function Size(): nat { 0 }
    function Weight(): nat { 0 }
    method Step(n: nat) returns (r: nat) decreases {:global} n { r := 0; }
  }
}
module Y { import Tr
  // Rule 1: extended from another module with no legacy form.  Rule 3: these are leaves.
  class Y extends Tr.T {
    constructor () { }
    function Size(): nat { 3 + Helper() }
    function Weight(): nat { 4 }
    method Step(n: nat) returns (r: nat) decreases {:global} n {
      var t: Tr.T := this;
      if n > 0 { r := t.Step(n - 1); } else { r := t.Weight(); }
    }
  }
  function Helper(): nat { 5 }
}
module M { import Tr import Y
  // A caller without a bound may dispatch to an undeclared member (rule 4).
  method Main() {
    var x: Tr.T := new Tr.X();
    var y: Tr.T := new Y.Y();
    var s := x.Size() + y.Size();
    var r := y.Step(3);
    assert false;
  }
}
